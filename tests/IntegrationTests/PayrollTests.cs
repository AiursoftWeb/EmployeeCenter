
namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class PayrollTests
{
    private readonly int _port;
    private readonly HttpClient _http;
    private IHost? _server;

    public PayrollTests()
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
        // Use a simpler regex that doesn't depend on many quotes
        var match = Regex.Match(html, @"__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""");
        if (!match.Success)
        {
            throw new InvalidOperationException($"Could not find anti-CSRF token on page: {url}");
        }

        return match.Groups[1].Value;
    }

    [TestMethod]
    public async Task ManagePayrollTest()
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

        // 2. Create a normal user
        var uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8);
        var userName = $"user-{uniqueId}";
        var email = $"{userName}@aiursoft.com";
        var password = "Test-Password-123";

        // Log off admin first to register
        await _http.GetAsync("/Account/LogOff");

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

        // 3. Get User ID from DB
        string userId;
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var user = await db.Users.FirstOrDefaultAsync(u => u.UserName == userName);
            Assert.IsNotNull(user);
            userId = user.Id;
        }

        // 4. Log in as admin again to issue payroll
        await _http.GetAsync("/Account/LogOff");
        loginToken = await GetAntiCsrfToken("/Account/Login");
        await _http.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "EmailOrUserName", "admin" },
            { "Password", "Admin@123456!" },
            { "__RequestVerificationToken", loginToken }
        }));

        var createPayrollToken = await GetAntiCsrfToken("/ManagePayroll/Create");
        var payrollContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "UserId", userId },
            { "TargetMonth", "2025-12" },
            { "Currency", "USD" },
            { "TotalAmount", "5000" },
            { "Content", "# December Payroll\nBase: 4000\nBonus: 1000" },
            { "__RequestVerificationToken", createPayrollToken }
        });
        var createPayrollResponse = await _http.PostAsync("/ManagePayroll/Create", payrollContent);
        Assert.AreEqual(HttpStatusCode.Found, createPayrollResponse.StatusCode);

        // 5. Verify payroll in Manage page
        var manageResponse = await _http.GetAsync("/ManagePayroll/Index");
        manageResponse.EnsureSuccessStatusCode();
        var manageHtml = await manageResponse.Content.ReadAsStringAsync();
        Assert.Contains(userName, manageHtml);

        // 6. Log in as the normal user and verify they can see their payroll
        await _http.GetAsync("/Account/LogOff");

        loginToken = await GetAntiCsrfToken("/Account/Login");
        loginResponse = await _http.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "EmailOrUserName", email },
            { "Password", password },
            { "__RequestVerificationToken", loginToken }
        }));
        Assert.AreEqual(HttpStatusCode.Found, loginResponse.StatusCode);

        var myPayrollsResponse = await _http.GetAsync("/Payroll/Index");
        myPayrollsResponse.EnsureSuccessStatusCode();
        var myPayrollsHtml = await myPayrollsResponse.Content.ReadAsStringAsync();
        Assert.Contains("2025-12", myPayrollsHtml);
        Assert.Contains("$5,000.00", myPayrollsHtml);

        // 7. View details
        int payrollId;
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var payroll = await db.Payrolls.FirstAsync(p => p.OwnerId == userId);
            payrollId = payroll.Id;
            Assert.AreEqual("USD", payroll.Currency);
        }

        var detailsResponse = await _http.GetAsync("/Payroll/Details/" + payrollId);
        detailsResponse.EnsureSuccessStatusCode();
        var detailsHtml = await detailsResponse.Content.ReadAsStringAsync();
        Assert.Contains("December Payroll", detailsHtml);
        Assert.Contains("$5,000.00", detailsHtml);

        // 8. Log in as admin again to export payroll
        await _http.GetAsync("/Account/LogOff");
        loginToken = await GetAntiCsrfToken("/Account/Login");
        await _http.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "EmailOrUserName", "admin" },
            { "Password", "Admin@123456!" },
            { "__RequestVerificationToken", loginToken }
        }));

        var exportResponse = await _http.GetAsync($"/ManagePayroll/Export?userId={userId}&year=2025");
        exportResponse.EnsureSuccessStatusCode();
        Assert.AreEqual("text/csv", exportResponse.Content.Headers.ContentType?.MediaType);
        var csvContent = await exportResponse.Content.ReadAsStringAsync();
        Assert.Contains(userId, csvContent);
        Assert.Contains("5000", csvContent);
        Assert.Contains("USD", csvContent);
        Assert.Contains("December Payroll", csvContent);

        // 9. Edit payroll
        var editPayrollToken = await GetAntiCsrfToken("/ManagePayroll/Edit/" + payrollId);
        var editPayrollContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Id", payrollId.ToString() },
            { "UserId", userId },
            { "TargetMonth", "2026-01" },
            { "Currency", "USD" },
            { "TotalAmount", "6000" },
            { "Content", "# January Payroll Updated\nBase: 5000\nBonus: 1000" },
            { "__RequestVerificationToken", editPayrollToken }
        });
        var editPayrollResponse = await _http.PostAsync("/ManagePayroll/Edit", editPayrollContent);
        Assert.AreEqual(HttpStatusCode.Found, editPayrollResponse.StatusCode);

        // 10. Verify edited payroll
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var payroll = await db.Payrolls.FirstAsync(p => p.Id == payrollId);
            Assert.AreEqual("2026-01", payroll.TargetMonth.ToString("yyyy-MM"));
            Assert.AreEqual(6000m, payroll.TotalAmount);
            Assert.AreEqual("# January Payroll Updated\nBase: 5000\nBonus: 1000", payroll.Content);
        }
    }
}
