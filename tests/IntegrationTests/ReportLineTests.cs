using Aiursoft.EmployeeCenter.Authorization;

namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class ReportLineTests
{
    private readonly int _port;
    private readonly HttpClient _http;
    private IHost? _server;

    public ReportLineTests()
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
    public async Task TestCircularDependency()
    {
        // 1. Create two users
        string idA, idB, emailA;
        var suffix = Guid.NewGuid().ToString("N")[..6];
        using (var scope = _server!.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var userA = new User { UserName = "usera" + suffix, DisplayName = "User A", Email = "usera" + suffix + "@test.com", AvatarRelativePath = User.DefaultAvatarPath };
            var userB = new User { UserName = "userb" + suffix, DisplayName = "User B", Email = "userb" + suffix + "@test.com", AvatarRelativePath = User.DefaultAvatarPath };
            var resA = await userManager.CreateAsync(userA, "Password123!");
            var resB = await userManager.CreateAsync(userB, "Password123!");
            if (!resA.Succeeded || !resB.Succeeded)
            {
                throw new Exception("Failed to create users: " + string.Join(", ", resA.Errors.Concat(resB.Errors).Select(e => e.Description)));
            }
            idA = userA.Id;
            idB = userB.Id;
            emailA = userA.Email;
        }

        // 2. Set A's manager to B, and B's manager to A
        using (var scope = _server!.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var dbUserA = await userManager.FindByIdAsync(idA);
            var dbUserB = await userManager.FindByIdAsync(idB);
            dbUserA!.ManagerId = idB;
            dbUserB!.ManagerId = idA;
            await userManager.UpdateAsync(dbUserA);
            await userManager.UpdateAsync(dbUserB);
        }

        // 3. Login as User A and view report line
        await LoginAsync(emailA, "Password123!");

        var reportLinePage = await _http.GetAsync("/ReportLine");
        reportLinePage.EnsureSuccessStatusCode();
        var reportLineHtml = await reportLinePage.Content.ReadAsStringAsync();

        Assert.Contains("Circular dependency detected in report line!", reportLineHtml);
    }

    [TestMethod]
    public async Task TestUserDetailsDisplay()
    {
        // 1. Create a user with full info
        string email;
        var suffix = Guid.NewGuid().ToString("N")[..6];
        using (var scope = _server!.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = new User
            {
                UserName = "user" + suffix,
                DisplayName = "Display Name",
                Email = "user" + suffix + "@test.com",
                AvatarRelativePath = User.DefaultAvatarPath,
                JobLevel = "L3",
                Title = "Technical Lead",
                LegalName = "Legal Name",
                PhoneNumber = "123456789",
                BankName = "Test Bank",
                BankAccount = "111222333",
                BankAccountName = "Legal Name"
            };
            var res = await userManager.CreateAsync(user, "Password123!");
            if (!res.Succeeded)
            {
                throw new Exception("Failed to create user: " + string.Join(", ", res.Errors.Select(e => e.Description)));
            }
            email = user.Email;
        }

        // 2. Login as the user
        await LoginAsync(email, "Password123!");

        // 3. View report line (Index defaults to self)
        var reportLinePage = await _http.GetAsync("/ReportLine");
        reportLinePage.EnsureSuccessStatusCode();
        var reportLineHtml = await reportLinePage.Content.ReadAsStringAsync();

        // 4. Verify user details are present
        Assert.Contains("User Details", reportLineHtml);
        Assert.Contains("L3", reportLineHtml);
        Assert.Contains("Technical Lead", reportLineHtml);
        Assert.Contains("Legal Name", reportLineHtml);
        Assert.Contains("123456789", reportLineHtml);
        Assert.Contains("Test Bank", reportLineHtml);
        Assert.Contains("111222333", reportLineHtml);
    }

    [TestMethod]
    public async Task TestUserDetailsDisplayWithPermission()
    {
        // 1. Create a target user
        string targetUserId, targetUserName;
        var suffix = Guid.NewGuid().ToString("N")[..6];
        using (var scope = _server!.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = new User
            {
                UserName = "target" + suffix,
                DisplayName = "Target User",
                Email = "target" + suffix + "@test.com",
                AvatarRelativePath = User.DefaultAvatarPath,
                JobLevel = "L5"
            };
            await userManager.CreateAsync(user, "Password123!");
            targetUserId = user.Id;
            targetUserName = user.UserName;
        }

        // 2. Create a viewer user with CanReadUsers permission
        string viewerEmail;
        using (var scope = _server!.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            var viewer = new User
            {
                UserName = "viewer" + suffix,
                DisplayName = "Viewer User",
                Email = "viewer" + suffix + "@test.com",
                AvatarRelativePath = User.DefaultAvatarPath
            };
            await userManager.CreateAsync(viewer, "Password123!");
            viewerEmail = viewer.Email;

            var roleName = "Viewers" + suffix;
            var role = new IdentityRole(roleName);
            await roleManager.CreateAsync(role);
            await roleManager.AddClaimAsync(role, new Claim(AppPermissions.Type, AppPermissionNames.CanReadUsers));
            await roleManager.AddClaimAsync(role, new Claim(AppPermissions.Type, AppPermissionNames.CanViewReportLine));
            await userManager.AddToRoleAsync(viewer, roleName);
        }

        // 3. Login as viewer
        await LoginAsync(viewerEmail, "Password123!");

        // 4. View target's report line
        var reportLinePage = await _http.GetAsync("/ReportLine/Index/" + targetUserId);
        reportLinePage.EnsureSuccessStatusCode();
        var reportLineHtml = await reportLinePage.Content.ReadAsStringAsync();

        // 5. Verify target details are present
        Assert.Contains("User Details", reportLineHtml);
        Assert.Contains("L5", reportLineHtml);
        Assert.Contains(targetUserName, reportLineHtml);

        // 6. Verify Mermaid graph links
        Assert.Contains($"/ReportLine/Index/{targetUserId}", reportLineHtml);
        Assert.IsFalse(reportLineHtml.Contains($"/Users/Details/{targetUserId}"), "Graph should not link to User Details anymore");
    }

    [TestMethod]
    public async Task TestReportLineLinks()
    {
        // 1. Create a user
        string userId, email;
        var suffix = Guid.NewGuid().ToString("N")[..6];
        using (var scope = _server!.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = new User
            {
                UserName = "user" + suffix,
                DisplayName = "Report User",
                Email = "user" + suffix + "@test.com",
                AvatarRelativePath = User.DefaultAvatarPath
            };
            await userManager.CreateAsync(user, "Password123!");
            userId = user.Id;
            email = user.Email;
        }

        // 2. Login
        await LoginAsync(email, "Password123!");

        // 3. View report line
        var response = await _http.GetAsync("/ReportLine");
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();

        // 4. Verify link in Mermaid label
        Assert.Contains($"/ReportLine/Index/{userId}", html);
        Assert.IsFalse(html.Contains($"/Users/Details/{userId}"), "Mermaid label should link to ReportLine Index");
    }
}
