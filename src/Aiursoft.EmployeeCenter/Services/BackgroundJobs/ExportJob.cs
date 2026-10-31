using Aiursoft.Canon.BackgroundJobs;

namespace Aiursoft.EmployeeCenter.Services.BackgroundJobs;

public class ExportJob(
    ExportService exportService,
    ILogger<ExportJob> logger,
    ExportJobIncidentService incidentService) : IBackgroundJob
{
    public string Name => "Export Job";
    public string Description => "Periodically exports blueprints and contracts to the specified path.";

    public async Task ExecuteAsync()
    {
        logger.LogInformation("Export job triggered.");
        try
        {
            await exportService.ExportAsync();
        }
        catch (Exception ex)
        {
            await incidentService.RecordAsync(false, ex.GetType().Name);
            throw;
        }
        await incidentService.RecordAsync(true);
        logger.LogInformation("Export job execution finished.");
    }
}