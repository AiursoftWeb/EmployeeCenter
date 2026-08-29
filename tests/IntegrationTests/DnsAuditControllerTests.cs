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
