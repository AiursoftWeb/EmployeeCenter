using Aiursoft.Canon.BackgroundJobs;

namespace Aiursoft.EmployeeCenter.Services.BackgroundJobs;

public class AudioFileCleanupJob(
    AudioFileCleanupService fileCleanupService,
    ILogger<AudioFileCleanupJob> logger) : IBackgroundJob
{
    public string Name => "Audio File Cleanup Job";
    public string Description => "Retries queued deletion of unreferenced audio files.";

    public async Task ExecuteAsync()
    {
        var removedCount = await fileCleanupService.CleanupQueuedAsync();
        logger.LogInformation("Removed {Count} queued audio files.", removedCount);
    }
}
