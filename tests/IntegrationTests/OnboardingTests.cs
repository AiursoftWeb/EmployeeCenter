
namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class OnboardingTests
{
    private readonly int _port;
    private readonly HttpClient _http;
    private IHost? _server;

    public OnboardingTests()
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
    public async Task OnboardingWorkflowTest()
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

        // 2. Create an onboarding task
        var createTaskToken = await GetAntiCsrfToken("/ManageOnboarding/Create");
        var taskContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Order", "1" },
            { "ExpectedDurationSeconds", "60" },
            { "Title", "Welcome Task" },
            { "Description", "Please read the manual." },
            { "StartLink", "https://example.com" },
            { "__RequestVerificationToken", createTaskToken }
        });
        var createTaskResponse = await _http.PostAsync("/ManageOnboarding/Create", taskContent);
        Assert.AreEqual(HttpStatusCode.Found, createTaskResponse.StatusCode);

        // 3. Verify task in Manage page
        var manageResponse = await _http.GetAsync("/ManageOnboarding/Index");
        manageResponse.EnsureSuccessStatusCode();
        var manageHtml = await manageResponse.Content.ReadAsStringAsync();
        Assert.Contains("Welcome Task", manageHtml);

        // 4. Create a normal user
        await _http.GetAsync("/Account/LogOff");
        var uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8);
        var userName = $"user-{uniqueId}";
        var email = $"{userName}@aiursoft.com";
        var password = "Test-Password-123";

        var registerToken = await GetAntiCsrfToken("/Account/Register");
        var registerContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Email", email },
            { "Password", password },
            { "ConfirmPassword", password },
            { "__RequestVerificationToken", registerToken }
        });
        var registerResponse = await _http.PostAsync("/Account/Register", registerContent);
        Assert.AreEqual(HttpStatusCode.Found, registerResponse.StatusCode);

        // 5. Login as the normal user
        loginToken = await GetAntiCsrfToken("/Account/Login");
        loginResponse = await _http.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "EmailOrUserName", email },
            { "Password", password },
            { "__RequestVerificationToken", loginToken }
        }));
        Assert.AreEqual(HttpStatusCode.Found, loginResponse.StatusCode);

        // 6. Verify task on Dashboard
        var dashboardResponse = await _http.GetAsync("/Dashboard");
        dashboardResponse.EnsureSuccessStatusCode();
        var dashboardHtml = await dashboardResponse.Content.ReadAsStringAsync();
        Assert.Contains("Welcome Task", dashboardHtml);
        Assert.Contains("Start Task", dashboardHtml);

        // 7. Start the task
        int taskId;
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var task = await db.OnboardingTasks.FirstAsync(t => t.Title == "Welcome Task");
            taskId = task.Id;
        }

        var startTaskToken = await GetAntiCsrfToken("/Dashboard");
        var startTaskContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "__RequestVerificationToken", startTaskToken }
        });
        var startTaskResponse = await _http.PostAsync($"/Onboarding/StartTask/{taskId}", startTaskContent);
        Assert.AreEqual(HttpStatusCode.Found, startTaskResponse.StatusCode);
        Assert.AreEqual("https://example.com/", startTaskResponse.Headers.Location?.ToString());

        // 8. Verify task is in progress on Dashboard
        dashboardResponse = await _http.GetAsync("/Dashboard");
        dashboardResponse.EnsureSuccessStatusCode();
        dashboardHtml = await dashboardResponse.Content.ReadAsStringAsync();
        Assert.Contains("In Progress", dashboardHtml);

        // 9. Try to complete the task immediately
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var log = await db.OnboardingTaskLogs.FirstAsync(l => l.TaskId == taskId && l.CompletionTime == null);
            log.StartTime = DateTime.UtcNow.AddSeconds(-31);
            await db.SaveChangesAsync();
        }

        // 10. Complete the task
        var completeTaskToken = await GetAntiCsrfToken("/Dashboard");
        var completeTaskContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "__RequestVerificationToken", completeTaskToken }
        });
        var completeTaskResponse = await _http.PostAsync($"/Onboarding/CompleteTask/{taskId}", completeTaskContent);
        Assert.AreEqual(HttpStatusCode.Found, completeTaskResponse.StatusCode);

        // 11. Verify task is completed on Dashboard
        dashboardResponse = await _http.GetAsync("/Dashboard");
        dashboardResponse.EnsureSuccessStatusCode();
        dashboardHtml = await dashboardResponse.Content.ReadAsStringAsync();
        Assert.Contains("Completed at", dashboardHtml);
        Assert.Contains("You have completed all onboarding tasks", dashboardHtml);
        Assert.Contains("Certificate", dashboardHtml);
        Assert.Contains("Wish your career takes off at Aiursoft!", dashboardHtml);

        // 12. Verify logo and brand name
        Assert.Contains("logo.svg", dashboardHtml);
        Assert.Contains("alt=\"Aiursoft\"", dashboardHtml);

        // 13. Update logo and brand name in settings
        // Login as admin again
        loginToken = await GetAntiCsrfToken("/Account/Login");
        loginContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "EmailOrUserName", "admin" },
            { "Password", "Admin@123456!" },
            { "__RequestVerificationToken", loginToken }
        });
        await _http.PostAsync("/Account/Login", loginContent);

        // Update Brand Name
        var editBrandToken = await GetAntiCsrfToken("/GlobalSettings/Index");
        var editBrandContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Key", "BrandName" },
            { "Value", "NewBrand" },
            { "__RequestVerificationToken", editBrandToken }
        });
        var editBrandResponse = await _http.PostAsync("/GlobalSettings/Edit", editBrandContent);
        Assert.AreEqual(HttpStatusCode.Found, editBrandResponse.StatusCode);

        // Create a dummy logo file to pass validation
        var logoFileName = "custom-logo.png";
        using (var scope = _server!.Services.CreateScope())
        {
            var storage = scope.ServiceProvider.GetRequiredService<Services.FileStorage.StorageService>();
            var physicalPath = storage.GetFilePhysicalPath(logoFileName);
            var dir = Path.GetDirectoryName(physicalPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir!);
            await File.WriteAllTextAsync(physicalPath, "fake image content");
        }

        // Update Logo
        var editLogoToken = await GetAntiCsrfToken("/GlobalSettings/Index");
        var editLogoContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Key", "ProjectLogo" },
            { "Value", logoFileName },
            { "__RequestVerificationToken", editLogoToken }
        });
        var editLogoResponse = await _http.PostAsync("/GlobalSettings/Edit", editLogoContent);
        Assert.AreEqual(HttpStatusCode.Found, editLogoResponse.StatusCode);

        // 14. Verify updated logo and brand name on Dashboard as the user
        await _http.GetAsync("/Account/LogOff");
        loginToken = await GetAntiCsrfToken("/Account/Login");
        await _http.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "EmailOrUserName", email },
            { "Password", password },
            { "__RequestVerificationToken", loginToken }
        }));

        dashboardResponse = await _http.GetAsync("/Dashboard");
        dashboardResponse.EnsureSuccessStatusCode();
        dashboardHtml = await dashboardResponse.Content.ReadAsStringAsync();
        Assert.Contains(logoFileName, dashboardHtml);
        Assert.Contains("alt=\"NewBrand\"", dashboardHtml);
    }
}
