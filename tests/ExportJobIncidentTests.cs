using System.Text.Json;
using Aiursoft.AiurProtocol.Models;
using Aiursoft.AlertCenter.SDK.Models;
using Aiursoft.EmployeeCenter.Configuration;
using Aiursoft.EmployeeCenter.Services.BackgroundJobs;
using Aiursoft.EmployeeCenter.Services.DnsAudit;
using Aiursoft.EmployeeCenter.Tests.IntegrationTests;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.EmployeeCenter.Services.FileStorage;

namespace Aiursoft.EmployeeCenter.Tests;

[TestClass]
public class ExportJobIncidentTests : TestBase
{
    private const string StateKey = "Internal.ExportJob.AlertState";
    private readonly FakeClient _client = new();

    [TestInitialize]
    public override async Task CreateServer()
    {
        await base.CreateServer();
        using var scope = Server!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
        var old = await db.GlobalSettings.FindAsync(StateKey);
        if (old != null) db.GlobalSettings.Remove(old);
        await db.SaveChangesAsync();
    }

    private async Task RecordAsync(bool succeeded, bool enabled = true)
    {
        // A new dependency scope for each execution also verifies restart-safe database state.
        using var scope = Server!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
        await new ExportJobIncidentService(db, _client, new Settings(enabled),
            NullLogger<ExportJobIncidentService>.Instance).RecordAsync(succeeded, succeeded ? null : "IOException");
    }

    private async Task<ExportJobIncidentService.State> ReadStateAsync()
    {
        using var scope = Server!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
        var row = await db.GlobalSettings.FindAsync(StateKey);
        return JsonSerializer.Deserialize<ExportJobIncidentService.State>(row!.Value!)!;
    }

    [TestMethod]
    public async Task EscalatesFourthFailureRecoversAndCreatesNewIncidentOnRecurrence()
    {
        await RecordAsync(false);
        var first = _client.Incidents.Single();
        Assert.AreEqual("sev3", first.Level);
        Assert.Contains("/Jobs", _client.Created.Single().Content!);
        Assert.Contains("IOException", _client.Created.Single().Content!);
        await RecordAsync(false);
        await RecordAsync(false);
        Assert.AreEqual("sev3", first.Level);
        Assert.HasCount(0, _client.Escalated);
        await RecordAsync(false);
        Assert.AreEqual("sev2", first.Level);
        Assert.HasCount(1, _client.Created);
        Assert.HasCount(1, _client.Escalated);
        await RecordAsync(false);
        Assert.AreEqual(5, (await ReadStateAsync()).Failures);
        Assert.HasCount(1, _client.Escalated);
        await RecordAsync(true);
        Assert.AreEqual("new", first.Status);
        Assert.AreEqual(1, (await ReadStateAsync()).Successes);
        await RecordAsync(true);
        Assert.AreEqual("mitigated", first.Status);
        Assert.AreEqual(2, (await ReadStateAsync()).Successes);
        Assert.AreEqual(0, (await ReadStateAsync()).Failures);
        await RecordAsync(false);
        Assert.HasCount(2, _client.Created);
        Assert.AreNotEqual(first.IncidentId, _client.Incidents.Last().IncidentId);
        Assert.AreEqual("sev3", _client.Incidents.Last().Level);
        Assert.AreEqual("mitigated", first.Status);
    }

    [TestMethod]
    public async Task FailureInterruptsRecoveryAndTwoConsecutiveSuccessesPersistAcrossScopes()
    {
        for (var i = 0; i < 4; i++) await RecordAsync(false);
        var incident = _client.Incidents.Single();
        await RecordAsync(true);
        Assert.AreEqual("new", incident.Status);
        Assert.AreEqual(1, (await ReadStateAsync()).Successes);
        await RecordAsync(false);
        Assert.AreEqual(0, (await ReadStateAsync()).Successes);
        Assert.AreEqual(1, (await ReadStateAsync()).Failures);
        Assert.HasCount(1, _client.Created);
        Assert.AreEqual("sev2", incident.Level);
        await RecordAsync(true);
        Assert.AreEqual("new", incident.Status);
        await RecordAsync(true);
        Assert.AreEqual("mitigated", incident.Status);
        Assert.AreEqual(2, (await ReadStateAsync()).Successes);
        Assert.AreEqual(0, (await ReadStateAsync()).Failures);
    }

    [TestMethod]
    public async Task LostCreateResponseAndNewScopeDoNotDuplicateIncident()
    {
        _client.LoseCreateResponse = true;
        await RecordAsync(false);
        Assert.HasCount(1, _client.Incidents);
        _client.LoseCreateResponse = false;
        await RecordAsync(false);
        Assert.HasCount(1, _client.Created);
        Assert.HasCount(1, _client.Incidents);
        Assert.AreEqual(2, (await ReadStateAsync()).Failures);
    }

    [TestMethod]
    public async Task FailedCreateReusesPersistedReceipt()
    {
        _client.FailCreates = true;
        await RecordAsync(false);
        var receipt = _client.Created.Single().ReportId;
        _client.FailCreates = false;
        await RecordAsync(false);
        Assert.AreEqual(receipt, _client.Created.Last().ReportId);
        Assert.HasCount(1, _client.Incidents);
    }

    [TestMethod]
    public async Task QueryFailurePreservesOutcomeAndDoesNotCreateOrMitigateBlindly()
    {
        await RecordAsync(false);
        _client.FailQueries = true;
        await RecordAsync(true);
        await RecordAsync(true);
        Assert.AreEqual(0, (await ReadStateAsync()).Failures);
        Assert.AreEqual("new", _client.Incidents.Single().Status);
        _client.FailQueries = false;
        await RecordAsync(true);
        Assert.AreEqual("mitigated", _client.Incidents.Single().Status);
        Assert.HasCount(1, _client.Created);
    }

    [TestMethod]
    public async Task FailedRecoveryRetriesAndPreservesHigherHumanSeverity()
    {
        await RecordAsync(false);
        var incident = _client.Incidents.Single();
        incident.Level = "sev1";
        for (var i = 0; i < 4; i++) await RecordAsync(false);
        Assert.AreEqual("sev1", incident.Level);
        Assert.HasCount(0, _client.Escalated);
        _client.FailMitigates = true;
        await RecordAsync(true);
        await RecordAsync(true);
        Assert.AreEqual("new", incident.Status);
        _client.FailMitigates = false;
        await RecordAsync(true);
        Assert.AreEqual("mitigated", incident.Status);
    }

    [TestMethod]
    public async Task HumanClosedIncidentRemainsClosedAndNextFailureUsesNewReceipt()
    {
        await RecordAsync(false);
        var old = _client.Incidents.Single();
        old.Status = "resolved";
        await RecordAsync(false);
        Assert.HasCount(2, _client.Incidents);
        Assert.AreNotEqual(old.ReportId, _client.Created.Last().ReportId);
        Assert.AreEqual("resolved", old.Status);
    }

    [TestMethod]
    public async Task DisabledIntegrationStillPersistsCounterAndDoesNotCallApi()
    {
        for (var i = 0; i < 4; i++) await RecordAsync(false, enabled: false);
        Assert.HasCount(0, _client.Created);
        await RecordAsync(false);
        Assert.AreEqual("sev2", _client.Incidents.Single().Level);
        await RecordAsync(true, enabled: false);
        Assert.AreEqual(0, (await ReadStateAsync()).Failures);
    }

    [TestMethod]
    public async Task ExportJobPreservesRealFailureAndSuccessWhenAlertApiIsUnavailable()
    {
        using var scope = Server!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<StorageService>();
        _client.FailQueries = true;
        var incidents = new ExportJobIncidentService(db, _client, new Settings(true),
            NullLogger<ExportJobIncidentService>.Instance);
        var path = Path.Combine(Path.GetTempPath(), "ExportJob-" + Guid.NewGuid());
        await File.WriteAllTextAsync(path, "A file cannot be an export directory");
        try
        {
            var settings = scope.ServiceProvider.GetRequiredService<IOptions<AppSettings>>().Value;
            var export = new ExportService(db, Options.Create(new AppSettings
            {
                ExportPath = path, AuthProvider = settings.AuthProvider, OIDC = settings.OIDC,
                Local = settings.Local, OCR = settings.OCR, Agent = settings.Agent
            }), storage,
                NullLogger<ExportService>.Instance);
            var job = new ExportJob(export, NullLogger<ExportJob>.Instance, incidents);
            await Assert.ThrowsExactlyAsync<IOException>(() => job.ExecuteAsync());
            Assert.AreEqual(1, (await ReadStateAsync()).Failures);
            File.Delete(path);
            await job.ExecuteAsync();
            Assert.AreEqual(0, (await ReadStateAsync()).Failures);
            Assert.IsTrue(File.Exists(Path.Combine(path, "GlobalSettings", "settings.md")));
        }
        finally
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private sealed class Settings(bool enabled) : IAlertCenterSettingsProvider
    {
        public Task<AlertCenterSettings> GetAsync() => Task.FromResult(new AlertCenterSettings { Enabled = enabled, ApiKey = "synthetic-test-key" });
    }

    private sealed class FakeClient : IAuditIncidentClient
    {
        public List<IncidentDetails> Incidents { get; } = [];
        public List<CreateIncidentRequest> Created { get; } = [];
        public List<Guid> Escalated { get; } = [];
        public bool LoseCreateResponse { get; set; }
        public bool FailCreates { get; set; }
        public bool FailQueries { get; set; }
        public bool FailMitigates { get; set; }
        public Task<QueryIncidentsResponse> QueryAsync(QueryIncidentsRequest request)
        {
            if (FailQueries) throw new HttpRequestException("Synthetic query failure");
            Assert.AreEqual("source", request.MetadataKey);
            Assert.AreEqual("EmployeeCenter.ExportJob", request.MetadataValue);
            Assert.IsFalse(request.ActiveOnly);
            return Task.FromResult(new QueryIncidentsResponse
            {
                Code = Code.JobDone, Page = request.Page, PageSize = request.PageSize,
                TotalCount = Incidents.Count, Items = Incidents.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList()
            });
        }
        public Task<CreateIncidentResponse> CreateAsync(CreateIncidentRequest request)
        {
            Created.Add(request);
            if (FailCreates) throw new HttpRequestException("Synthetic create failure");
            var incident = new IncidentDetails
            {
                IncidentId = Guid.NewGuid(), ReportId = request.ReportId, Version = Guid.NewGuid(),
                Context = request.Context, Status = "new", Level = request.Level, ReceivedAtUtc = DateTimeOffset.UtcNow
            };
            Incidents.Add(incident);
            if (LoseCreateResponse) throw new HttpRequestException("Synthetic lost response");
            return Task.FromResult(new CreateIncidentResponse { Code = Code.JobDone, IncidentId = incident.IncidentId });
        }
        public Task<ChangeIncidentSeverityResponse> ChangeSeverityAsync(Guid id, ChangeIncidentSeverityRequest request)
        {
            var incident = Incidents.Single(item => item.IncidentId == id);
            Assert.AreEqual(incident.Version, request.Version);
            incident.Level = request.Level;
            incident.Version = Guid.NewGuid();
            Escalated.Add(id);
            return Task.FromResult(new ChangeIncidentSeverityResponse { Code = Code.JobDone, IncidentId = id });
        }
        public Task<MitigateIncidentResponse> MitigateAsync(Guid id, MitigateIncidentRequest request)
        {
            if (FailMitigates) throw new HttpRequestException("Synthetic recovery failure");
            var incident = Incidents.Single(item => item.IncidentId == id);
            Assert.AreEqual(incident.Version, request.Version);
            incident.Status = "mitigated";
            incident.Version = Guid.NewGuid();
            return Task.FromResult(new MitigateIncidentResponse { Code = Code.JobDone, IncidentId = id });
        }
    }
}
