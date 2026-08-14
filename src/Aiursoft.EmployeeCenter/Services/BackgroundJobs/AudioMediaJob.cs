using Aiursoft.Canon.BackgroundJobs;
using Aiursoft.EmployeeCenter.Configuration;
using Aiursoft.EmployeeCenter.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Aiursoft.EmployeeCenter.Services.BackgroundJobs;

public class AudioMediaJob(
    EmployeeCenterDbContext context,
    AudioMediaQueueService mediaQueueService,
    IOptions<AsrSettings> settings,
    ILogger<AudioMediaJob> logger) : IBackgroundJob
{
    public string Name => "Audio Media Job";
    public string Description => "Recovers and queues uploaded audio and video media processing.";

    public async Task ExecuteAsync()
    {
        var staleBefore = DateTime.UtcNow.AddSeconds(-settings.Value.MediaProcessingTimeoutSeconds - 60);
        var staleItems = await context.Audios
            .Where(audio =>
                audio.MediaStatus == AudioMediaStatus.Processing &&
                audio.MediaProcessingStartedTime < staleBefore)
            .ToListAsync();
        foreach (var audio in staleItems)
        {
            audio.MediaStatus = AudioMediaStatus.Uploaded;
            audio.MediaProcessingToken = null;
            audio.MediaProcessingStartedTime = null;
        }
        await context.SaveChangesAsync();

        var audioIds = await context.Audios
            .Where(audio => audio.MediaStatus == AudioMediaStatus.Uploaded)
            .OrderBy(audio => audio.CreateTime)
            .Select(audio => audio.Id)
            .Take(50)
            .ToListAsync();
        var queuedCount = 0;
        foreach (var audioId in audioIds)
        {
            if (mediaQueueService.QueueIfNotActive(audioId))
            {
                queuedCount++;
            }
        }
        logger.LogInformation("Queued {Count} audio media processing tasks.", queuedCount);
    }
}
