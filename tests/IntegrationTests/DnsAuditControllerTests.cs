using Aiursoft.EmployeeCenter.Configuration;
using Aiursoft.EmployeeCenter.Services;

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

        var response = await Http.GetAsync("/DnsAudit/Index");

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("DNS Audit", html);
        Assert.Contains("Cloudflare API token is not configured", html);
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
