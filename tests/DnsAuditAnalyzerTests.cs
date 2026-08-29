using Aiursoft.EmployeeCenter.Models.DnsAuditViewModels;
using Aiursoft.EmployeeCenter.Services.DnsAudit;

namespace Aiursoft.EmployeeCenter.Tests;

[TestClass]
public class DnsAuditAnalyzerTests
{
    [TestMethod]
    public void FindsUnknownDnsRecord()
    {
        var report = Analyze(
            records:
            [
                Record("known.example.com", "A", "192.0.2.10", proxied: true),
                Record("unknown.example.com", "A", "192.0.2.10", proxied: true)
            ],
            services: [RegisteredService("known.example.com", proxied: true)],
            servers: [RegisteredServer("192.0.2.10")]);

        Assert.HasCount(1, report.Issues);
        var issue = report.Issues.Single();
        Assert.AreEqual(DnsAuditIssueType.UnknownDns, issue.Type);
        Assert.AreEqual(DnsAuditSeverity.Error, issue.Severity);
        Assert.AreEqual("unknown.example.com", issue.Domain);
    }

    [TestMethod]
    public void FindsMissingDnsForRunningService()
    {
        var report = Analyze(
            records: [],
            services: [RegisteredService("missing.example.com")],
            servers: []);

        Assert.HasCount(1, report.Issues);
        var issue = report.Issues.Single();
        Assert.AreEqual(DnsAuditIssueType.MissingDns, issue.Type);
        Assert.AreEqual(DnsAuditSeverity.Critical, issue.Severity);
    }

    [TestMethod]
    public void FindsDnsOnlyIpv4WithoutIpv6()
    {
        var report = Analyze(
            records: [Record("v4-only.example.com", "A", "192.0.2.10", proxied: false)],
            services: [RegisteredService("v4-only.example.com")],
            servers: [RegisteredServer("192.0.2.10")]);

        Assert.HasCount(1, report.Issues);
        var issue = report.Issues.Single();
        Assert.AreEqual(DnsAuditIssueType.AddressFamilyMismatch, issue.Type);
        StringAssert.Contains(issue.Details, "IPv4 but not IPv6");
    }

    [TestMethod]
    public void AllowsProxiedIpv4OriginWithoutIpv6Origin()
    {
        var report = Analyze(
            records: [Record("proxied.example.com", "A", "192.0.2.10", proxied: true)],
            services: [RegisteredService("proxied.example.com", proxied: true)],
            servers: [RegisteredServer("192.0.2.10")]);

        Assert.IsEmpty(report.Issues);
    }

    [TestMethod]
    public void FindsDnsAddressThatDoesNotBelongToRegisteredServer()
    {
        var report = Analyze(
            records: [Record("moved.example.com", "A", "198.51.100.99", proxied: true)],
            services: [RegisteredService("moved.example.com", proxied: true)],
            servers: [RegisteredServer("192.0.2.10")]);

        Assert.HasCount(1, report.Issues);
        var issue = report.Issues.Single();
        Assert.AreEqual(DnsAuditIssueType.UnknownServer, issue.Type);
        Assert.AreEqual(DnsAuditSeverity.Critical, issue.Severity);
        StringAssert.Contains(issue.Details, "198.51.100.99");
    }

    [TestMethod]
    public void RecognizesIpv4AndIpv6RegisteredOnTheSameServer()
    {
        var server = RegisteredServer("192.0.2.10", id: 1, ipv6Address: "2001:db8::10");
        var report = Analyze(
            records: [Record("dual-stack.example.com", "AAAA", "2001:db8::10", proxied: true)],
            services: [RegisteredService("dual-stack.example.com", proxied: true, serverId: server.Id)],
            servers: [server]);

        Assert.IsFalse(report.Issues.Any(issue => issue.Type == DnsAuditIssueType.UnknownServer));
        Assert.IsFalse(report.Issues.Any(issue => issue.Type == DnsAuditIssueType.ServerAssignmentMismatch));
    }

    [TestMethod]
    public void AcceptsFrpsServerIpv6ForAServiceRunningOnAnotherServer()
    {
        var runningServer = RegisteredServer("192.168.50.178", id: 1);
        var frpsServer = RegisteredServer(
            "124.160.101.12",
            id: 2,
            ipv6Address: "240e:f7:a020:203::9:de");
        var service = RegisteredService(
            "apkg-dev.example.com",
            proxied: true,
            serverId: runningServer.Id,
            isViaFrps: true,
            frpsServerId: frpsServer.Id);

        var report = Analyze(
            records: [Record("apkg-dev.example.com", "AAAA", "240e:f7:a020:203::9:de", proxied: true)],
            services: [service],
            servers: [runningServer, frpsServer]);

        Assert.IsEmpty(report.Issues);
    }

    [TestMethod]
    public void FindsFrpsServiceWithoutFrpsServerAssignment()
    {
        var report = Analyze(
            records: [Record("frps.example.com", "A", "192.0.2.10", proxied: true)],
            services:
            [
                RegisteredService(
                    "frps.example.com",
                    proxied: true,
                    serverId: 1,
                    isViaFrps: true,
                    frpsServerId: null)
            ],
            servers: [RegisteredServer("192.0.2.10")]);

        Assert.IsTrue(report.Issues.Any(issue =>
            issue.Type == DnsAuditIssueType.MissingFrpsServerAssignment &&
            issue.Severity == DnsAuditSeverity.Warning));
    }

    [TestMethod]
    public void UsesResolvedAddressesForDnsOnlyCnameSymmetryAndServerCheck()
    {
        var report = Analyze(
            records: [Record("alias.example.com", "CNAME", "origin.external.example", proxied: false)],
            services: [RegisteredService("alias.example.com")],
            servers: [RegisteredServer("192.0.2.10, 2001:db8::10")],
            resolvedCnames: new Dictionary<string, IReadOnlyCollection<string>>
            {
                ["alias.example.com"] = ["192.0.2.10", "2001:db8::10"]
            });

        Assert.IsEmpty(report.Issues);
    }

    [TestMethod]
    public void FindsCloudflareProxyStatusDrift()
    {
        var report = Analyze(
            records: [Record("proxy.example.com", "A", "192.0.2.10", proxied: true)],
            services: [RegisteredService("proxy.example.com", proxied: false)],
            servers: [RegisteredServer("192.0.2.10")]);

        Assert.IsTrue(report.Issues.Any(issue => issue.Type == DnsAuditIssueType.ProxyStatusMismatch));
    }

    [TestMethod]
    public void FindsServiceAssignedToDifferentRegisteredServer()
    {
        var expectedServer = RegisteredServer("192.0.2.10", id: 1);
        var actualServer = RegisteredServer("198.51.100.20", id: 2);
        var report = Analyze(
            records: [Record("wrong-server.example.com", "A", "198.51.100.20", proxied: true)],
            services: [RegisteredService("wrong-server.example.com", proxied: true, serverId: expectedServer.Id)],
            servers: [expectedServer, actualServer]);

        Assert.IsTrue(report.Issues.Any(issue => issue.Type == DnsAuditIssueType.ServerAssignmentMismatch));
        Assert.IsFalse(report.Issues.Any(issue => issue.Type == DnsAuditIssueType.UnknownServer));
    }

    [TestMethod]
    public void FindsPublishedOfflineService()
    {
        var report = Analyze(
            records: [Record("offline.example.com", "A", "192.0.2.10", proxied: true)],
            services: [RegisteredService("offline.example.com", proxied: true, status: ServiceStatus.Offline)],
            servers: [RegisteredServer("192.0.2.10")]);

        Assert.IsTrue(report.Issues.Any(issue => issue.Type == DnsAuditIssueType.OfflineServicePublished));
    }

    [TestMethod]
    public void FindsDanglingDnsOnlyCname()
    {
        var report = Analyze(
            records: [Record("dangling.example.com", "CNAME", "missing.external.example", proxied: false)],
            services: [RegisteredService("dangling.example.com")],
            servers: []);

        Assert.IsTrue(report.Issues.Any(issue => issue.Type == DnsAuditIssueType.DanglingCname));
        Assert.IsTrue(report.Issues.Any(issue =>
            issue.Type == DnsAuditIssueType.UnverifiableDnsTarget &&
            issue.Severity == DnsAuditSeverity.Info));
    }

    [TestMethod]
    public void FindsServiceOutsideEveryVisibleCloudflareZone()
    {
        var report = Analyze(
            records: [],
            services: [RegisteredService("service.other-zone.test")],
            servers: []);

        Assert.IsTrue(report.Issues.Any(issue =>
            issue.Type == DnsAuditIssueType.ServiceOutsideAuditedZone &&
            issue.Severity == DnsAuditSeverity.Warning));
    }

    [TestMethod]
    public void FindsRunningServiceWithoutServerAssignment()
    {
        var report = Analyze(
            records: [Record("unassigned.example.com", "A", "192.0.2.10", proxied: true)],
            services: [RegisteredService("unassigned.example.com", proxied: true, serverId: null)],
            servers: [RegisteredServer("192.0.2.10")]);

        Assert.IsTrue(report.Issues.Any(issue => issue.Type == DnsAuditIssueType.MissingServerAssignment));
    }

    [TestMethod]
    public void ForbidsWildcardDnsForEveryRecordType()
    {
        var report = Analyze(
            records:
            [
                Record("*.example.com", "A", "192.0.2.10", proxied: true),
                Record("*.internal.example.com", "TXT", "wildcard-policy-test", proxied: false)
            ],
            services: [],
            servers: [RegisteredServer("192.0.2.10")]);

        var wildcardIssues = report.Issues
            .Where(issue => issue.Type == DnsAuditIssueType.WildcardDnsRecord)
            .ToList();
        Assert.HasCount(2, wildcardIssues);
        Assert.IsTrue(wildcardIssues.All(issue => issue.Severity == DnsAuditSeverity.Critical));
        Assert.IsTrue(wildcardIssues.Any(issue => issue.Domain == "*.internal.example.com"));
    }

    [TestMethod]
    public void RecognizesCloudflareManagedHostnameOutsideStandardRecordApi()
    {
        var report = Analyze(
            records: [],
            services: [RegisteredService("packages.example.com", proxied: true)],
            servers: [RegisteredServer("192.0.2.10")],
            publiclyResolvableDomains: ["packages.example.com"]);

        Assert.IsFalse(report.Issues.Any(issue => issue.Type == DnsAuditIssueType.MissingDns));
        Assert.IsTrue(report.Issues.Any(issue =>
            issue.Type == DnsAuditIssueType.ManagedDnsOutsideRecordApi &&
            issue.Severity == DnsAuditSeverity.Info));
    }

    private static DnsAuditReport Analyze(
        IReadOnlyCollection<DnsAuditRecord> records,
        IReadOnlyCollection<Service> services,
        IReadOnlyCollection<Server> servers,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? resolvedCnames = null,
        IReadOnlyCollection<string>? publiclyResolvableDomains = null)
    {
        return DnsAuditAnalyzer.Analyze(new DnsAuditInput(
            ["example.com"],
            records.Count,
            records,
            services,
            servers,
            resolvedCnames ?? new Dictionary<string, IReadOnlyCollection<string>>(),
            publiclyResolvableDomains));
    }

    private static DnsAuditRecord Record(string name, string type, string content, bool proxied)
    {
        return new DnsAuditRecord
        {
            Id = Guid.NewGuid().ToString(),
            ZoneName = "example.com",
            Name = name,
            Type = type,
            Content = content,
            Proxied = proxied
        };
    }

    private static Service RegisteredService(
        string domain,
        bool proxied = false,
        int? serverId = 1,
        ServiceStatus status = ServiceStatus.Running,
        bool isViaFrps = false,
        int? frpsServerId = null)
    {
        return new Service
        {
            Id = Random.Shared.Next(1, int.MaxValue),
            Domain = domain,
            IsCloudflareProxied = proxied,
            ServerId = serverId,
            IsViaFrps = isViaFrps,
            FrpsServerId = frpsServerId,
            Status = status
        };
    }

    private static Server RegisteredServer(string addresses, int id = 1, string? ipv6Address = null)
    {
        return new Server
        {
            Id = id,
            ServerIp = addresses,
            Ipv6Address = ipv6Address
        };
    }
}
