using Aiursoft.Canon.TaskQueue;
using Aiursoft.EmployeeCenter.Configuration;
using Aiursoft.EmployeeCenter.Models.DnsAuditViewModels;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.EmployeeCenter.Services.BackgroundJobs;
using Aiursoft.EmployeeCenter.Services.DnsAudit;

namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class DnsAuditControllerTests : TestBase
{
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

        var response = await Http.GetAsync("/DnsAudit/Index");

        Assert.AreEqual(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("/Error/Code403", response.Headers.Location?.OriginalString ?? string.Empty);
    }

    [TestMethod]
    public async Task AdminCanOpenDnsAuditWhenTokenIsNotConfigured()
    {
        await LoginAsAdmin();
        GetService<DnsAuditSnapshotCache>().SetNotConfigured(DateTime.UtcNow);

        var response = await Http.GetAsync("/DnsAudit/Index");

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("DNS Audit", html);
        Assert.Contains("Cloudflare API token is not configured", html);
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

        var response = await Http.GetAsync("/DnsAudit/Index");

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("cached-audit.example.com", html);
        Assert.Contains("Automatically refreshed every 20 minutes", html);
    }

    [TestMethod]
    public async Task UnknownDnsFindingCanBeRegisteredAsDomainAlias()
    {
        await LoginAsAdmin();
        var db = GetService<EmployeeCenterDbContext>();
        var targetService = new Service
        {
            Domain = "avigame.anduinlab.com",
            Status = ServiceStatus.Running
        };
        db.Services.Add(targetService);
        await db.SaveChangesAsync();

        GetService<DnsAuditSnapshotCache>().SetSuccess(new DnsAuditReport
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
        }, DateTime.UtcNow);

        var auditResponse = await Http.GetAsync("/DnsAudit/Index");
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
        Assert.AreEqual("https://avigame.anduinlab.com/", alias.TargetUrl);
        Assert.AreEqual(targetService.Id, alias.TargetServiceId);
    }

    [TestMethod]
    public async Task AdminCanManageAliasesFromCentralDashboard()
    {
        await LoginAsAdmin();
        var db = GetService<EmployeeCenterDbContext>();
        var targetService = new Service { Domain = "dashboard-target.example.com" };
        db.DomainAliases.Add(new DomainAlias
        {
            Domain = "legacy-dashboard.example.com",
            TargetService = targetService,
            TargetUrl = "https://dashboard-target.example.com/"
        });
        await db.SaveChangesAsync();
        GetService<DnsAuditSnapshotCache>().SetSuccess(new DnsAuditReport
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
        }, DateTime.UtcNow.AddMinutes(1));

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
        GetService<DnsAuditSnapshotCache>().SetSuccess(new DnsAuditReport
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
        }, DateTime.UtcNow);

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
        var targetService = new Service { Domain = "target.example.com" };
        db.Services.Add(targetService);
        await db.SaveChangesAsync();
        GetService<DnsAuditSnapshotCache>().SetSuccess(new DnsAuditReport
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
        }, DateTime.UtcNow);

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
            "/DnsAudit/Refresh",
            new Dictionary<string, string>(),
            tokenUrl: "/DnsAudit/Index");

        Assert.AreEqual(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("/DnsAudit", response.Headers.Location?.OriginalString ?? string.Empty);
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
}
