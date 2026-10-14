using Aiursoft.Canon.BackgroundJobs;
using Aiursoft.EmployeeCenter.Services.FileStorage;

namespace Aiursoft.EmployeeCenter.Services.BackgroundJobs;

public sealed class QualificationUploadCleanupJob(QualificationFileService files) : IBackgroundJob
{
    public string Name => "Qualification upload cleanup";
    public string Description => "Removes private qualification uploads abandoned for at least one hour.";

    public Task ExecuteAsync()
    {
        files.DeleteExpiredUploads(DateTime.UtcNow.AddHours(-1));
        return Task.CompletedTask;
    }
}
