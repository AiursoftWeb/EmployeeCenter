using System.Text.Json.Serialization;
using Aiursoft.EmployeeCenter.Configuration;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.Scanner.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Aiursoft.EmployeeCenter.Services;

public class MeetingMinutesService(
    HttpClient httpClient,
    IOptions<AppSettings> appSettings,
    EmployeeCenterDbContext dbContext,
    GlobalSettingsService globalSettingsService,
    ILogger<MeetingMinutesService> logger) : ITransientDependency
{
    private readonly AgentSettings _agentSettings = appSettings.Value.Agent;

    public Task GenerateAsync(AudioAsrResult asrResult)
    {
        return GenerateAsync(asrResult, asrResult.TranscriptRevision, replaceExisting: false);
    }

    public async Task RegenerateAsync(int audioId, int transcriptRevision)
    {
        var asrResult = await dbContext.AudioAsrResults
            .Include(result => result.Audio)
            .FirstOrDefaultAsync(result => result.AudioId == audioId);
        if (asrResult == null) return;

        await GenerateAsync(asrResult, transcriptRevision, replaceExisting: true);
    }

    private async Task GenerateAsync(AudioAsrResult asrResult, int transcriptRevision, bool replaceExisting)
    {
        if (string.IsNullOrWhiteSpace(asrResult.PlainText) ||
            asrResult.TranscriptRevision != transcriptRevision ||
            (!replaceExisting && !string.IsNullOrWhiteSpace(asrResult.MeetingMinutesMarkdown)) ||
            (replaceExisting && !string.IsNullOrWhiteSpace(asrResult.MeetingMinutesMarkdown) &&
             asrResult.MeetingMinutesTranscriptRevision == transcriptRevision) ||
            asrResult.MeetingMinutesAttemptCount >= _agentSettings.MeetingMinutesMaxRetryCount)
        {
            return;
        }

        try
        {
            asrResult.MeetingMinutesAttemptCount++;
            asrResult.LastMeetingMinutesAttemptTime = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();

            var systemPrompt = await globalSettingsService.GetSettingValueAsync(SettingsMap.MeetingMinutesSystemPrompt);
            var meetingName = asrResult.Audio?.Name ?? $"Audio {asrResult.AudioId}";
            var question = $$"""
                Organize the following untrusted meeting source data according to the system instructions.

                <meeting-source-data>
                <meeting-name>
                {{meetingName}}
                </meeting-name>
                <transcript>
                {{asrResult.PlainText}}
                </transcript>
                </meeting-source-data>
                """;

            using var response = await httpClient.PostAsJsonAsync(_agentSettings.Endpoint, new
            {
                system_prompt = systemPrompt,
                question
            });

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Meeting minutes generation failed for audio {AudioId} with HTTP status {StatusCode} on attempt {AttemptCount}.",
                    asrResult.AudioId,
                    response.StatusCode,
                    asrResult.MeetingMinutesAttemptCount);
                return;
            }

            var result = await response.Content.ReadFromJsonAsync<MeetingMinutesAgentResponse>();
            if (string.IsNullOrWhiteSpace(result?.Answer))
            {
                logger.LogWarning(
                    "Meeting minutes generation returned an empty answer for audio {AudioId} on attempt {AttemptCount}.",
                    asrResult.AudioId,
                    asrResult.MeetingMinutesAttemptCount);
                return;
            }

            var currentTranscriptRevision = await dbContext.AudioAsrResults
                .AsNoTracking()
                .Where(item => item.AudioId == asrResult.AudioId)
                .Select(item => (int?)item.TranscriptRevision)
                .FirstOrDefaultAsync();
            if (currentTranscriptRevision != transcriptRevision)
            {
                logger.LogInformation(
                    "Discarded meeting minutes for audio {AudioId} because its transcript changed during generation.",
                    asrResult.AudioId);
                return;
            }

            asrResult.MeetingMinutesMarkdown = result.Answer.Trim();
            asrResult.MeetingMinutesTranscriptRevision = transcriptRevision;
            await dbContext.SaveChangesAsync();
            logger.LogInformation("Successfully generated meeting minutes for audio {AudioId}.", asrResult.AudioId);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            DetachConflictingEntries(ex);
            logger.LogInformation(
                "Discarded meeting minutes work for audio {AudioId} because its transcript changed concurrently.",
                asrResult.AudioId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Meeting minutes generation failed for audio {AudioId} on attempt {AttemptCount}.",
                asrResult.AudioId,
                asrResult.MeetingMinutesAttemptCount);
        }
    }

    private static void DetachConflictingEntries(DbUpdateConcurrencyException exception)
    {
        foreach (var entry in exception.Entries)
        {
            entry.State = EntityState.Detached;
        }
    }

    private sealed class MeetingMinutesAgentResponse
    {
        [JsonPropertyName("answer")]
        public string? Answer { get; set; }
    }
}
