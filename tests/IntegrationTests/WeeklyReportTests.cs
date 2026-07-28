
using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Configuration;

namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class WeeklyReportTests
{
    private readonly int _port;
    private readonly HttpClient _http;
    private IHost? _server;

    public WeeklyReportTests()
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
        // Redirects to login are possible if not authenticated, but Register shouldn't.
        if (response.StatusCode == HttpStatusCode.Found)
        {
            // Follow redirect once if needed, or assume caller handles it.
            // For Register/Login pages, usually 200.
        }

        var html = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(html,
            @"<input name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)"" />");
        if (!match.Success)
        {
            // If we are redirected to login, maybe we are unauthorized.
            // But usually we call this on a page we expect to see form.
            // throw new InvalidOperationException($"Could not find anti-CSRF token on page: {url}. Status: {response.StatusCode}");
            // Return empty or let it fail later.
        }
        return match.Groups[1].Value;
    }

    private async Task<string> GetUserIdByEmail(string email)
    {
        using var scope = _server!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
        var user = await db.Users.FirstAsync(u => u.Email == email);
        return user.Id;
    }

    private async Task GrantPermission(string userId, string permission)
    {
        using var scope = _server!.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        var roleName = "TestRole_" + permission;
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            var role = new IdentityRole(roleName);
            await roleManager.CreateAsync(role);
            await roleManager.AddClaimAsync(role, new Claim(AppPermissions.Type, permission));
        }

        var user = await userManager.FindByIdAsync(userId);
        await userManager.AddToRoleAsync(user!, roleName);
    }

    private async Task LoginAs(string username, string password)
    {
        await _http.GetAsync("/Account/LogOff");
        var loginToken = await GetAntiCsrfToken("/Account/Login");
        var loginContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "EmailOrUserName", username },
            { "Password", password },
            { "__RequestVerificationToken", loginToken }
        });
        var loginResponse = await _http.PostAsync("/Account/Login", loginContent);
        if (loginResponse.StatusCode != HttpStatusCode.Found)
        {
            // Fallback: maybe already logged in or error?
            // But usually expects redirect to ReturnUrl or Home
        }
    }

    [TestMethod]
    public async Task WeeklyReportWorkflowTest()
    {
        var email = $"test-{Guid.NewGuid()}@aiursoft.com";
        var password = "Test-Password-123";

        // 1. Register
        var registerToken = await GetAntiCsrfToken("/Account/Register");
        var registerContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Email", email },
            { "Password", password },
            { "ConfirmPassword", password },
            { "__RequestVerificationToken", registerToken }
        });
        await _http.PostAsync("/Account/Register", registerContent);

        // Grant Permission
        var userId = await GetUserIdByEmail(email);
        await GrantPermission(userId, AppPermissionNames.CanCreateWeeklyReport);

        // Re-Login to refresh claims
        await LoginAs(email, password);

        // 2. Visit Weekly Report Index
        var indexResponse = await _http.GetAsync("/WeeklyReport/Index");
        Assert.AreEqual(HttpStatusCode.OK, indexResponse.StatusCode);
        var indexHtml = await indexResponse.Content.ReadAsStringAsync();

        // Should show "New Weekly Report"
        Assert.Contains("New Weekly Report", indexHtml);
        // Assert.IsFalse(indexHtml.Contains("New Weekly Report"));


        // 3. Submit Report for Current Week
        // We need to pick a valid Sunday. 
        // The form defaults to current week start.
        var now = DateTime.UtcNow;
        var offset = (int)now.DayOfWeek;
        var thisWeekStart = now.AddDays(-offset).Date;
        var weekStartStr = thisWeekStart.ToString("yyyy-MM-dd");

        var createToken = await GetAntiCsrfToken("/WeeklyReport/Index");
        var createContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "content", "UNIQUE_REPORT_THIS_WEEK" },
            { "weekStartDate", weekStartStr },
            { "__RequestVerificationToken", createToken }
        });

        var createResponse = await _http.PostAsync("/WeeklyReport/Create", createContent);
        Assert.AreEqual(HttpStatusCode.Found, createResponse.StatusCode); // Redirect to Index

        // 4. Verify in DB
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var report = await db.WeeklyReports.FirstOrDefaultAsync(r => r.UserId == userId);
            Assert.IsNotNull(report);
            Assert.AreEqual("UNIQUE_REPORT_THIS_WEEK", report.Content);
            Assert.AreEqual(thisWeekStart, report.WeekStartDate);
        }

        // 5. Try to submit AGAIN for same week
        // Note: The UI might hide it, but we can POST manually to test backend validation.
        createContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "content", "Duplicate report attempt" },
            { "weekStartDate", weekStartStr },
            { "__RequestVerificationToken", createToken }
        });
        createResponse = await _http.PostAsync("/WeeklyReport/Create", createContent);
        Assert.AreEqual(HttpStatusCode.Found, createResponse.StatusCode);
        // Logic says: if exists, it should append.

        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var reports = await db.WeeklyReports.Where(r => r.UserId == userId).ToListAsync();
            Assert.HasCount(1, reports); // Still 1
            Assert.AreEqual("UNIQUE_REPORT_THIS_WEEK\r\n\r\nDuplicate report attempt", reports[0].Content);
        }

        // 6. Submit for Past Week
        var pastWeekStart = thisWeekStart.AddDays(-7);
        var pastWeekStr = pastWeekStart.ToString("yyyy-MM-dd");

        createContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "content", "UNIQUE_REPORT_PAST_WEEK" },
            { "weekStartDate", pastWeekStr },
            { "__RequestVerificationToken", createToken }
        });
        createResponse = await _http.PostAsync("/WeeklyReport/Create", createContent);
        Assert.AreEqual(HttpStatusCode.Found, createResponse.StatusCode);

        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var reports = await db.WeeklyReports.Where(r => r.UserId == userId).OrderBy(r => r.WeekStartDate).ToListAsync();
            Assert.HasCount(2, reports);
            Assert.AreEqual(pastWeekStart, reports[0].WeekStartDate);
            Assert.AreEqual("UNIQUE_REPORT_PAST_WEEK", reports[0].Content);
            Assert.AreEqual(thisWeekStart, reports[1].WeekStartDate);
        }

        // 7. Verify sorting in Index
        var sortedResponse = await _http.GetAsync("/WeeklyReport/Index");
        var sortedHtml = await sortedResponse.Content.ReadAsStringAsync();

        // Find indices of the reports in the HTML
        var indexFirst = sortedHtml.IndexOf("UNIQUE_REPORT_THIS_WEEK", StringComparison.Ordinal);
        var indexPast = sortedHtml.IndexOf("UNIQUE_REPORT_PAST_WEEK", StringComparison.Ordinal);

        Assert.AreNotEqual(-1, indexFirst, "Could not find 'UNIQUE_REPORT_THIS_WEEK' in index");
        Assert.AreNotEqual(-1, indexPast, "Could not find 'UNIQUE_REPORT_PAST_WEEK' in index");

        // Since we sort by WeekStartDate DESC, "UNIQUE_REPORT_THIS_WEEK" (this week) should appear BEFORE "UNIQUE_REPORT_PAST_WEEK" (past week)
        Assert.IsLessThan(indexPast, indexFirst, "Reports are not sorted correctly by WeekStartDate DESC");

        // 8. Verify sorting with same WeekStartDate but different CreateTime
        var createToken2 = await GetAntiCsrfToken("/WeeklyReport/Index");
        // Create another user to post in the same week
        var otherEmail = $"sorting-test-{Guid.NewGuid()}@aiursoft.com";
        var registerTokenOther = await GetAntiCsrfToken("/Account/Register");
        var registerContentOther = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Email", otherEmail },
            { "Password", password },
            { "ConfirmPassword", password },
            { "__RequestVerificationToken", registerTokenOther }
        });
        await _http.PostAsync("/Account/Register", registerContentOther);
        var otherUserId = await GetUserIdByEmail(otherEmail);
        await GrantPermission(otherUserId, AppPermissionNames.CanCreateWeeklyReport);
        await LoginAs(otherEmail, password);

        var createContent2 = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "content", "UNIQUE_REPORT_OTHER_USER_SAME_WEEK" },
            { "weekStartDate", weekStartStr },
            { "__RequestVerificationToken", createToken2 }
        });
        await _http.PostAsync("/WeeklyReport/Create", createContent2);

        var finalResponse = await _http.GetAsync("/WeeklyReport/Index");
        var finalHtml = await finalResponse.Content.ReadAsStringAsync();

        var indexOther = finalHtml.IndexOf("UNIQUE_REPORT_OTHER_USER_SAME_WEEK", StringComparison.Ordinal);
        indexFirst = finalHtml.IndexOf("UNIQUE_REPORT_THIS_WEEK", StringComparison.Ordinal);

        // Both are this week. "Other user report same week" was created LATER, so it should be ABOVE "My first report"
        // Order: WeekStartDate DESC (same), then CreateTime DESC
        Assert.IsLessThan(indexFirst, indexOther, "Reports are not sorted correctly by CreateTime DESC within same week");
    }

    [TestMethod]
    public async Task TestWeeklyReportMissingStatus()
    {
        var email = $"test-status-{Guid.NewGuid()}@aiursoft.com";
        var password = "Test-Password-123";

        // 1. Register and Grant Permission
        var registerToken = await GetAntiCsrfToken("/Account/Register");
        var registerContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Email", email },
            { "Password", password },
            { "ConfirmPassword", password },
            { "__RequestVerificationToken", registerToken }
        });
        await _http.PostAsync("/Account/Register", registerContent);
        var userId = await GetUserIdByEmail(email);
        await GrantPermission(userId, AppPermissionNames.CanCreateWeeklyReport);
        await LoginAs(email, password);

        // 2. Initially, should be critical (Red) as last 4 weeks are missing
        var indexResponse = await _http.GetAsync("/WeeklyReport/Index");
        var indexHtml = await indexResponse.Content.ReadAsStringAsync();
        Assert.Contains("Extremely Dangerous!", indexHtml);
        Assert.Contains("You haven't submitted any reports in the last 4 weeks!", indexHtml);

        // 3. Submit one report (this week)
        var now = DateTime.UtcNow;
        var offset = (int)now.DayOfWeek;
        var thisWeekStart = now.AddDays(-offset).Date;
        var weekStartStr = thisWeekStart.ToString("yyyy-MM-dd");

        var createToken = await GetAntiCsrfToken("/WeeklyReport/Index");
        var createContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "content", "Report for this week" },
            { "weekStartDate", weekStartStr },
            { "__RequestVerificationToken", createToken }
        });
        await _http.PostAsync("/WeeklyReport/Create", createContent);

        // 4. Now should be Warning (Yellow) as 3 weeks are still missing
        indexResponse = await _http.GetAsync("/WeeklyReport/Index");
        indexHtml = await indexResponse.Content.ReadAsStringAsync();
        Assert.Contains("Incomplete Submission", indexHtml);
        Assert.Contains("You have 3 missing report(s) in the last 4 weeks.", indexHtml);

        // 5. Submit 3 more reports for past weeks
        for (int i = 1; i <= 3; i++)
        {
            var pastWeekStart = thisWeekStart.AddDays(-i * 7);
            var pastWeekStr = pastWeekStart.ToString("yyyy-MM-dd");
            createContent = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "content", $"Report for week -{i}" },
                { "weekStartDate", pastWeekStr },
                { "__RequestVerificationToken", createToken }
            });
            await _http.PostAsync("/WeeklyReport/Create", createContent);
        }

        // 6. Now should be Success (Green)
        indexResponse = await _http.GetAsync("/WeeklyReport/Index");
        indexHtml = await indexResponse.Content.ReadAsStringAsync();
        Assert.Contains("Everything is up to date!", indexHtml);
        Assert.Contains("Your reports for the last 4 weeks are all submitted.", indexHtml);
    }

    [TestMethod]
    public async Task TestEditWeeklyReport()
    {
        var email = $"test-edit-{Guid.NewGuid()}@aiursoft.com";
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
        await _http.PostAsync("/Account/Register", registerContent);
        var userId = await GetUserIdByEmail(email);
        await GrantPermission(userId, AppPermissionNames.CanCreateWeeklyReport);
        await LoginAs(email, password);

        // 2. Submit a report
        var createToken = await GetAntiCsrfToken("/WeeklyReport/Index");
        var createContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "content", "Original Content" },
            { "weekStartDate", DateTime.UtcNow.AddDays(-(int)DateTime.UtcNow.DayOfWeek).ToString("yyyy-MM-dd") },
            { "__RequestVerificationToken", createToken }
        });
        await _http.PostAsync("/WeeklyReport/Create", createContent);

        int reportId;
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var report = await db.WeeklyReports.FirstAsync(r => r.UserId == userId);
            reportId = report.Id;
        }

        // 3. Edit the report
        var editToken = await GetAntiCsrfToken($"/WeeklyReport/Edit/{reportId}");
        var editContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Id", reportId.ToString() },
            { "Content", "Updated Content" },
            { "__RequestVerificationToken", editToken }
        });
        var editResponse = await _http.PostAsync("/WeeklyReport/Edit", editContent);
        Assert.AreEqual(HttpStatusCode.Found, editResponse.StatusCode);

        // 4. Verify in DB
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var report = await db.WeeklyReports.FirstAsync(r => r.Id == reportId);
            Assert.AreEqual("Updated Content", report.Content);
        }

        // 5. Try to edit someone else's report
        var otherEmail = $"other-{Guid.NewGuid()}@aiursoft.com";
        var registerTokenOther = await GetAntiCsrfToken("/Account/Register");
        var registerContentOther = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Email", otherEmail },
            { "Password", password },
            { "ConfirmPassword", password },
            { "__RequestVerificationToken", registerTokenOther }
        });
        await _http.PostAsync("/Account/Register", registerContentOther);

        var otherId = await GetUserIdByEmail(otherEmail);
        await GrantPermission(otherId, AppPermissionNames.CanCreateWeeklyReport); // Grant permission so we can get token from Index

        await LoginAs(otherEmail, password);

        var editGetResponseOther = await _http.GetAsync($"/WeeklyReport/Edit/{reportId}");
        Assert.AreEqual(HttpStatusCode.Unauthorized, editGetResponseOther.StatusCode);

        var tokenForOther = await GetAntiCsrfToken("/WeeklyReport/Index");
        var editContentOther = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Id", reportId.ToString() },
            { "Content", "Try Update Other's Content" },
            { "__RequestVerificationToken", tokenForOther }
        });
        var editResponseOther = await _http.PostAsync("/WeeklyReport/Edit", editContentOther);
        Assert.AreEqual(HttpStatusCode.Unauthorized, editResponseOther.StatusCode);

        // 6. Try to edit a report older than 4 weeks
        await LoginAs(email, password);
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var oldReport = new WeeklyReport
            {
                UserId = userId,
                Content = "Old Content",
                CreateTime = DateTime.UtcNow.AddDays(-30),
                WeekStartDate = DateTime.UtcNow.AddDays(-35)
            };
            db.WeeklyReports.Add(oldReport);
            await db.SaveChangesAsync();
            reportId = oldReport.Id;
        }

        var editTokenOld = await GetAntiCsrfToken($"/WeeklyReport/Edit/{reportId}");
        var oldEditContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Id", reportId.ToString() },
            { "Content", "Try Update Old Content" },
            { "__RequestVerificationToken", editTokenOld }
        });
        var oldEditResponse = await _http.PostAsync("/WeeklyReport/Edit", oldEditContent);
        Assert.AreEqual(HttpStatusCode.BadRequest, oldEditResponse.StatusCode);
    }

    [TestMethod]
    public async Task TestManageAnyoneWeeklyReport()
    {
        var adminEmail = $"admin-{Guid.NewGuid()}@aiursoft.com";
        var userEmail = $"user-{Guid.NewGuid()}@aiursoft.com";
        var password = "Test-Password-123";

        // 1. Register Admin and User
        var registerToken = await GetAntiCsrfToken("/Account/Register");
        var registerContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Email", adminEmail },
            { "Password", password },
            { "ConfirmPassword", password },
            { "__RequestVerificationToken", registerToken }
        });
        await _http.PostAsync("/Account/Register", registerContent);
        var adminId = await GetUserIdByEmail(adminEmail);

        registerToken = await GetAntiCsrfToken("/Account/Register");
        registerContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Email", userEmail },
            { "Password", password },
            { "ConfirmPassword", password },
            { "__RequestVerificationToken", registerToken }
        });
        await _http.PostAsync("/Account/Register", registerContent);
        var userId = await GetUserIdByEmail(userEmail);

        // 2. Grant CanManageAnyoneWeeklyReport to Admin
        await GrantPermission(adminId, AppPermissionNames.CanManageAnyoneWeeklyReport);
        await LoginAs(adminEmail, password);

        // 3. Admin creates report on behalf of User
        var now = DateTime.UtcNow;
        var offset = (int)now.DayOfWeek;
        var thisWeekStart = now.AddDays(-offset).Date;
        var weekStartStr = thisWeekStart.ToString("yyyy-MM-dd");

        var createToken = await GetAntiCsrfToken("/WeeklyReport/Index");
        var createContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "content", "Admin created for user" },
            { "onBehalfOf", userId },
            { "weekStartDate", weekStartStr },
            { "__RequestVerificationToken", createToken }
        });
        var createResponse = await _http.PostAsync("/WeeklyReport/Create", createContent);
        Assert.AreEqual(HttpStatusCode.Found, createResponse.StatusCode);

        int reportId;
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var report = await db.WeeklyReports.FirstAsync(r => r.UserId == userId);
            Assert.AreEqual("Admin created for user", report.Content);
            reportId = report.Id;
        }

        // 4. Admin edits User's report
        var editToken = await GetAntiCsrfToken($"/WeeklyReport/Edit/{reportId}");
        var editContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Id", reportId.ToString() },
            { "Content", "Admin updated user report" },
            { "__RequestVerificationToken", editToken }
        });
        var editResponse = await _http.PostAsync("/WeeklyReport/Edit", editContent);
        Assert.AreEqual(HttpStatusCode.Found, editResponse.StatusCode);

        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var report = await db.WeeklyReports.FirstAsync(r => r.Id == reportId);
            Assert.AreEqual("Admin updated user report", report.Content);
        }

        // 5. Admin edits User's report older than 4 weeks (Bypass limit)
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var report = await db.WeeklyReports.FirstAsync(r => r.Id == reportId);
            db.Entry(report).Property(r => r.CreateTime).CurrentValue = DateTime.UtcNow.AddDays(-30);
            await db.SaveChangesAsync();
        }

        var editTokenOld = await GetAntiCsrfToken($"/WeeklyReport/Edit/{reportId}");
        var oldEditContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Id", reportId.ToString() },
            { "Content", "Admin updated OLD user report" },
            { "__RequestVerificationToken", editTokenOld }
        });
        var oldEditResponse = await _http.PostAsync("/WeeklyReport/Edit", oldEditContent);
        Assert.AreEqual(HttpStatusCode.Found, oldEditResponse.StatusCode);

        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var report = await db.WeeklyReports.FirstAsync(r => r.Id == reportId);
            Assert.AreEqual("Admin updated OLD user report", report.Content);
        }

        // 6. Admin deletes User's report
        var deleteToken = await GetAntiCsrfToken("/WeeklyReport/Index");
        var deleteContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "id", reportId.ToString() },
            { "__RequestVerificationToken", deleteToken }
        });
        // We use the Delete action we just created
        var deleteResponse = await _http.PostAsync($"/WeeklyReport/Delete/{reportId}", deleteContent);
        Assert.AreEqual(HttpStatusCode.Found, deleteResponse.StatusCode);

        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var report = await db.WeeklyReports.FirstOrDefaultAsync(r => r.Id == reportId);
            Assert.IsNull(report);
        }
    }

    [TestMethod]
    public async Task TestAdminSeesAllWeeksEvenIfSubmitted()
    {
        var adminEmail = "admin-all-" + Guid.NewGuid() + "@aiursoft.com";
        var password = "Test-Password-123";

        // 1. Register Admin
        var registerToken = await GetAntiCsrfToken("/Account/Register");
        var registerContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Email", adminEmail },
            { "Password", password },
            { "ConfirmPassword", password },
            { "__RequestVerificationToken", registerToken }
        });
        await _http.PostAsync("/Account/Register", registerContent);

        var adminId = await GetUserIdByEmail(adminEmail);
        await GrantPermission(adminId, AppPermissionNames.CanCreateWeeklyReport);
        await GrantPermission(adminId, AppPermissionNames.CanManageAnyoneWeeklyReport);

        // 2. Admin submits for themselves for current week
        await LoginAs(adminEmail, password);
        var now = DateTime.UtcNow;
        var offset = (int)now.DayOfWeek;
        var thisWeekStart = now.AddDays(-offset).Date;
        var weekStartStr = thisWeekStart.ToString("yyyy-MM-dd");

        var createToken = await GetAntiCsrfToken("/WeeklyReport/Index");
        var createContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "content", "Admin's own report" },
            { "weekStartDate", weekStartStr },
            { "__RequestVerificationToken", createToken }
        });
        await _http.PostAsync("/WeeklyReport/Create", createContent);

        // 3. Admin checks their own available weeks (via Index)
        var indexResponse = await _http.GetAsync("/WeeklyReport/Index");
        var indexHtml = await indexResponse.Content.ReadAsStringAsync();

        // Current week SHOULD be available for admin because they have CanManageAnyoneWeeklyReport permission
        Assert.Contains("value=\"" + weekStartStr + "\"", indexHtml);
    }

    [TestMethod]
    public async Task TestAdminCanAppendReport()
    {
        var adminEmail = "admin-append-" + Guid.NewGuid() + "@aiursoft.com";
        var userEmail = "user-to-append-" + Guid.NewGuid() + "@aiursoft.com";
        var password = "Test-Password-123";

        // 1. Register Admin and User
        var registerToken = await GetAntiCsrfToken("/Account/Register");
        foreach (var email in new[] { adminEmail, userEmail })
        {
            var registerContent = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "Email", email },
                { "Password", password },
                { "ConfirmPassword", password },
                { "__RequestVerificationToken", registerToken }
            });
            await _http.PostAsync("/Account/Register", registerContent);
            registerToken = await GetAntiCsrfToken("/Account/Register");
        }

        var adminId = await GetUserIdByEmail(adminEmail);
        var userId = await GetUserIdByEmail(userEmail);

        await GrantPermission(adminId, AppPermissionNames.CanCreateWeeklyReport);
        await GrantPermission(adminId, AppPermissionNames.CanManageAnyoneWeeklyReport);
        await GrantPermission(userId, AppPermissionNames.CanCreateWeeklyReport);

        // 2. User submits their own report
        await LoginAs(userEmail, password);
        var now = DateTime.UtcNow;
        var offset = (int)now.DayOfWeek;
        var thisWeekStart = now.AddDays(-offset).Date;
        var weekStartStr = thisWeekStart.ToString("yyyy-MM-dd");

        var userCreateToken = await GetAntiCsrfToken("/WeeklyReport/Index");
        var userCreateContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "content", "User's original report" },
            { "weekStartDate", weekStartStr },
            { "__RequestVerificationToken", userCreateToken }
        });
        await _http.PostAsync("/WeeklyReport/Create", userCreateContent);

        // 3. Admin appends to user's report
        await LoginAs(adminEmail, password);
        var adminCreateToken = await GetAntiCsrfToken("/WeeklyReport/Index");
        var adminCreateContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "content", "Admin's appended report" },
            { "onBehalfOf", userId },
            { "weekStartDate", weekStartStr },
            { "__RequestVerificationToken", adminCreateToken }
        });
        var response = await _http.PostAsync("/WeeklyReport/Create", adminCreateContent);
        Assert.AreEqual(HttpStatusCode.Found, response.StatusCode);

        // 4. Verify in DB
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var reports = await db.WeeklyReports.Where(r => r.UserId == userId).ToListAsync();
            Assert.HasCount(1, reports);
            Assert.AreEqual("User's original report\r\n\r\nAdmin's appended report", reports[0].Content);
        }
    }

    [TestMethod]
    public async Task TestWeeklyReportWithProjects()
    {
        var email = $"test-proj-{Guid.NewGuid()}@aiursoft.com";
        var password = "Test-Password-123";

        // 1. Register
        var registerToken = await GetAntiCsrfToken("/Account/Register");
        var registerContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Email", email },
            { "Password", password },
            { "ConfirmPassword", password },
            { "__RequestVerificationToken", registerToken }
        });
        await _http.PostAsync("/Account/Register", registerContent);
        var userId = await GetUserIdByEmail(email);
        await GrantPermission(userId, AppPermissionNames.CanCreateWeeklyReport);
        await LoginAs(email, password);

        // 2. Create Approved Project (Requirement)
        int projectId;
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            // Enable Force Project Association
            var setting = await db.GlobalSettings.FindAsync(SettingsMap.ForceProjectAssociation);
            if (setting == null)
            {
                setting = new GlobalSetting { Key = SettingsMap.ForceProjectAssociation, Value = "True" };
                db.GlobalSettings.Add(setting);
            }
            else
            {
                setting.Value = "True";
            }

            var req = new Requirement
            {
                Title = "Test Project",
                Content = "Desc",
                SubmitterId = userId,
                Status = RequirementStatus.Approved
            };
            db.Requirements.Add(req);
            await db.SaveChangesAsync();
            projectId = req.Id;
        }

        // 3. Try to submit without project
        var createToken = await GetAntiCsrfToken("/WeeklyReport/Index");
        var createContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Content", "Report without project" },
            { "__RequestVerificationToken", createToken }
        });
        var response = await _http.PostAsync("/WeeklyReport/Create", createContent);
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);

        // 4. Submit with project
        createContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Content", "Report with project" },
            { "Requirements[0].RequirementId", projectId.ToString() },
            { "Requirements[0].Hours", "5" },
            { "__RequestVerificationToken", createToken }
        });
        response = await _http.PostAsync("/WeeklyReport/Create", createContent);
        Assert.AreEqual(HttpStatusCode.Found, response.StatusCode);

        // 5. Verify
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var report = await db.WeeklyReports
                .Include(r => r.WeeklyReportRequirements)
                .FirstAsync(r => r.UserId == userId);

            Assert.AreEqual(1, report.WeeklyReportRequirements.Count());
            Assert.AreEqual(projectId, report.WeeklyReportRequirements.First().RequirementId);
            Assert.AreEqual(5, report.WeeklyReportRequirements.First().Hours);
        }
    }
}
