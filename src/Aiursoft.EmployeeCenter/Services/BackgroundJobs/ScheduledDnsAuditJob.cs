using Aiursoft.Canon.BackgroundJobs;
using Aiursoft.EmployeeCenter.Services.DnsAudit;

namespace Aiursoft.EmployeeCenter.Services.BackgroundJobs;

public sealed class ScheduledDnsAuditJob(ServiceAuditStore auditStore, DnsAuditJob auditJob) : IBackgroundJob
{
    public string Name => "Scheduled Service Audit";
    public string Description => "Checks services every 15 minutes while findings persist, or every three hours when healthy.";

    public async Task ExecuteAsync()
    {
        if (await auditStore.IsScheduledAuditDueAsync(DateTime.UtcNow))
            await auditJob.ExecuteAsync();
    }
}
