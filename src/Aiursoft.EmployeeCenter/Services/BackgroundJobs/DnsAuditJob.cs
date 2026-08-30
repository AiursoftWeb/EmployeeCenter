using Aiursoft.Canon.BackgroundJobs;
using Aiursoft.EmployeeCenter.Services.DnsAudit;

namespace Aiursoft.EmployeeCenter.Services.BackgroundJobs;

public sealed class DnsAuditJob(
    CloudflareDnsAuditService auditService,
    DnsAuditSnapshotCache snapshotCache,
    ILogger<DnsAuditJob> logger) : IBackgroundJob
{
    public string Name => "Service Audit";
    public string Description => "Refreshes the cached service registry, DNS, and availability audit every three hours.";

    public async Task ExecuteAsync()
    {
        if (!snapshotCache.TryBeginRefresh())
        {
            logger.LogInformation("Service audit refresh is already running; skipping the overlapping trigger");
            return;
        }

        var attemptedAt = DateTime.UtcNow;
        try
        {
            if (!await auditService.IsConfiguredAsync())
            {
                snapshotCache.SetNotConfigured(attemptedAt);
                logger.LogInformation("Service audit skipped because the Cloudflare API token is not configured");
                return;
            }

            var report = await auditService.AuditAsync();
            snapshotCache.SetSuccess(report, attemptedAt);
            logger.LogInformation(
                "Service audit cache refreshed: {AvailabilityHealthy}/{AvailabilityChecked} public endpoints healthy, {Critical} critical, {Errors} errors, {Warnings} warnings, {Info} info",
                report.AvailabilityHealthyCount,
                report.AvailabilityCheckedCount,
                report.CriticalCount,
                report.ErrorCount,
                report.WarningCount,
                report.InfoCount);
        }
        catch (CloudflareDnsAuditException ex)
        {
            logger.LogWarning(ex, "Cloudflare portion of the service audit failed");
            snapshotCache.SetFailure(ex.Message, attemptedAt);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Service audit could not reach the Cloudflare API");
            snapshotCache.SetFailure("Cloudflare API could not be reached. The last successful audit remains available.", attemptedAt);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected service audit failure");
            snapshotCache.SetFailure("The service audit failed unexpectedly. The last successful audit remains available.", attemptedAt);
        }
        finally
        {
            snapshotCache.EndRefresh();
        }
    }
}
