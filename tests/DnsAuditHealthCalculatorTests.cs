using Aiursoft.EmployeeCenter.Models.DnsAuditViewModels;
using Aiursoft.EmployeeCenter.Services.DnsAudit;

namespace Aiursoft.EmployeeCenter.Tests;

[TestClass]
public class DnsAuditHealthCalculatorTests
{
    [TestMethod]
    public void CacheKeepsTheLastGoodReportWhenARefreshFails()
    {
        var cache = new DnsAuditSnapshotCache();
        var report = Report(1);
        cache.SetSuccess(report, DateTime.UtcNow.AddMinutes(-1));

        cache.SetFailure("Temporary failure", DateTime.UtcNow);

        Assert.AreSame(report, cache.Current.Report);
        Assert.AreEqual("Temporary failure", cache.Current.ErrorMessage);
        Assert.IsTrue(cache.Current.IsConfigured);
    }

    [TestMethod]
    public void NoBlockingFindingsAlwaysProducesOneHundredPercent()
    {
        var report = Report(3,
            Issue(DnsAuditSeverity.Info, "managed.example.com", 1),
            Issue(DnsAuditSeverity.Info, "r2.example.com"));

        var score = DnsAuditHealthCalculator.Calculate([1, 2], report);

        Assert.AreEqual(100, score.Percentage);
        Assert.AreEqual(5, score.HealthySubjectCount);
        Assert.AreEqual(5, score.TotalSubjectCount);
    }

    [TestMethod]
    public void RepeatedFindingsOnlyPenalizeEachAuditSubjectOnce()
    {
        var report = Report(2,
            Issue(DnsAuditSeverity.Warning, "service.example.com", 1),
            Issue(DnsAuditSeverity.Critical, "service.example.com", 1),
            Issue(DnsAuditSeverity.Warning, "unknown.example.com"),
            Issue(DnsAuditSeverity.Error, "unknown.example.com"));

        var score = DnsAuditHealthCalculator.Calculate([1, 2], report);

        Assert.AreEqual(50, score.Percentage);
        Assert.AreEqual(1, score.HealthyServiceCount);
        Assert.AreEqual(1, score.HealthyDnsHostnameCount);
        Assert.AreEqual(2, score.HealthySubjectCount);
        Assert.AreEqual(4, score.TotalSubjectCount);
    }

    [TestMethod]
    public void EmptyRegistryAndDnsSnapshotIsHealthy()
    {
        var score = DnsAuditHealthCalculator.Calculate([], Report(0));

        Assert.AreEqual(100, score.Percentage);
        Assert.AreEqual(0, score.TotalSubjectCount);
    }

    private static DnsAuditReport Report(int auditedHostnameCount, params DnsAuditIssue[] issues)
    {
        return new DnsAuditReport
        {
            ZoneCount = 1,
            RecordCount = auditedHostnameCount,
            AuditedHostnameCount = auditedHostnameCount,
            Issues = issues
        };
    }

    private static DnsAuditIssue Issue(DnsAuditSeverity severity, string domain, int? serviceId = null)
    {
        return new DnsAuditIssue
        {
            Type = DnsAuditIssueType.UnknownDns,
            Severity = severity,
            Domain = domain,
            Details = "Test finding",
            ServiceId = serviceId
        };
    }
}
