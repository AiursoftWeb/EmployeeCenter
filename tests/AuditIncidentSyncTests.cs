using Aiursoft.AiurProtocol.Models;
using Aiursoft.AlertCenter.SDK.Models;
using Aiursoft.EmployeeCenter.Configuration;
using Aiursoft.EmployeeCenter.Models.DnsAuditViewModels;
using Aiursoft.EmployeeCenter.Services.DnsAudit;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;

namespace Aiursoft.EmployeeCenter.Tests;

[TestClass]
public class AuditIncidentSyncTests
{
    private static DnsAuditIssue Issue(DnsAuditSeverity severity = DnsAuditSeverity.Critical,
        DnsAuditIssueType type = DnsAuditIssueType.ServiceUnavailable, string domain = "payment.example.com") => new()
    {
        Type = type, Severity = severity, Domain = domain, ServiceId = 1, Details = "Probe evidence."
    };

    private static DnsAuditInput Evidence(bool? healthy = true, bool retired = false, string[]? zones = null) => new(
        zones ?? ["example.com"], 0, [],
        retired ? [] : [new Service { Id = 1, PrimaryDomain = "payment.example.com" }], [],
        new Dictionary<string, IReadOnlyCollection<string>>(),
        ServiceAvailabilityResults: healthy.HasValue
            ? new Dictionary<int, ServiceAvailabilityResult> { [1] = new(healthy.Value, healthy.Value ? 200 : 503, "Probe result") }
            : new Dictionary<int, ServiceAvailabilityResult>());

    private static DnsAuditReport Report(DnsAuditIssue[] issues, DnsAuditInput? evidence = null) => new()
    {
        Issues = issues, RecoveryEvidence = evidence, GeneratedAt = new DateTime(2026, 10, 17, 0, 0, 0, DateTimeKind.Utc)
    };

    private static AuditIncidentSyncService Synchronizer(FakeClient client) =>
        new(client, new TestAlertCenterSettingsProvider(), NullLogger<AuditIncidentSyncService>.Instance);

    private sealed class TestAlertCenterSettingsProvider(bool enabled = true, string key = "test-key") : IAlertCenterSettingsProvider
    {
        public Task<AlertCenterSettings> GetAsync() => Task.FromResult(new AlertCenterSettings
        {
            Enabled = enabled, ApiKey = key
        });
    }

    [TestMethod]
    [DataRow(DnsAuditIssueType.ServiceUnavailable, "public endpoint")]
    [DataRow(DnsAuditIssueType.MissingDns, "authoritative DNS")]
    [DataRow(DnsAuditIssueType.ProxyStatusMismatch, "Cloudflare")]
    public async Task DescriptionExplainsEvidenceActionAndRecovery(DnsAuditIssueType type, string expectedAction)
    {
        var client = new FakeClient();
        await Synchronizer(client).SynchronizeAsync(42, Report([Issue(type: type)]));
        var content = client.Created.Single().Content!;
        Assert.Contains("payment.example.com", content);
        Assert.Contains("Probe evidence.", content);
        Assert.Contains("audit run: 42", content);
        Assert.Contains(expectedAction, content);
        Assert.Contains("automatically mitigates", content);
        Assert.Contains("/ServiceAudit", content);
    }

    [TestMethod]
    public async Task LongEvidencePreservesOperatorInstructionsAndAuditLink()
    {
        var client = new FakeClient();
        var issue = new DnsAuditIssue { Type = DnsAuditIssueType.MissingDns, Severity = DnsAuditSeverity.Critical,
            Domain = "payment.example.com", Details = new string('x', 6000) };
        await Synchronizer(client).SynchronizeAsync(42, Report([issue]));
        var content = client.Created.Single().Content!;
        Assert.IsTrue(content.Length <= 4000);
        Assert.Contains("Next steps:", content);
        Assert.EndsWith("/ServiceAudit", content);
        Assert.AreEqual(issue.Details, client.Created.Single().Context!["evidence"]!["details"]!.Value<string>());
    }

    [TestMethod]
    [DataRow(false, "", true)]
    [DataRow(true, "", false)]
    public async Task DisabledOrIncompleteSettingsDoNotCreateIncidents(bool enabled, string key, bool success)
    {
        var client = new FakeClient();
        var sync = new AuditIncidentSyncService(client, new TestAlertCenterSettingsProvider(enabled, key),
            NullLogger<AuditIncidentSyncService>.Instance);
        Assert.AreEqual(success, await sync.SynchronizeAsync(1, Report([Issue()], Evidence(false))));
        Assert.HasCount(0, client.QueriedPages);
        Assert.HasCount(0, client.Created);
        Assert.HasCount(0, client.Mitigated);
    }

    [TestMethod]
    [DataRow("mitigated")]
    [DataRow("resolved")]
    public async Task ReconcilesUnchangedManuallyClosedRecoveredAndRecurringIssues(string inactiveStatus)
    {
        var client = new FakeClient();
        var sync = Synchronizer(client);
        var issue = Issue();
        Assert.IsTrue(await sync.SynchronizeAsync(1, Report([issue], Evidence(false))));
        var first = client.Incidents.Single();
        Assert.IsTrue(await sync.SynchronizeAsync(2, Report([issue], Evidence(false))));
        Assert.HasCount(1, client.Created);
        first.Status = inactiveStatus;
        Assert.IsTrue(await sync.SynchronizeAsync(3, Report([issue], Evidence(false))));
        Assert.HasCount(2, client.Created);
        Assert.AreNotEqual(client.Created[0].ReportId, client.Created[1].ReportId);
        Assert.IsTrue(await sync.SynchronizeAsync(4, Report([], Evidence())));
        Assert.AreEqual("mitigated", client.Incidents[1].Status);
        Assert.HasCount(1, client.Mitigated);
        Assert.Contains("Audit run 4", client.Mitigated[0].Request.Reason);
        Assert.IsTrue(await sync.SynchronizeAsync(5, Report([issue], Evidence(false))));
        Assert.HasCount(3, client.Created);
    }

    [TestMethod]
    public async Task MapsSeverityAndKeepsIdentityIndependentOfDetailsCaseAndSeverity()
    {
        var client = new FakeClient();
        var sync = Synchronizer(client);
        await sync.SynchronizeAsync(1, Report([
            Issue(), Issue(DnsAuditSeverity.Error, domain: "error.example.com"),
            Issue(DnsAuditSeverity.Warning, domain: "warning.example.com"),
            Issue(DnsAuditSeverity.Info, domain: "info.example.com")]));
        CollectionAssert.AreEquivalent(new[] { "sev2", "sev3", "sev4" }, client.Created.Select(item => item.Level).ToArray());
        Assert.AreEqual("EmployeeCenter.ServiceAudit", client.Created[0].Context?.Value<string>("source"));
        Assert.IsTrue(client.Created[0].Context?["evidence"] is JObject);
        await sync.SynchronizeAsync(2, Report([new DnsAuditIssue
        {
            Type = DnsAuditIssueType.ServiceUnavailable, Severity = DnsAuditSeverity.Warning,
            Domain = "PAYMENT.EXAMPLE.COM.", ServiceId = 1, Details = "Changed probe text."
        }]));
        Assert.HasCount(3, client.Created);
    }

    [TestMethod]
    public async Task ReusesReportIdAfterAnUncertainCreateAndDoesNotHideSyncFailure()
    {
        var client = new FakeClient { FailCreates = true };
        var sync = Synchronizer(client);
        var report = Report([Issue()]);
        Assert.IsFalse(await sync.SynchronizeAsync(5, report));
        client.FailCreates = false;
        Assert.IsTrue(await sync.SynchronizeAsync(5, report));
        Assert.AreEqual(client.Created[0].ReportId, client.Created[1].ReportId);
        Assert.AreEqual(client.Created[0].OccurredAtUtc, client.Created[1].OccurredAtUtc);
    }

    [TestMethod]
    [DataRow(null, false, false)]
    [DataRow(false, false, false)]
    [DataRow(true, false, true)]
    [DataRow(null, true, true)]
    public async Task ClosesOnlyConfirmedRecoveryOrRetiredTargets(bool? healthy, bool retired, bool shouldMitigate)
    {
        var client = new FakeClient();
        var sync = Synchronizer(client);
        await sync.SynchronizeAsync(1, Report([Issue()]));
        Assert.IsTrue(await sync.SynchronizeAsync(2, Report([], Evidence(healthy, retired))));
        Assert.AreEqual(shouldMitigate ? 1 : 0, client.Mitigated.Count);
    }

    [TestMethod]
    [DataRow(DnsAuditIssueType.UnverifiableDnsTarget)]
    [DataRow(DnsAuditIssueType.ManagedDnsOutsideRecordApi)]
    [DataRow(DnsAuditIssueType.PublicDnsLookupFailed)]
    [DataRow(DnsAuditIssueType.ServiceOutsideAuditedZone)]
    public async Task IncompleteDnsEvidenceCannotMitigateAnEarlierMismatch(DnsAuditIssueType uncertainty)
    {
        var client = new FakeClient();
        var sync = Synchronizer(client);
        await sync.SynchronizeAsync(1, Report([Issue(type: DnsAuditIssueType.ServerAssignmentMismatch)]));
        await sync.SynchronizeAsync(2, Report([Issue(DnsAuditSeverity.Info, uncertainty)], Evidence()));
        Assert.IsEmpty(client.Mitigated);
        await sync.SynchronizeAsync(3, Report([], Evidence(zones: [])));
        Assert.IsEmpty(client.Mitigated);
        await sync.SynchronizeAsync(4, Report([], Evidence()));
        Assert.HasCount(1, client.Mitigated);
    }

    [TestMethod]
    public async Task HistoricalReportsAndForeignOrMalformedIncidentsCannotAuthorizeClosures()
    {
        var client = new FakeClient();
        var sync = Synchronizer(client);
        await sync.SynchronizeAsync(1, Report([Issue()]));
        client.Incidents.Add(new IncidentDetails { IncidentId = Guid.NewGuid(), Status = "new", Context = new JObject { ["source"] = "Another task" } });
        client.Incidents.Add(new IncidentDetails { IncidentId = Guid.NewGuid(), Status = "new", Context = new JObject { ["source"] = new JObject() } });
        await sync.SynchronizeAsync(2, Report([]));
        Assert.IsEmpty(client.Mitigated);
        await sync.SynchronizeAsync(3, Report([], Evidence()));
        Assert.HasCount(1, client.Mitigated);
        Assert.AreEqual(2, client.Incidents.Count(item => item.Status == "new"));
    }

    [TestMethod]
    public async Task ReadsEveryPageBeforeClosingDuplicatesAndKeepsOneActiveIncident()
    {
        var client = new FakeClient();
        var sync = Synchronizer(client);
        await sync.SynchronizeAsync(1, Report([Issue()]));
        var original = client.Incidents.Single();
        for (var index = 0; index < 100; index++)
            client.Incidents.Add(new IncidentDetails
            {
                IncidentId = Guid.NewGuid(), Version = Guid.NewGuid(), Status = "acked",
                Context = (JObject)original.Context!.DeepClone(), ReceivedAtUtc = original.ReceivedAtUtc.AddMinutes(1)
            });
        client.QueriedPages.Clear();
        client.FailPage = 2;
        Assert.IsFalse(await sync.SynchronizeAsync(2, Report([Issue()])));
        Assert.IsEmpty(client.Mitigated);
        client.FailPage = 0;
        client.QueriedPages.Clear();
        await sync.SynchronizeAsync(2, Report([Issue()]));
        CollectionAssert.AreEqual(new[] { 1, 2 }, client.QueriedPages);
        Assert.AreEqual(100, client.Mitigated.Count);
        Assert.IsTrue(client.Mitigated.All(item => item.Request.Reason.Contains("duplicate")));
        Assert.AreEqual("new", original.Status);
        Assert.AreEqual(1, client.Incidents.Count(item => item.Status is not ("mitigated" or "resolved")));
    }

    [TestMethod]
    public async Task QueryFailureAbortsAllWritesAndResolutionConflictDoesNotForceClose()
    {
        var client = new FakeClient();
        var sync = Synchronizer(client);
        await sync.SynchronizeAsync(1, Report([Issue()]));
        client.FailQueries = true;
        Assert.IsFalse(await sync.SynchronizeAsync(2, Report([], Evidence())));
        Assert.IsEmpty(client.Mitigated);
        client.FailQueries = false;
        client.FailMitigates = true;
        Assert.IsFalse(await sync.SynchronizeAsync(3, Report([], Evidence())));
        Assert.AreEqual("new", client.Incidents.Single().Status);
        Assert.HasCount(1, client.Mitigated);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task HttpAliasRecoveryRequiresAConfirmedRedirect(bool matches)
    {
        var client = new FakeClient();
        var sync = Synchronizer(client);
        var issue = new DnsAuditIssue
        {
            Type = DnsAuditIssueType.DomainAliasRedirectMismatch, Severity = DnsAuditSeverity.Critical,
            Domain = "alias.example.com", DomainAliasId = 2, Details = "Unexpected redirect."
        };
        await sync.SynchronizeAsync(1, Report([issue]));
        var evidence = Evidence() with
        {
            DomainAliases = [new DomainAlias { Id = 2, Domain = issue.Domain, Type = DomainAliasType.HttpRedirect }],
            DomainAliasRedirectResults = new Dictionary<string, DomainAliasRedirectResult>
            {
                [issue.Domain] = new(matches, matches ? 302 : null, null, "Redirect observation")
            }
        };
        await sync.SynchronizeAsync(2, Report([], evidence));
        Assert.AreEqual(matches ? 1 : 0, client.Mitigated.Count);
    }

    private sealed class FakeClient : IAuditIncidentClient
    {
        public List<IncidentDetails> Incidents { get; } = [];
        public List<CreateIncidentRequest> Created { get; } = [];
        public List<(Guid Id, MitigateIncidentRequest Request)> Mitigated { get; } = [];
        public List<int> QueriedPages { get; } = [];
        public int FailPage { get; set; }
        public bool FailQueries { get; set; }
        public bool FailCreates { get; set; }
        public bool FailMitigates { get; set; }
        public Task<QueryIncidentsResponse> QueryAsync(QueryIncidentsRequest request)
        {
            if (FailQueries || request.Page == FailPage) throw new HttpRequestException("Query unavailable");
            QueriedPages.Add(request.Page);
            Assert.IsTrue(request.ActiveOnly);
            var active = Incidents.Where(item => item.Status is not ("mitigated" or "resolved")).ToList();
            return Task.FromResult(new QueryIncidentsResponse
            {
                Code = Code.JobDone, Page = request.Page, PageSize = request.PageSize, TotalCount = active.Count,
                Items = active.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList()
            });
        }
        public Task<CreateIncidentResponse> CreateAsync(CreateIncidentRequest request)
        {
            Created.Add(request);
            if (FailCreates) throw new HttpRequestException("Create outcome unknown");
            var incident = new IncidentDetails
            {
                IncidentId = Guid.NewGuid(), Version = Guid.NewGuid(), Status = "new",
                Context = request.Context, ReportId = request.ReportId, ReceivedAtUtc = DateTimeOffset.UtcNow
            };
            Incidents.Add(incident);
            return Task.FromResult(new CreateIncidentResponse { Code = Code.JobDone, IncidentId = incident.IncidentId });
        }
        public Task<MitigateIncidentResponse> MitigateAsync(Guid incidentId, MitigateIncidentRequest request)
        {
            Mitigated.Add((incidentId, request));
            if (FailMitigates) throw new InvalidOperationException("Version conflict");
            var incident = Incidents.Single(item => item.IncidentId == incidentId);
            Assert.AreEqual(incident.Version, request.Version);
            incident.Status = "mitigated";
            return Task.FromResult(new MitigateIncidentResponse { Code = Code.JobDone, IncidentId = incidentId });
        }
    }
}
