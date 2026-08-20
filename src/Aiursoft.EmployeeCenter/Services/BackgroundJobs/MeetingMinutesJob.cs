using Aiursoft.Canon.BackgroundJobs;
using Aiursoft.EmployeeCenter.Configuration;
using Aiursoft.EmployeeCenter.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Aiursoft.EmployeeCenter.Services.BackgroundJobs;

public class MeetingMinutesJob(
    EmployeeCenterDbContext db,
    MeetingMinutesService meetingMinutesService,
    MeetingMinutesQueueService meetingMinutesQueueService,
    IOptions<AppSettings> appSettings,
    ILogger<MeetingMinutesJob> logger) : IBackgroundJob
{
    private const int BatchSize = 50;
    private readonly AgentSettings _agentSettings = appSettings.Value.Agent;

    public string Name => "Meeting Minutes Job";
    public string Description => "Generates Markdown meeting minutes for completed audio transcripts.";

    public async Task ExecuteAsync()
    {
        try
        {
            var candidates = await db.AudioAsrResults
                .Include(result => result.Audio)
                .Where(result => result.PlainText != string.Empty)
                .Where(result => result.TranscriptRevision == result.MeetingMinutesTranscriptRevision)
                .Where(result => result.MeetingMinutesMarkdown == null || result.MeetingMinutesMarkdown == string.Empty)
                .Where(result => result.MeetingMinutesAttemptCount < _agentSettings.MeetingMinutesMaxRetryCount)
                .OrderBy(result => result.MeetingMinutesAttemptCount)
                .ThenBy(result => result.CreateTime)
                .Take(BatchSize)
                .ToListAsync();

            if (candidates.Count == 0)
            {
                logger.LogInformation("No transcripts awaiting meeting minutes generation.");
                return;
            }

            logger.LogInformation("Found {Count} transcripts awaiting meeting minutes generation.", candidates.Count);
            foreach (var candidate in candidates)
            {
                try
                {
                    await meetingMinutesQueueService.ExecuteIfNotActiveAsync(
                        candidate.AudioId,
                        candidate.TranscriptRevision,
                        candidate.CreateTime,
                        () => meetingMinutesService.GenerateAsync(candidate));
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unexpected error while generating meeting minutes for audio {AudioId}.", candidate.AudioId);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred in the meeting minutes job.");
        }
    }
}
