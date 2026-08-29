using Aiursoft.Canon.BackgroundJobs;
using Aiursoft.EmployeeCenter.Services.DnsAudit;

namespace Aiursoft.EmployeeCenter.Services.BackgroundJobs;

public sealed class DnsAuditJob(
    CloudflareDnsAuditService auditService,
    DnsAuditSnapshotCache snapshotCache,
    ILogger<DnsAuditJob> logger) : IBackgroundJob
{
    public string Name => "DNS Audit";
    public string Description => "Refreshes the cached Cloudflare DNS audit snapshot every 20 minutes.";

    public async Task ExecuteAsync()
    {
        if (!snapshotCache.TryBeginRefresh())
        {
            logger.LogInformation("DNS audit refresh is already running; skipping the overlapping trigger");
            return;
        }

        var attemptedAt = DateTime.UtcNow;
        try
        {
            if (!await auditService.IsConfiguredAsync())
            {
                snapshotCache.SetNotConfigured(attemptedAt);
                logger.LogInformation("DNS audit skipped because the Cloudflare API token is not configured");
                return;
            }

            var report = await auditService.AuditAsync();
            snapshotCache.SetSuccess(report, attemptedAt);
            logger.LogInformation(
                "DNS audit cache refreshed: {Critical} critical, {Errors} errors, {Warnings} warnings, {Info} info",
                report.CriticalCount,
                report.ErrorCount,
                report.WarningCount,
                report.InfoCount);
        }
        catch (CloudflareDnsAuditException ex)
        {
            logger.LogWarning(ex, "Cloudflare DNS audit failed");
            snapshotCache.SetFailure(ex.Message, attemptedAt);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Cloudflare DNS audit could not reach the API");
            snapshotCache.SetFailure("Cloudflare API could not be reached. The last successful audit remains available.", attemptedAt);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected DNS audit failure");
            snapshotCache.SetFailure("The DNS audit failed unexpectedly. The last successful audit remains available.", attemptedAt);
        }
        finally
        {
            snapshotCache.EndRefresh();
        }
    }
}
