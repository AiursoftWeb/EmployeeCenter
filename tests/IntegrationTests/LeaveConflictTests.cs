namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class LeaveConflictTests
{
    private readonly int _port;
    private readonly HttpClient _http;
    private IHost? _server;

    public LeaveConflictTests()
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

        if (response.StatusCode == HttpStatusCode.Found)
        {
            var redirectUrl = response.Headers.Location;
            response = await _http.GetAsync(redirectUrl);
        }

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(html,
            @"<input name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)"" />");
        if (!match.Success)
        {
            throw new InvalidOperationException($"Could not find anti-CSRF token on page: {url}");
        }

        return match.Groups[1].Value;
    }

    [TestMethod]
    public async Task CannotApplyForOverlappingLeaveTest()
    {
        var email = $"test-{Guid.NewGuid()}@aiursoft.com";
        var password = "Test-Password-123";

        // 1. Register and Login
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

        // 2. Initialize Allocation (Visit Index)
        await _http.GetAsync("/Leave/Index");

        // 3. Apply for first leave (e.g., next Monday to next Friday)
        var startDate1 = DateTime.UtcNow.Date.AddDays(7);
        while (startDate1.DayOfWeek != DayOfWeek.Monday) startDate1 = startDate1.AddDays(1);
        var endDate1 = startDate1.AddDays(4); // Friday

        var applyToken = await GetAntiCsrfToken("/Leave/Apply");
        var applyContent1 = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "LeaveType", "AnnualLeave" },
            { "StartDate", startDate1.ToString("yyyy-MM-dd") },
            { "EndDate", endDate1.ToString("yyyy-MM-dd") },
            { "Reason", "First Vacation" },
            { "__RequestVerificationToken", applyToken }
        });
        var response1 = await _http.PostAsync("/Leave/Apply", applyContent1);
        Assert.AreEqual(HttpStatusCode.Found, response1.StatusCode);

        // 4. Apply for overlapping leave (e.g., Tuesday to Thursday of the same week)
        var startDate2 = startDate1.AddDays(1);
        var endDate2 = startDate1.AddDays(3);

        applyToken = await GetAntiCsrfToken("/Leave/Apply");
        var applyContent2 = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "LeaveType", "AnnualLeave" },
            { "StartDate", startDate2.ToString("yyyy-MM-dd") },
            { "EndDate", endDate2.ToString("yyyy-MM-dd") },
            { "Reason", "Overlapping Vacation" },
            { "__RequestVerificationToken", applyToken }
        });

        var response2 = await _http.PostAsync("/Leave/Apply", applyContent2);

        // 5. Assert Failure
        Assert.AreEqual(HttpStatusCode.OK, response2.StatusCode, "Overlapping leave application should be rejected (Validation Error).");
    }
}
