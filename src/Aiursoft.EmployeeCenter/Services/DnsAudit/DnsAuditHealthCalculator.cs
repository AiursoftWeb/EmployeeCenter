using Aiursoft.EmployeeCenter.Models.DnsAuditViewModels;

namespace Aiursoft.EmployeeCenter.Services.DnsAudit;

public sealed record DnsAuditHealthScore(
    double Percentage,
    int HealthyServiceCount,
    int TotalServiceCount,
    int HealthyDnsHostnameCount,
    int TotalDnsHostnameCount)
{
    public int HealthySubjectCount => HealthyServiceCount + HealthyDnsHostnameCount;
    public int TotalSubjectCount => TotalServiceCount + TotalDnsHostnameCount;
}

public static class DnsAuditHealthCalculator
{
    public static DnsAuditHealthScore Calculate(
        IReadOnlyCollection<int> registeredServiceIds,
        DnsAuditReport report)
    {
        var unhealthyIssues = report.Issues
            .Where(issue => issue.Severity >= DnsAuditSeverity.Warning)
            .ToList();
        var unhealthyServiceCount = unhealthyIssues
            .Where(issue => issue.ServiceId.HasValue && registeredServiceIds.Contains(issue.ServiceId.Value))
            .Select(issue => issue.ServiceId!.Value)
            .Distinct()
            .Count();
        var unhealthyDnsHostnameCount = unhealthyIssues
            .Where(issue => !issue.ServiceId.HasValue)
            .Select(issue => DnsAuditAnalyzer.NormalizeDomain(issue.Domain))
            .Where(domain => domain.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(report.AuditedHostnameCount)
            .Count();

        var healthyServiceCount = Math.Max(0, registeredServiceIds.Count - unhealthyServiceCount);
        var healthyDnsHostnameCount = Math.Max(0, report.AuditedHostnameCount - unhealthyDnsHostnameCount);
        var healthySubjectCount = healthyServiceCount + healthyDnsHostnameCount;
        var totalSubjectCount = registeredServiceIds.Count + report.AuditedHostnameCount;
        var percentage = totalSubjectCount == 0
            ? 100
            : Math.Round(healthySubjectCount * 100.0 / totalSubjectCount, 1);

        return new DnsAuditHealthScore(
            percentage,
            healthyServiceCount,
            registeredServiceIds.Count,
            healthyDnsHostnameCount,
            report.AuditedHostnameCount);
    }
}
