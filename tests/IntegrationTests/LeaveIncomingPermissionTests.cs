using Aiursoft.EmployeeCenter.Authorization;

namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class LeaveIncomingPermissionTests
{
    private readonly int _port;
    private readonly HttpClient _http;
    private IHost? _server;

    public LeaveIncomingPermissionTests()
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
        var match = Regex.Match(html,
            @"<input name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)"" />");
        if (!match.Success)
        {
            throw new InvalidOperationException($"Could not find anti-CSRF token on page: {url}");
        }
        return match.Groups[1].Value;
    }

    private async Task LoginAsync(string email, string password)
    {
        var loginToken = await GetAntiCsrfToken("/Account/Login");
        var loginContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "EmailOrUserName", email },
            { "Password", password },
            { "__RequestVerificationToken", loginToken }
        });
        var loginResponse = await _http.PostAsync("/Account/Login", loginContent);
        Assert.AreEqual(HttpStatusCode.Found, loginResponse.StatusCode);
    }

    [TestMethod]
    public async Task TestIncomingViewPermissions()
    {
        string managerId, managerEmail;
        string employeeId;
        string hrEmail;
        string otherId, otherEmail;

        using (var scope = _server!.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();

            // Create Manager
            var manager = new User { UserName = "manager", Email = "manager@test.com", DisplayName = "Manager", AvatarRelativePath = User.DefaultAvatarPath };
            await userManager.CreateAsync(manager, "Password123!");
            managerId = manager.Id;
            managerEmail = manager.Email;

            // Create Employee reporting to Manager
            var employee = new User { UserName = "employee", Email = "employee@test.com", DisplayName = "Employee", ManagerId = managerId, AvatarRelativePath = User.DefaultAvatarPath };
            await userManager.CreateAsync(employee, "Password123!");
            employeeId = employee.Id;

            // Create HR with CanApproveAnyLeave
            var hr = new User { UserName = "hr", Email = "hr@test.com", DisplayName = "HR", AvatarRelativePath = User.DefaultAvatarPath };
            await userManager.CreateAsync(hr, "Password123!");
            hrEmail = hr.Email;

            var hrRole = new IdentityRole("HRRole");
            await roleManager.CreateAsync(hrRole);
            await roleManager.AddClaimAsync(hrRole, new Claim("Permission", AppPermissionNames.CanApproveAnyLeave));
            await userManager.AddToRoleAsync(hr, "HRRole");

            // Create Other user (not manager, no permission)
            var other = new User { UserName = "other", Email = "other@test.com", DisplayName = "Other", AvatarRelativePath = User.DefaultAvatarPath };
            await userManager.CreateAsync(other, "Password123!");
            otherId = other.Id;
            otherEmail = other.Email;

            // Create leave for employee
            db.LeaveApplications.Add(new LeaveApplication
            {
                UserId = employeeId,
                LeaveType = LeaveType.AnnualLeave,
                StartDate = DateTime.UtcNow.Date.AddDays(10),
                EndDate = DateTime.UtcNow.Date.AddDays(12),
                TotalDays = 3,
                Reason = "Employee Vacation",
                IsPending = true,
                SubmittedAt = DateTime.UtcNow
            });

            // Create leave for other
            db.LeaveApplications.Add(new LeaveApplication
            {
                UserId = otherId,
                LeaveType = LeaveType.AnnualLeave,
                StartDate = DateTime.UtcNow.Date.AddDays(10),
                EndDate = DateTime.UtcNow.Date.AddDays(12),
                TotalDays = 3,
                Reason = "Other Vacation",
                IsPending = true,
                SubmittedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
        }

        // 1. Manager should see Employee's leave but NOT Other's leave
        await LoginAsync(managerEmail, "Password123!");
        var response = await _http.GetAsync("/Leave/Incoming");
        var html = await response.Content.ReadAsStringAsync();
        StringAssert.Contains(html, "Employee Vacation");
        Assert.IsFalse(html.Contains("Other Vacation"));

        // 2. HR should see BOTH leaves by default (showAll=true)
        await LoginAsync(hrEmail, "Password123!");
        response = await _http.GetAsync("/Leave/Incoming");
        html = await response.Content.ReadAsStringAsync();
        StringAssert.Contains(html, "Employee Vacation");
        StringAssert.Contains(html, "Other Vacation");

        // 2.1 HR should see ONLY their team if showAll=false
        // In this test HR has no subordinates, so they should see NOTHING.
        response = await _http.GetAsync("/Leave/Incoming?showAll=false");
        html = await response.Content.ReadAsStringAsync();
        Assert.IsFalse(html.Contains("Employee Vacation"));
        Assert.IsFalse(html.Contains("Other Vacation"));
        StringAssert.Contains(html, "No pending applications.");

        // 3. Other should see NO leaves (unless they have subordinates, which they don't)
        await LoginAsync(otherEmail, "Password123!");
        response = await _http.GetAsync("/Leave/Incoming");
        html = await response.Content.ReadAsStringAsync();
        Assert.IsFalse(html.Contains("Employee Vacation"));
        Assert.IsFalse(html.Contains("Other Vacation"));
        StringAssert.Contains(html, "No pending applications.");
    }
}
