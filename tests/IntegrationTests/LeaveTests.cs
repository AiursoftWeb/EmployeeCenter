
namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class LeaveTests
{
    private readonly int _port;
    private readonly HttpClient _http;
    private IHost? _server;

    public LeaveTests()
    {
        var cookieContainer = new CookieContainer();
        var handler = new HttpClientHandler
        {
            CookieContainer = cookieContainer,
            AllowAutoRedirect = false // We need to handle redirects manually to check locations
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
        var match = Regex.Match(html,
            @"<input name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)"" />");
        if (!match.Success)
        {
            throw new InvalidOperationException($"Could not find anti-CSRF token on page: {url}");
        }

        return match.Groups[1].Value;
    }



    private async Task<int> GetLatestLeaveId(string userId)
    {
        using var scope = _server!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
        var leave = await db.LeaveApplications
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.Id)
            .FirstOrDefaultAsync();
        return leave?.Id ?? 0;
    }



    // Helper to get user ID based on email
    private async Task<string> GetUserIdByEmail(string email)
    {
        using var scope = _server!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
        var user = await db.Users.FirstAsync(u => u.Email == email);
        return user.Id;
    }

    private async Task Login(string email, string password)
    {
        var loginToken = await GetAntiCsrfToken("/Account/Login");
        var loginContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "EmailOrUserName", email },
            { "Password", password },
            { "__RequestVerificationToken", loginToken }
        });
        var response = await _http.PostAsync("/Account/Login", loginContent);
        Assert.AreEqual(HttpStatusCode.Found, response.StatusCode);
    }

    [TestMethod]
    public async Task CanSearchAnyoneLeaveHistoryWithPermissionTest()
    {
        // 1. Login as admin (who has CanApproveAnyLeave)
        await Login("admin@default.com", "Admin@123456!");

        // 2. Create another user (target user)
        var targetUserEmail = $"target-{Guid.NewGuid()}@aiursoft.com";
        string targetUserId;
        using (var scope = _server!.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var targetUser = new User
            {
                UserName = targetUserEmail.Split('@')[0],
                Email = targetUserEmail,
                DisplayName = "Target User",
                AvatarRelativePath = User.DefaultAvatarPath
            };
            var result = await userManager.CreateAsync(targetUser, "Test-Password-123");
            Assert.IsTrue(result.Succeeded);
            targetUserId = targetUser.Id;

            // Give some leave records to target user
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            db.LeaveApplications.Add(new LeaveApplication
            {
                UserId = targetUserId,
                LeaveType = LeaveType.AnnualLeave,
                StartDate = DateTime.UtcNow.Date.AddDays(5),
                EndDate = DateTime.UtcNow.Date.AddDays(7),
                TotalDays = 3,
                Reason = "Target's Vacation",
                IsPending = true,
                IsApproved = false,
                SubmittedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        // 3. Admin visits Team Calendar with searchUserId
        var response = await _http.GetAsync($"/Leave/TeamCalendar?searchUserId={targetUserId}");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        // 4. Verify search results are present
        StringAssert.Contains(html, "Leave History: Target User");
        StringAssert.Contains(html, "Target&#x27;s Vacation"); // HTML encoded
        StringAssert.Contains(html, "Pending");
    }

    [TestMethod]
    public async Task LeaveWithdrawalTest()
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
        await _http.PostAsync("/Account/Register", registerContent);

        // 2. Initialize Allocation (Visit Index)
        await _http.GetAsync("/Leave/Index");

        // 3. Apply for FUTURE leave (30 days from now)
        var thirtyDaysFromNow = DateTime.UtcNow.Date.AddDays(30);
        while (thirtyDaysFromNow.DayOfWeek == DayOfWeek.Saturday || thirtyDaysFromNow.DayOfWeek == DayOfWeek.Sunday)
        {
            thirtyDaysFromNow = thirtyDaysFromNow.AddDays(1);
        }

        var applyToken = await GetAntiCsrfToken("/Leave/Apply");
        var applyContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "LeaveType", "AnnualLeave" },
            { "StartDate", thirtyDaysFromNow.ToString("yyyy-MM-dd") },
            { "EndDate", thirtyDaysFromNow.AddDays(3).ToString("yyyy-MM-dd") },
            { "Reason", "Vacation" },
            { "__RequestVerificationToken", applyToken }
        });
        var applyResponse = await _http.PostAsync("/Leave/Apply", applyContent);
        Assert.AreEqual(HttpStatusCode.Found, applyResponse.StatusCode);

        // Get Leave ID
        var userId = await GetUserIdByEmail(email);
        var leaveId = await GetLatestLeaveId(userId);
        Assert.AreNotEqual(0, leaveId);

        // 4. Withdraw validity Check (Date < StartDate)
        // Today < Tomorrow => OK.

        // 5. Withdraw
        // Note: Withdraw is POST /Leave/Withdraw/{id} ? No, usually POST form data or query string?
        // Controller: public async Task<IActionResult> Withdraw(int id)
        // In ASP.NET Core MVC, if parameter is 'id', it can come from Route or Form or Query.
        // The form action is asp-route-id="@item.Id", so it generates /Leave/Withdraw/123
        // And method is POST.

        // Need token for withdraw form? Yes, ValidateAntiForgeryToken is on.
        // I need to get token from Index page which has the form?
        // Or just get any token from Leave/Index.
        var withdrawToken = await GetAntiCsrfToken("/Leave/Index");

        // The form submits to /Leave/Withdraw/{id}
        var withdrawResponse = await _http.PostAsync($"/Leave/Withdraw/{leaveId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "__RequestVerificationToken", withdrawToken }
        }));

        Assert.AreEqual(HttpStatusCode.Found, withdrawResponse.StatusCode); // Redirect to Index

        // 6. Verify IsWithdrawn
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var leave = await db.LeaveApplications.FindAsync(leaveId);
            Assert.IsTrue(leave!.IsWithdrawn);
        }
    }

    [TestMethod]
    public async Task CannotWithdrawStartedLeaveTest()
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

        // 2. Visit Index to init
        await _http.GetAsync("/Leave/Index");

        // 3. Apply for TODAY leave (Already started)
        var today = DateTime.UtcNow.Date;
        while (today.DayOfWeek == DayOfWeek.Saturday || today.DayOfWeek == DayOfWeek.Sunday)
        {
            today = today.AddDays(-1);
        }

        var applyToken = await GetAntiCsrfToken("/Leave/Apply");
        var applyContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "LeaveType", "AnnualLeave" },
            { "StartDate", today.ToString("yyyy-MM-dd") },
            { "EndDate", today.AddDays(14).ToString("yyyy-MM-dd") },
            { "Reason", "Sick" },
            { "__RequestVerificationToken", applyToken }
        });
        var applyResponse = await _http.PostAsync("/Leave/Apply", applyContent);
        Assert.AreEqual(HttpStatusCode.Found, applyResponse.StatusCode);

        // Get Leave ID
        var userId = await GetUserIdByEmail(email);
        var leaveId = await GetLatestLeaveId(userId);

        // 4. Attempt Withdraw
        // Since the leave has started, the "Withdraw" button (and its form) is NOT rendered on Index.
        // So we cannot get the token from Index page. We must get it from another page, e.g. Apply.
        var withdrawToken = await GetAntiCsrfToken("/Leave/Apply");
        var withdrawResponse = await _http.PostAsync($"/Leave/Withdraw/{leaveId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "__RequestVerificationToken", withdrawToken }
        }));

        // 5. Assert Failure
        // Controller returns BadRequest("Cannot withdraw...") -> 400
        Assert.AreEqual(HttpStatusCode.BadRequest, withdrawResponse.StatusCode);

        // 6. Verify DB Not Withdrawn
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var leave = await db.LeaveApplications.FindAsync(leaveId);
            Assert.IsFalse(leave!.IsWithdrawn);
        }
    }

    [TestMethod]
    public async Task TeamCalendarIncludesSelfTest()
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
        await _http.PostAsync("/Account/Register", registerContent);

        // 2. Initialize Allocation (Visit Index)
        await _http.GetAsync("/Leave/Index");

        // 3. Create an approved leave directly in DB
        var userId = await GetUserIdByEmail(email);
        var today = DateTime.UtcNow.Date;

        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var leave = new LeaveApplication
            {
                UserId = userId,
                LeaveType = LeaveType.AnnualLeave,
                StartDate = today.AddDays(10),
                EndDate = today.AddDays(12),
                TotalDays = 3,
                Reason = "Vacation",
                IsPending = false, // Approved
                IsApproved = true,
                SubmittedAt = DateTime.UtcNow,
                ReviewedAt = DateTime.UtcNow,
                ReviewedById = userId // Self approved for test simplicity
            };
            db.LeaveApplications.Add(leave);
            await db.SaveChangesAsync();
        }

        // 4. Visit Team Calendar
        var response = await _http.GetAsync("/Leave/TeamCalendar");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        // 5. Verify "Me" is present
        StringAssert.Contains(html, "Me", "The Team Calendar page should contain the text 'Me'.");

        // Also verify the user's email is present
        StringAssert.Contains(html, email, "The Team Calendar page should contain the user's email.");
    }

    [TestMethod]
    public async Task IncomingViewRendersTest()
    {
        var email = $"admin-{Guid.NewGuid()}@aiursoft.com";
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

        // 2. Visit Incoming (Requires permission, but seeding usually gives admin rights or first user is admin)
        var response = await _http.GetAsync("/Leave/Incoming");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        // 3. Verify table headers (including the ones we fixed)
        StringAssert.Contains(html, "Applicant", "Incoming view should have 'Applicant' column.");
        StringAssert.Contains(html, "Type", "Incoming view should have 'Type' column.");
        StringAssert.Contains(html, "Period", "Incoming view should have 'Period' column.");
        StringAssert.Contains(html, "Days", "Incoming view should have 'Days' column.");
        StringAssert.Contains(html, "Reason", "Incoming view should have 'Reason' column.");
        StringAssert.Contains(html, "Submitted", "Incoming view should have 'Submitted' column.");
        StringAssert.Contains(html, "Actions", "Incoming view should have 'Actions' column.");
    }

    [TestMethod]
    public async Task HolidayAdjustmentAffectsLeaveCalculationTest()
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
        await _http.PostAsync("/Account/Register", registerContent);

        // 2. Initialize Allocation (Visit Index)
        await _http.GetAsync("/Leave/Index");

        // 3. Find an upcoming Saturday in August (to avoid any public holidays)
        var targetSaturday = DateTime.UtcNow.Date;
        while (targetSaturday.DayOfWeek != DayOfWeek.Saturday || targetSaturday.Month != 8)
        {
            targetSaturday = targetSaturday.AddDays(1);
        }
        
        // Let's test a 3-day leave from Friday to Sunday enveloping the target Saturday.
        var startDate = targetSaturday.AddDays(-1);
        var endDate = targetSaturday.AddDays(1);

        // Normal calculation: Friday + Saturday + Sunday = 1 Working day (Friday only)
        // With Adjustment (Saturday = WorkDay): Friday + Saturday + Sunday = 2 Working days

        // Apply Adjustment
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            db.AdjustedHolidays.Add(new AdjustedHoliday
            {
                Date = targetSaturday,
                Type = HolidayType.WorkDay,
                Reason = "Compensatory work day"
            });
            await db.SaveChangesAsync();
        }

        // Apply for leave enveloping the Saturday
        var applyToken = await GetAntiCsrfToken("/Leave/Apply");
        var applyContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "LeaveType", "AnnualLeave" },
            { "StartDate", startDate.ToString("yyyy-MM-dd") },
            { "EndDate", endDate.ToString("yyyy-MM-dd") },
            { "Reason", "Vacation over adjusted weekend" },
            { "__RequestVerificationToken", applyToken }
        });
        
        var applyResponse = await _http.PostAsync("/Leave/Apply", applyContent);
        Assert.AreEqual(HttpStatusCode.Found, applyResponse.StatusCode);

        // Verify total days calculated
        var userId = await GetUserIdByEmail(email);
        var leaveId = await GetLatestLeaveId(userId);

        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var leave = await db.LeaveApplications.FindAsync(leaveId);
            Assert.IsNotNull(leave);

            // Since Friday is normal working day + Saturday is now adjusted as working day = 2 days
            Assert.AreEqual(2m, leave.TotalDays);
        }
    }
}
