using System.Text.Json.Serialization;
using Aiursoft.EmployeeCenter.Configuration;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.Scanner.Abstractions;
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

    public async Task GenerateAsync(AudioAsrResult asrResult)
    {
        if (string.IsNullOrWhiteSpace(asrResult.PlainText) ||
            !string.IsNullOrWhiteSpace(asrResult.MeetingMinutesMarkdown) ||
            asrResult.MeetingMinutesAttemptCount >= _agentSettings.MeetingMinutesMaxRetryCount)
        {
            return;
        }

        asrResult.MeetingMinutesAttemptCount++;
        asrResult.LastMeetingMinutesAttemptTime = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        try
        {
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

            asrResult.MeetingMinutesMarkdown = result.Answer.Trim();
            await dbContext.SaveChangesAsync();
            logger.LogInformation("Successfully generated meeting minutes for audio {AudioId}.", asrResult.AudioId);
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

    private sealed class MeetingMinutesAgentResponse
    {
        [JsonPropertyName("answer")]
        public string? Answer { get; set; }
    }
}
