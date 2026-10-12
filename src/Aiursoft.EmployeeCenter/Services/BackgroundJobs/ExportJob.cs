using Aiursoft.Canon.BackgroundJobs;

namespace Aiursoft.EmployeeCenter.Services.BackgroundJobs;

public class ExportJob(
    ExportService exportService,
    ILogger<ExportJob> logger) : IBackgroundJob
{
    public string Name => "Export Job";
    public string Description => "Periodically exports blueprints and contracts to the specified path.";

    public async Task ExecuteAsync()
    {
        logger.LogInformation("Export job triggered.");
        await exportService.ExportAsync();
        logger.LogInformation("Export job execution finished.");
    }
}