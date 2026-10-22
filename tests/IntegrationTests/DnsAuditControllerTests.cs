using Aiursoft.Canon.TaskQueue;
using Aiursoft.AlertCenter.SDK.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Aiursoft.EmployeeCenter.Configuration;
using Aiursoft.EmployeeCenter.InMemory;
using Aiursoft.EmployeeCenter.Models.DnsAuditViewModels;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.EmployeeCenter.Services.BackgroundJobs;
using Aiursoft.EmployeeCenter.Services.DnsAudit;

namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class DnsAuditControllerTests : TestBase
{
    [TestInitialize]
    public override async Task CreateServer()
    {
        await base.CreateServer();
        using var scope = Server!.Services.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<GlobalSettingsService>();
        await settings.UpdateSettingAsync(SettingsMap.ServiceAuditScope, "{}");
        await settings.UpdateSettingAsync(SettingsMap.CloudflareApiToken, string.Empty);
    }

    [TestCleanup]
    public override async Task CleanServer()
    {
        if (Server != null)
        {
            using var scope = Server.Services.CreateScope();
            var settings = scope.ServiceProvider.GetRequiredService<GlobalSettingsService>();
            await settings.UpdateSettingAsync(SettingsMap.ServiceAuditScope, "{}");
            await settings.UpdateSettingAsync(SettingsMap.CloudflareApiToken, string.Empty);
        }
        await base.CleanServer();
    }

    [TestMethod]
    public async Task AuditSettingsPersistNormalizedScopeAndPreserveRegistrations()
    {
        await LoginAsAdmin();
        var response = await PostForm("/ServiceAudit/Settings", new Dictionary<string, string>
        {
            ["AuditAllZones"] = "false", ["IncludedZones"] = " AIURSOFT.COM.\n aiursoft.com ",
            ["ExcludedDomains"] = " ANDUINLAB.COM. "
        });
        Assert.AreEqual(HttpStatusCode.Found, response.StatusCode);
        using var scope = Server!.Services.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<GlobalSettingsService>();
        var policy = ServiceAuditScope.Deserialize(await settings.GetSettingValueAsync(SettingsMap.ServiceAuditScope));
        Assert.IsFalse(policy.AuditAllZones);
        CollectionAssert.AreEqual(new[] { "aiursoft.com" }, policy.IncludedZones);
        Assert.IsTrue(policy.Excludes("temp.anduinlab.com"));
        Assert.IsFalse(policy.Excludes("notanduinlab.com"));
        var html = await Http.GetStringAsync("/ServiceAudit/Settings");
        Assert.Contains("anduinlab.com", html);
        Assert.DoesNotContain(SettingsMap.ServiceAuditScope, await Http.GetStringAsync("/GlobalSettings/Index"));
    }

    [TestMethod]
    public async Task AuditCheckboxSelectionsSurviveUnavailableZonesAndCanBeUnchecked()
    {
        await LoginAsAdmin();
        var response = await PostForm("/ServiceAudit/Settings", new Dictionary<string, string>
        {
            ["AuditAllZones"] = "false",
            ["IncludedZoneSelections[0]"] = "AIURSOFT.COM.",
            ["ExcludedDomainSelections[0]"] = "ANDUINLAB.COM.",
            ["ExcludedDomains"] = "another.example.com"
        });
        Assert.AreEqual(HttpStatusCode.Found, response.StatusCode);
        var html = await Http.GetStringAsync("/ServiceAudit/Settings");
        Assert.Contains("Cloudflare zones are unavailable", html);
        Assert.IsTrue(Regex.IsMatch(html, "name=\"IncludedZoneSelections\" value=\"aiursoft.com\" checked"));
        Assert.IsTrue(Regex.IsMatch(html, "name=\"ExcludedDomainSelections\" value=\"anduinlab.com\" checked"));
        Assert.IsTrue(Regex.IsMatch(html, "name=\"ExcludedDomainSelections\" value=\"another.example.com\" checked"));
        // A rejected edit keeps unavailable saved zones as unchecked choices so they can be selected again.
        response = await PostForm("/ServiceAudit/Settings", new Dictionary<string, string>
        {
            ["AuditAllZones"] = "false", ["ExcludedDomainSelections[0]"] = "anduinlab.com"
        });
        html = await response.Content.ReadAsStringAsync();
        Assert.Contains("validation-summary-errors", html);
        Assert.Contains("name=\"IncludedZoneSelections\" value=\"aiursoft.com\"", html);
        Assert.IsFalse(Regex.IsMatch(html, "name=\"IncludedZoneSelections\" value=\"aiursoft.com\" checked"));
        // Simulate unchecking the personal domain while keeping the other selection.
        response = await PostForm("/ServiceAudit/Settings", new Dictionary<string, string>
        {
            ["AuditAllZones"] = "true", ["ExcludedDomainSelections[0]"] = "another.example.com"
        });
        Assert.AreEqual(HttpStatusCode.Found, response.StatusCode);
        using var scope = Server!.Services.CreateScope();
        var policy = ServiceAuditScope.Deserialize(await scope.ServiceProvider.GetRequiredService<GlobalSettingsService>()
            .GetSettingValueAsync(SettingsMap.ServiceAuditScope));
        CollectionAssert.AreEqual(new[] { "another.example.com" }, policy.ExcludedDomains);
        Assert.IsFalse(policy.Excludes("temp.anduinlab.com"));
    }

    [TestMethod]
    public async Task AuditCheckboxListIncludesRegisteredAndLastDiscoveredDomains()
    {
        await LoginAsAdmin();
        using var scope = Server!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
        var service = new Service { PrimaryDomain = "registered.example.com", IsAvailabilityAuditEnabled = false };
        db.Services.Add(service);
        await db.SaveChangesAsync();
        var alias = new DomainAlias { Domain = "alias.example.com", TargetServiceId = service.Id };
        db.DomainAliases.Add(alias);
        await db.SaveChangesAsync();
        var run = new ServiceAuditRun { Status = ServiceAuditRunStatus.Succeeded, CompletedAt = DateTime.UtcNow };
        db.ServiceAuditRuns.Add(run);
        await db.SaveChangesAsync();
        var issue = new ServiceAuditIssue
        {
            ServiceAuditRunId = run.Id, Type = nameof(DnsAuditIssueType.UnknownDns), Severity = nameof(DnsAuditSeverity.Error),
            Domain = "discovered.example.com", Details = "Unregistered domain"
        };
        db.ServiceAuditIssues.Add(issue);
        await db.SaveChangesAsync();
        try
        {
            var html = await Http.GetStringAsync("/ServiceAudit/Settings");
            foreach (var domain in new[] { service.PrimaryDomain, alias.Domain, "discovered.example.com" })
                Assert.Contains($"name=\"ExcludedDomainSelections\" value=\"{domain}\"", html);
        }
        finally
        {
            db.ServiceAuditIssues.Remove(issue);
            db.ServiceAuditRuns.Remove(run);
            db.DomainAliases.Remove(alias);
            db.Services.Remove(service);
            await db.SaveChangesAsync();
        }
    }

    [TestMethod]
    public async Task InvalidAdditionalDomainPreservesCheckboxSelections()
    {
        await LoginAsAdmin();
        var response = await PostForm("/ServiceAudit/Settings", new Dictionary<string, string>
        {
            ["AuditAllZones"] = "false", ["IncludedZoneSelections[0]"] = "aiursoft.com",
            ["ExcludedDomainSelections[0]"] = "anduinlab.com", ["ExcludedDomains"] = "https://bad.example.com"
        });
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("validation-summary-errors", html);
        Assert.IsTrue(Regex.IsMatch(html, "name=\"ExcludedDomainSelections\" value=\"anduinlab.com\" checked"));
        Assert.Contains("https://bad.example.com", html);
        Assert.Contains("<details class=\"mb-3\" open", html);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("*.anduinlab.com")]
    [DataRow("https://anduinlab.com")]
    public async Task InvalidCheckboxValueDoesNotChangeScope(string domain)
    {
        await LoginAsAdmin();
        var response = await PostForm("/ServiceAudit/Settings", new Dictionary<string, string>
        {
            ["AuditAllZones"] = "true", ["ExcludedDomainSelections[0]"] = domain
        });
        response.EnsureSuccessStatusCode();
        Assert.Contains("validation-summary-errors", await response.Content.ReadAsStringAsync());
        using var scope = Server!.Services.CreateScope();
        Assert.AreEqual("{}", await scope.ServiceProvider.GetRequiredService<GlobalSettingsService>()
            .GetSettingValueAsync(SettingsMap.ServiceAuditScope));
    }

    [TestMethod]
    public async Task ZoneChoicesLoadOnlyZoneMetadataWithoutRunningAnAudit()
    {
        using var scope = Server!.Services.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<GlobalSettingsService>();
        await settings.UpdateSettingAsync(SettingsMap.CloudflareApiToken, "synthetic-audit-token");
        await using var db = new InMemoryContext(new DbContextOptionsBuilder<InMemoryContext>()
            .UseInMemoryDatabase($"zone-choices-{Guid.NewGuid():N}").Options);
        var factory = new ScopeHttpFactory();
        var audit = new CloudflareDnsAuditService(db, settings, factory, NullLogger<CloudflareDnsAuditService>.Instance);
        CollectionAssert.AreEqual(new[] { "aiursoft.com", "anduinlab.com" }, await audit.GetZoneNamesAsync());
        Assert.AreEqual(1, factory.Paths.Count);
        Assert.AreEqual("/client/v4/zones", factory.Paths[0]);
    }

    [TestMethod]
    [DataRow("", "")]
    [DataRow("https://aiursoft.com", "")]
    [DataRow("aiursoft.com", "*.anduinlab.com")]
    [DataRow("aiursoft.com", "149.28.157.30")]
    public async Task InvalidAuditSettingsDoNotChangeScope(string included, string excluded)
    {
        await LoginAsAdmin();
        var response = await PostForm("/ServiceAudit/Settings", new Dictionary<string, string>
        {
            ["AuditAllZones"] = "false", ["IncludedZones"] = included, ["ExcludedDomains"] = excluded
        });
        response.EnsureSuccessStatusCode();
        Assert.Contains("validation-summary-errors", await response.Content.ReadAsStringAsync());
        using var scope = Server!.Services.CreateScope();
        Assert.IsTrue(ServiceAuditScope.Deserialize(await scope.ServiceProvider.GetRequiredService<GlobalSettingsService>()
            .GetSettingValueAsync(SettingsMap.ServiceAuditScope)).AuditAllZones);
    }

    [TestMethod]
    public async Task AuditScopeCannotBypassSettingsPermissionOrAntiforgery()
    {
        await LoginAsAdmin();
        var response = await PostForm("/GlobalSettings/Edit", new Dictionary<string, string>
        {
            ["Key"] = SettingsMap.ServiceAuditScope, ["Value"] = "{}"
        }, tokenUrl: "/GlobalSettings/Index");
        Assert.AreEqual(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("/Error/Code403", response.Headers.Location?.OriginalString ?? string.Empty);
        response = await PostForm("/ServiceAudit/Settings", new Dictionary<string, string>(), includeToken: false);
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task AuditSettingsRespectDeploymentOverrides()
    {
        await LoginAsAdmin();
        using var scope = Server!.Services.CreateScope();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var cache = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
        var key = $"GlobalSettings:{SettingsMap.ServiceAuditScope}";
        var original = config[key];
        try
        {
            config[key] = "{\"ExcludedDomains\":[\"anduinlab.com\"]}";
            cache.Remove($"global-setting-{SettingsMap.ServiceAuditScope}");
            var html = await Http.GetStringAsync("/ServiceAudit/Settings");
            Assert.Contains("anduinlab.com", html);
            Assert.Contains("managed by deployment configuration", html);
            var response = await PostForm("/ServiceAudit/Settings", new Dictionary<string, string>
            {
                ["AuditAllZones"] = "true", ["ExcludedDomains"] = "aiursoft.com"
            });
            Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
        }
        finally
        {
            config[key] = original;
            cache.Remove($"global-setting-{SettingsMap.ServiceAuditScope}");
        }
    }

    [TestMethod]
    public async Task AuditSettingsPageUsesGeneratedChineseResources()
    {
        await LoginAsAdmin();
        var response = await Http.GetAsync("/Culture/Set?culture=zh-CN&returnUrl=/ServiceAudit/Settings");
        Assert.AreEqual(HttpStatusCode.Found, response.StatusCode);
        var html = WebUtility.HtmlDecode(await Http.GetStringAsync("/ServiceAudit/Settings"));
        Assert.Contains("审计设置", html);
        Assert.Contains("排除的域名", html);
    }

    [TestMethod]
    [DataRow(true, false, 3)]
    [DataRow(true, true, 1)]
    [DataRow(false, false, 1)]
    public async Task AuditScopeFiltersDiscoveryAndExcludedRegisteredHttpChecks(bool allZones, bool excludePersonal, int hostnameCount)
    {
        using var scope = Server!.Services.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<GlobalSettingsService>();
        await settings.UpdateSettingAsync(SettingsMap.CloudflareApiToken, "synthetic-audit-token");
        await settings.UpdateSettingAsync(SettingsMap.ServiceAuditScope, System.Text.Json.JsonSerializer.Serialize(new ServiceAuditScope
        {
            AuditAllZones = allZones, IncludedZones = ["aiursoft.com"],
            ExcludedDomains = excludePersonal ? ["anduinlab.com"] : []
        }));
        await using var db = new InMemoryContext(new DbContextOptionsBuilder<InMemoryContext>()
            .UseInMemoryDatabase($"scope-{Guid.NewGuid():N}").Options);
        if (excludePersonal)
        {
            db.Services.Add(new Service { PrimaryDomain = "temp.anduinlab.com", Protocols = "HTTPS" });
            await db.SaveChangesAsync();
        }
        var factory = new ScopeHttpFactory();
        var audit = new CloudflareDnsAuditService(db, settings, factory, NullLogger<CloudflareDnsAuditService>.Instance);
        var report = await audit.AuditAsync();
        Assert.AreEqual(hostnameCount, report.AuditedHostnameCount);
        Assert.IsTrue(report.Issues.Any(issue => issue.Domain == "new.aiursoft.com" && issue.Type == DnsAuditIssueType.UnknownDns));
        Assert.AreEqual(allZones && !excludePersonal, report.Issues.Any(issue => issue.Domain == "temp.anduinlab.com"));
        Assert.AreEqual(0, report.AvailabilityCheckedCount);
        Assert.IsTrue(factory.Paths.All(path => path.StartsWith("/client/v4/", StringComparison.Ordinal)));
        if (excludePersonal) Assert.AreEqual(1, await db.Services.CountAsync());
    }

    [TestMethod]
    public async Task RegisteredExternalServiceRemainsAuditedOutsideDiscoveryZones()
    {
        using var scope = Server!.Services.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<GlobalSettingsService>();
        await settings.UpdateSettingAsync(SettingsMap.CloudflareApiToken, "synthetic-audit-token");
        await settings.UpdateSettingAsync(SettingsMap.ServiceAuditScope,
            "{\"AuditAllZones\":false,\"IncludedZones\":[\"aiursoft.com\"]}");
        await using var db = new InMemoryContext(new DbContextOptionsBuilder<InMemoryContext>()
            .UseInMemoryDatabase($"registered-scope-{Guid.NewGuid():N}").Options);
        db.Services.Add(new Service { PrimaryDomain = "temp.anduinlab.com", IsAvailabilityAuditEnabled = false });
        await db.SaveChangesAsync();
        var audit = new CloudflareDnsAuditService(db, settings, new ScopeHttpFactory(), NullLogger<CloudflareDnsAuditService>.Instance);
        var report = await audit.AuditAsync();
        Assert.AreEqual(2, report.AuditedHostnameCount);
        Assert.IsFalse(report.Issues.Any(issue => issue.Domain == "unknown.anduinlab.com"));
        Assert.IsTrue(report.Issues.Any(issue => issue.Domain == "temp.anduinlab.com" && issue.Type == DnsAuditIssueType.UnknownServer));
        Assert.IsFalse(report.Issues.Any(issue => issue.Domain == "temp.anduinlab.com" && issue.Type == DnsAuditIssueType.UnknownDns));
    }

    [TestMethod]
    public async Task InaccessibleIncludedZoneFailsInsteadOfReportingGreen()
    {
        using var scope = Server!.Services.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<GlobalSettingsService>();
        await settings.UpdateSettingAsync(SettingsMap.CloudflareApiToken, "synthetic-audit-token");
        await settings.UpdateSettingAsync(SettingsMap.ServiceAuditScope,
            "{\"AuditAllZones\":false,\"IncludedZones\":[\"missing.example.com\"]}");
        await using var db = new InMemoryContext(new DbContextOptionsBuilder<InMemoryContext>()
            .UseInMemoryDatabase($"missing-zone-{Guid.NewGuid():N}").Options);
        var audit = new CloudflareDnsAuditService(db, settings, new ScopeHttpFactory(), NullLogger<CloudflareDnsAuditService>.Instance);
        await Assert.ThrowsExactlyAsync<CloudflareDnsAuditException>(() => audit.AuditAsync());
    }

    [TestMethod]
    public async Task ExcludedCnameTargetStillResolvesTheCompanyOriginWithoutAuditingTheTarget()
    {
        using var scope = Server!.Services.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<GlobalSettingsService>();
        await settings.UpdateSettingAsync(SettingsMap.CloudflareApiToken, "synthetic-audit-token");
        await settings.UpdateSettingAsync(SettingsMap.ServiceAuditScope,
            "{\"AuditAllZones\":false,\"IncludedZones\":[\"aiursoft.com\"],\"ExcludedDomains\":[\"anduinlab.com\"]}");
        await using var db = new InMemoryContext(new DbContextOptionsBuilder<InMemoryContext>()
            .UseInMemoryDatabase($"cname-scope-{Guid.NewGuid():N}").Options);
        var audit = new CloudflareDnsAuditService(db, settings, new ScopeHttpFactory(true), NullLogger<CloudflareDnsAuditService>.Instance);
        var report = await audit.AuditAsync();
        Assert.AreEqual(1, report.AuditedHostnameCount);
        Assert.IsFalse(report.Issues.Any(issue => issue.Domain.EndsWith("anduinlab.com", StringComparison.Ordinal)));
        var origin = report.Issues.Single(issue => issue.Type == DnsAuditIssueType.UnknownServer);
        Assert.AreEqual("new.aiursoft.com", origin.Domain);
        Assert.Contains("192.0.2.2", origin.Details);
        Assert.IsFalse(report.Issues.Any(issue => issue.Type == DnsAuditIssueType.DanglingCname));
    }

    private sealed class ScopeHttpFactory(bool crossZoneCname = false) : IHttpClientFactory
    {
        public List<string> Paths { get; } = [];
        public HttpClient CreateClient(string name) => new(new ScopeHttpHandler(Paths, crossZoneCname));
    }

    private sealed class ScopeHttpHandler(List<string> paths, bool crossZoneCname) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            paths.Add(path);
            var json = path switch
            {
                "/client/v4/zones" => "{\"success\":true,\"result\":[{\"id\":\"company\",\"name\":\"aiursoft.com\"},{\"id\":\"personal\",\"name\":\"anduinlab.com\"}]}",
                "/client/v4/zones/company/dns_records" when crossZoneCname => "{\"success\":true,\"result\":[{\"id\":\"company-record\",\"type\":\"CNAME\",\"name\":\"new.aiursoft.com\",\"content\":\"temp.anduinlab.com\",\"proxied\":false}]}",
                "/client/v4/zones/company/dns_records" => "{\"success\":true,\"result\":[{\"id\":\"company-record\",\"type\":\"A\",\"name\":\"new.aiursoft.com\",\"content\":\"192.0.2.1\",\"proxied\":true}]}",
                "/client/v4/zones/personal/dns_records" => "{\"success\":true,\"result\":[{\"id\":\"personal-record\",\"type\":\"A\",\"name\":\"temp.anduinlab.com\",\"content\":\"192.0.2.2\",\"proxied\":true},{\"id\":\"other-personal-record\",\"type\":\"A\",\"name\":\"unknown.anduinlab.com\",\"content\":\"192.0.2.3\",\"proxied\":true}]}",
                _ => throw new InvalidOperationException("Unexpected HTTP request: " + path)
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        }
    }

    private sealed class TestAlertCenterSettingsProvider : IAlertCenterSettingsProvider
    {
        public Task<AlertCenterSettings> GetAsync() => Task.FromResult(new AlertCenterSettings
        {
            Enabled = true, ApiKey = "test-key"
        });
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task AuditOutcomeRemainsIndependentOfAlertCenterAvailability(bool auditSucceeds)
    {
        await using var db = new InMemoryContext(new DbContextOptionsBuilder<InMemoryContext>()
            .UseInMemoryDatabase($"audit-sync-{Guid.NewGuid():N}").Options);
        using var scope = Server!.Services.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<GlobalSettingsService>();
        await settings.UpdateSettingAsync(SettingsMap.CloudflareApiToken, "audit-test-token");
        var client = new UnavailableIncidentClient();
        var sync = new AuditIncidentSyncService(client, new TestAlertCenterSettingsProvider(), NullLogger<AuditIncidentSyncService>.Instance);
        var cache = new DnsAuditSnapshotCache();
        var job = new DnsAuditJob(
            new CloudflareDnsAuditService(db, settings, new AuditHttpFactory(auditSucceeds), NullLogger<CloudflareDnsAuditService>.Instance),
            new ServiceAuditStore(db), cache, NullLogger<DnsAuditJob>.Instance, sync);
        await job.ExecuteAsync();
        var run = await db.ServiceAuditRuns.SingleAsync();
        Assert.AreEqual(auditSucceeds ? ServiceAuditRunStatus.Succeeded : ServiceAuditRunStatus.Failed, run.Status);
        Assert.AreEqual(auditSucceeds ? 1 : 0, client.Queries);
        if (auditSucceeds)
        {
            Assert.IsNull(run.ErrorMessage);
            Assert.IsNotNull(cache.Current.Report);
            Assert.IsNull(cache.Current.ErrorMessage);
        }
        else Assert.IsNotNull(run.ErrorMessage);
        Assert.IsTrue(cache.TryBeginRefresh());
        cache.EndRefresh();
    }

    private sealed class UnavailableIncidentClient : IAuditIncidentClient
    {
        public int Queries { get; private set; }
        public Task<QueryIncidentsResponse> QueryAsync(QueryIncidentsRequest request)
        {
            Queries++;
            throw new HttpRequestException("AlertCenter is unavailable.");
        }
        public Task<CreateIncidentResponse> CreateAsync(CreateIncidentRequest request) => throw new AssertFailedException("No writes after a failed read.");
        public Task<MitigateIncidentResponse> MitigateAsync(Guid incidentId, MitigateIncidentRequest request) => throw new AssertFailedException("No closures after a failed read.");
    }

    private sealed class AuditHttpFactory(bool succeeds) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new AuditHttpHandler(succeeds));
    }

    private sealed class AuditHttpHandler(bool succeeds) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(succeeds ? HttpStatusCode.OK : HttpStatusCode.BadGateway)
            {
                Content = new StringContent(succeeds ? "{\"success\":true,\"result\":[]}" : "{\"success\":false}")
            });
    }

    [TestMethod]
    public async Task AuditQueueCoalescesPendingRequests()
    {
        await using var db = new InMemoryContext(
            new DbContextOptionsBuilder<InMemoryContext>()
                .UseInMemoryDatabase($"audit-queue-{Guid.NewGuid():N}")
                .Options);
        var store = new ServiceAuditStore(db);

        var first = await store.QueueAsync("first-user");
        var second = await store.QueueAsync("second-user");

        Assert.AreEqual(first, second);
        Assert.AreEqual(1, await db.ServiceAuditRuns.CountAsync(run => run.Id == first));
    }

    [TestMethod]
    public async Task DomainAliasManagementRequiresDnsAuditAndServiceManagementPermissions()
    {
        await RegisterAndLoginAsync();

        var response = await Http.GetAsync("/DomainAliases/Index");

        Assert.AreEqual(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("/Error/Code403", response.Headers.Location?.OriginalString ?? string.Empty);
    }

    [TestMethod]
    public async Task DnsAuditRequiresItsOwnPermission()
    {
        await RegisterAndLoginAsync();

        var response = await Http.GetAsync("/ServiceAudit");

        Assert.AreEqual(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("/Error/Code403", response.Headers.Location?.OriginalString ?? string.Empty);
    }

    [TestMethod]
    public async Task AdminCanOpenDnsAuditWhenTokenIsNotConfigured()
    {
        await LoginAsAdmin();
        GetService<DnsAuditSnapshotCache>().SetNotConfigured(DateTime.UtcNow);

        var response = await Http.GetAsync("/ServiceAudit");

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Service Audit", html);
        Assert.Contains("Cloudflare API token is not configured", html);
    }

    [TestMethod]
    public async Task LegacyDnsAuditRouteRedirectsToServiceAudit()
    {
        await LoginAsAdmin();

        var response = await Http.GetAsync("/DnsAudit/Index");

        Assert.AreEqual(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.Contains("/ServiceAudit", response.Headers.Location?.OriginalString ?? string.Empty);
    }

    [TestMethod]
    public async Task AdminReadsTheCachedDnsAuditSnapshot()
    {
        await LoginAsAdmin();
        GetService<DnsAuditSnapshotCache>().SetSuccess(new DnsAuditReport
        {
            ZoneCount = 2,
            RecordCount = 42,
            AuditedHostnameCount = 7,
            Issues =
            [
                new DnsAuditIssue
                {
                    Type = DnsAuditIssueType.UnknownDns,
                    Severity = DnsAuditSeverity.Warning,
                    Domain = "cached-audit.example.com",
                    Details = "Cached finding"
                }
            ]
        }, DateTime.UtcNow);

        var response = await Http.GetAsync("/ServiceAudit");

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("cached-audit.example.com", html);
        Assert.Contains("Automatically refreshed every 3 hours", html);
    }

    [TestMethod]
    public async Task UnknownDnsFindingCanBeRegisteredAsDomainAlias()
    {
        await LoginAsAdmin();
        var db = GetService<EmployeeCenterDbContext>();
        var targetService = new Service
        {
            PrimaryDomain = "avigame.anduinlab.com",
            Status = ServiceStatus.Running
        };
        db.Services.Add(targetService);
        await db.SaveChangesAsync();

        await SaveAuditReportAsync(new DnsAuditReport
        {
            ZoneCount = 1,
            RecordCount = 1,
            AuditedHostnameCount = 1,
            Issues =
            [
                new DnsAuditIssue
                {
                    Type = DnsAuditIssueType.UnknownDns,
                    Severity = DnsAuditSeverity.Error,
                    Domain = "avigame.aiursoft.com",
                    Details = "Unknown DNS"
                }
            ]
        });

        var auditResponse = await Http.GetAsync("/ServiceAudit");
        auditResponse.EnsureSuccessStatusCode();
        Assert.Contains("Register as alias", await auditResponse.Content.ReadAsStringAsync());

        var response = await PostForm(
            "/DomainAliases/Create",
            new Dictionary<string, string>
            {
                ["Domain"] = "avigame.aiursoft.com",
                ["TargetServiceId"] = targetService.Id.ToString(),
                ["TargetUrl"] = "https://avigame.anduinlab.com/"
            },
            tokenUrl: "/DomainAliases/Create");

        Assert.AreEqual(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("/DomainAliases", response.Headers.Location?.OriginalString ?? string.Empty);
        db.ChangeTracker.Clear();
        var alias = await db.DomainAliases
            .Include(item => item.TargetService)
            .SingleAsync(item => item.Domain == "avigame.aiursoft.com");
        Assert.AreEqual("avigame.aiursoft.com", alias.Domain);
        Assert.AreEqual(DomainAliasType.HttpRedirect, alias.Type);
        Assert.AreEqual("https://avigame.anduinlab.com/", alias.TargetUrl);
        Assert.AreEqual(targetService.Id, alias.TargetServiceId);
    }

    [TestMethod]
    public async Task UnknownDnsFindingCanBeRegisteredAsCnameAliasWithoutTargetUrl()
    {
        await LoginAsAdmin();
        var db = GetService<EmployeeCenterDbContext>();
        var targetService = new Service
        {
            PrimaryDomain = "mail.aiursoft.com",
            Status = ServiceStatus.Running
        };
        db.Services.Add(targetService);
        await db.SaveChangesAsync();

        await SaveAuditReportAsync(new DnsAuditReport
        {
            ZoneCount = 1,
            RecordCount = 1,
            AuditedHostnameCount = 1,
            Issues =
            [
                new DnsAuditIssue
                {
                    Type = DnsAuditIssueType.UnknownDns,
                    Severity = DnsAuditSeverity.Error,
                    Domain = "autoconfig.aiursoft.com",
                    Details = "Unknown DNS"
                }
            ]
        });

        var response = await PostForm(
            "/DomainAliases/Create",
            new Dictionary<string, string>
            {
                ["Domain"] = "autoconfig.aiursoft.com",
                ["TargetServiceId"] = targetService.Id.ToString(),
                ["Type"] = ((int)DomainAliasType.Cname).ToString()
            },
            tokenUrl: "/DomainAliases/Create?sourceDomain=autoconfig.aiursoft.com");

        Assert.AreEqual(HttpStatusCode.Found, response.StatusCode);
        db.ChangeTracker.Clear();
        var alias = await db.DomainAliases.SingleAsync(item => item.Domain == "autoconfig.aiursoft.com");
        Assert.AreEqual(DomainAliasType.Cname, alias.Type);
        Assert.IsNull(alias.TargetUrl);
        Assert.AreEqual(targetService.Id, alias.TargetServiceId);
    }

    [TestMethod]
    public async Task AdminCanManageAliasesFromCentralDashboard()
    {
        await LoginAsAdmin();
        var db = GetService<EmployeeCenterDbContext>();
        var targetService = new Service { PrimaryDomain = "dashboard-target.example.com" };
        db.DomainAliases.Add(new DomainAlias
        {
            Domain = "legacy-dashboard.example.com",
            TargetService = targetService,
            TargetUrl = "https://dashboard-target.example.com/"
        });
        await db.SaveChangesAsync();
        await SaveAuditReportAsync(new DnsAuditReport
        {
            ZoneCount = 1,
            RecordCount = 2,
            AuditedHostnameCount = 2,
            Issues =
            [
                new DnsAuditIssue
                {
                    Type = DnsAuditIssueType.UnknownDns,
                    Severity = DnsAuditSeverity.Error,
                    Domain = "new-dashboard-alias.example.com",
                    Details = "Unknown DNS"
                }
            ]
        });

        var response = await Http.GetAsync("/DomainAliases/Index");

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Domain Alias Registry", html);
        Assert.Contains("legacy-dashboard.example.com", html);
        Assert.Contains("Available DNS candidates", html);
        Assert.Contains("/DomainAliases/Create", html);
        Assert.Contains("/DomainAliases/Delete", html);
        Assert.Contains("Healthy", html);
    }

    [TestMethod]
    public async Task CentralCreatePageListsCurrentUnknownDnsFindings()
    {
        await LoginAsAdmin();
        await SaveAuditReportAsync(new DnsAuditReport
        {
            ZoneCount = 1,
            RecordCount = 1,
            AuditedHostnameCount = 1,
            Issues =
            [
                new DnsAuditIssue
                {
                    Type = DnsAuditIssueType.UnknownDns,
                    Severity = DnsAuditSeverity.Error,
                    Domain = "selectable-alias.example.com",
                    Details = "Unknown DNS"
                }
            ]
        });

        var response = await Http.GetAsync("/DomainAliases/Create");

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("selectable-alias.example.com", html);
        Assert.Contains("Select audited DNS hostname", html);
    }

    [TestMethod]
    public async Task DomainAliasTargetMustMatchSelectedService()
    {
        await LoginAsAdmin();
        var db = GetService<EmployeeCenterDbContext>();
        var targetService = new Service { PrimaryDomain = "target.example.com" };
        db.Services.Add(targetService);
        await db.SaveChangesAsync();
        await SaveAuditReportAsync(new DnsAuditReport
        {
            ZoneCount = 1,
            RecordCount = 1,
            AuditedHostnameCount = 1,
            Issues =
            [
                new DnsAuditIssue
                {
                    Type = DnsAuditIssueType.UnknownDns,
                    Severity = DnsAuditSeverity.Error,
                    Domain = "alias.example.com",
                    Details = "Unknown DNS"
                }
            ]
        });

        var response = await PostForm(
            "/DomainAliases/Create",
            new Dictionary<string, string>
            {
                ["Domain"] = "alias.example.com",
                ["TargetServiceId"] = targetService.Id.ToString(),
                ["TargetUrl"] = "https://wrong.example.com/"
            },
            tokenUrl: "/DomainAliases/Create?sourceDomain=alias.example.com");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsFalse(await db.DomainAliases.AnyAsync(item => item.Domain == "alias.example.com"));
        Assert.Contains("must match the selected service", await response.Content.ReadAsStringAsync());
    }

    [TestMethod]
    public async Task ArbitraryHostnameCannotBeRegisteredOutsideCurrentAuditFinding()
    {
        await LoginAsAdmin();

        var response = await Http.GetAsync("/DomainAliases/Create?sourceDomain=internal.example.com");

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task AdminCanQueueAnImmediateDnsAuditRefresh()
    {
        await LoginAsAdmin();
        GetService<DnsAuditSnapshotCache>().SetNotConfigured(DateTime.UtcNow);
        var queue = GetService<ServiceTaskQueue>();
        var initialCount = queue.GetAllTasks().Count(task => task.ServiceType == typeof(DnsAuditJob));

        var response = await PostForm(
            "/ServiceAudit/Refresh",
            new Dictionary<string, string>(),
            tokenUrl: "/ServiceAudit");

        Assert.AreEqual(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("/ServiceAudit", response.Headers.Location?.OriginalString ?? string.Empty);
        await Task.Delay(100);
        var auditTasks = queue.GetAllTasks()
            .Where(task => task.ServiceType == typeof(DnsAuditJob))
            .ToList();
        Assert.IsGreaterThan(initialCount, auditTasks.Count);
        Assert.IsTrue(auditTasks.Any(task => task.TriggerSource == TaskTriggerSource.Manual));
    }

    [TestMethod]
    public async Task AdminCanQueueAuditFromAliasDashboard()
    {
        await LoginAsAdmin();
        var queue = GetService<ServiceTaskQueue>();
        var initialCount = queue.GetAllTasks().Count(task => task.ServiceType == typeof(DnsAuditJob));

        var response = await PostForm(
            "/DomainAliases/RefreshAudit",
            new Dictionary<string, string>(),
            tokenUrl: "/DomainAliases/Index");

        Assert.AreEqual(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("/DomainAliases", response.Headers.Location?.OriginalString ?? string.Empty);
        await Task.Delay(100);
        Assert.IsGreaterThan(
            initialCount,
            queue.GetAllTasks().Count(task => task.ServiceType == typeof(DnsAuditJob)));
    }

    [TestMethod]
    public async Task CloudflareTokenIsNeverRenderedBackToSettingsPage()
    {
        const string secret = "secret-token-that-must-not-be-rendered";
        await LoginAsAdmin();
        using (var scope = Server!.Services.CreateScope())
        {
            var settings = scope.ServiceProvider.GetRequiredService<GlobalSettingsService>();
            await settings.UpdateSettingAsync(SettingsMap.CloudflareApiToken, secret);

            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var storedValue = await db.GlobalSettings
                .Where(setting => setting.Key == SettingsMap.CloudflareApiToken)
                .Select(setting => setting.Value)
                .SingleAsync();
            Assert.AreNotEqual(secret, storedValue);
            Assert.StartsWith("protected:v1:", storedValue);
            Assert.AreEqual(secret, await settings.GetSettingValueAsync(SettingsMap.CloudflareApiToken));
        }

        var response = await Http.GetAsync("/GlobalSettings/Index");

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(secret, html);
        Assert.Contains(SettingsMap.CloudflareApiToken, html);
        Assert.Contains("Configured", html);
    }
    private async Task SaveAuditReportAsync(DnsAuditReport report)
    {
        // Candidate hostnames come from the persisted snapshot whenever audit history exists.
        var store = GetService<ServiceAuditStore>();
        var runId = await store.BeginRunAsync();
        await store.CompleteSuccessAsync(runId, report);
    }

}
