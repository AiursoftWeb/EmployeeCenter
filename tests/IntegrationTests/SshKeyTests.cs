
namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class SshKeyTests
{
    private readonly int _port;
    private readonly HttpClient _http;
    private IHost? _server;

    public SshKeyTests()
    {
        var cookieContainer = new CookieContainer();
        var handler = new HttpClientHandler
        {
            CookieContainer = cookieContainer,
            AllowAutoRedirect = false
        };
        _port = Network.GetAvailablePort();
        _http = new HttpClient(handler)
        {
            BaseAddress = new Uri($"http://localhost:{_port}")
        };
    }

    [TestInitialize]
    public async Task CreateServer()
    {
        _server = await AppAsync<Startup>([], port: _port);
        await _server.UpdateDbAsync<EmployeeCenterDbContext>();
        await _server.SeedAsync();
        await _server.StartAsync();
    }

    [TestCleanup]
    public async Task CleanServer()
    {
        if (_server == null) return;
        await _server.StopAsync();
        _server.Dispose();
    }

    private async Task<string> GetAntiCsrfToken(string url)
    {
        var response = await _http.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(html, @"__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""");
        if (!match.Success)
        {
            throw new InvalidOperationException($"Could not find anti-CSRF token on page: {url}");
        }

        return match.Groups[1].Value;
    }

    [TestMethod]
    public async Task SshKeyManagementTest()
    {
        // 1. Login as admin
        var loginToken = await GetAntiCsrfToken("/Account/Login");
        var loginContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "EmailOrUserName", "admin" },
            { "Password", "Admin@123456!" },
            { "__RequestVerificationToken", loginToken }
        });
        var loginResponse = await _http.PostAsync("/Account/Login", loginContent);
        Assert.AreEqual(HttpStatusCode.Found, loginResponse.StatusCode);

        // 2. Add an SSH Key for admin
        string adminId;
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var admin = await db.Users.FirstAsync(u => u.UserName == "admin");
            adminId = admin.Id;
        }

        var createToken = await GetAntiCsrfToken("/SshKeys/Create?userId=" + adminId);
        var createContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "TargetUserId", adminId },
            { "Name", "Admin Key" },
            { "PublicKey", "ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABAQC..." },
            { "__RequestVerificationToken", createToken }
        });

        var createResponse = await _http.PostAsync("/SshKeys/Create", createContent);
        Assert.AreEqual(HttpStatusCode.Found, createResponse.StatusCode);

        // 3. Verify key exists
        var indexResponse = await _http.GetAsync("/SshKeys/Index");
        indexResponse.EnsureSuccessStatusCode();
        var indexHtml = await indexResponse.Content.ReadAsStringAsync();
        Assert.Contains("Admin Key", indexHtml);

        // 4. Edit the key
        int keyId;
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var key = await db.SshKeys.FirstAsync(k => k.Name == "Admin Key");
            keyId = key.Id;
        }

        var editToken = await GetAntiCsrfToken($"/SshKeys/Edit/{keyId}");
        var editContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Id", keyId.ToString() },
            { "TargetUserId", adminId },
            { "Name", "Updated Admin Key" },
            { "PublicKey", "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAI..." },
            { "__RequestVerificationToken", editToken }
        });

        var editResponse = await _http.PostAsync("/SshKeys/Edit", editContent);
        Assert.AreEqual(HttpStatusCode.Found, editResponse.StatusCode);

        indexResponse = await _http.GetAsync("/SshKeys/Index");
        indexHtml = await indexResponse.Content.ReadAsStringAsync();
        Assert.Contains("Updated Admin Key", indexHtml);

        // 5. Delete the key
        var deleteToken = await GetAntiCsrfToken("/SshKeys/Index");
        var deleteResponse = await _http.PostAsync($"/SshKeys/Delete/{keyId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "__RequestVerificationToken", deleteToken }
        }));
        Assert.AreEqual(HttpStatusCode.Found, deleteResponse.StatusCode);

        indexResponse = await _http.GetAsync("/SshKeys/Index");
        indexHtml = await indexResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Updated Admin Key", indexHtml);
    }
}
