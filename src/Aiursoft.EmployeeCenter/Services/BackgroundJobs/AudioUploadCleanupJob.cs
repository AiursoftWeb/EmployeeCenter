using Aiursoft.Canon.BackgroundJobs;

namespace Aiursoft.EmployeeCenter.Services.BackgroundJobs;

public class AudioUploadCleanupJob(
    AudioUploadCleanupService cleanupService) : IBackgroundJob
{
    public string Name => "Audio Upload Cleanup";
    public string Description => "Removes expired abandoned audio uploads and consumed upload records.";

    public async Task ExecuteAsync()
    {
        await cleanupService.CleanupAsync(DateTime.UtcNow);
    }
}
