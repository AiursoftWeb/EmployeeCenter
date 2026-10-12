using Aiursoft.EmployeeCenter.Authorization;
// ReSharper disable RedundantUsingDirective

namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class ManageOnboardingProgressTests
{
    private readonly int _port;
    private readonly HttpClient _http;
    private IHost? _server;

    public ManageOnboardingProgressTests()
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
    public async Task VisibilityTest()
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

        // 2. Verify progress table is visible for admin
        var manageResponse = await _http.GetAsync("/ManageOnboarding/Index");
        manageResponse.EnsureSuccessStatusCode();
        var manageHtml = await manageResponse.Content.ReadAsStringAsync();
        Assert.Contains("Employee Onboarding Progress", manageHtml, "Admin should see the progress table");

        // 3. Create a role and user with only CanManageOnboarding
        string userName = "onboarding_manager";
        string password = "TestPassword123!";
        using (var scope = _server!.Services.CreateScope())
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

            var role = new IdentityRole("OnboardingManagerOnly");
            await roleManager.CreateAsync(role);
            await roleManager.AddClaimAsync(role, new Claim(AppPermissions.Type, AppPermissionNames.CanManageOnboarding));

            var user = new User
            {
                UserName = userName,
                DisplayName = "Onboarding Manager",
                Email = "om@test.com",
                AvatarRelativePath = User.DefaultAvatarPath
            };
            await userManager.CreateAsync(user, password);
            await userManager.AddToRoleAsync(user, "OnboardingManagerOnly");
        }

        // 4. Login as the new user
        await _http.GetAsync("/Account/LogOff");
        loginToken = await GetAntiCsrfToken("/Account/Login");
        loginResponse = await _http.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "EmailOrUserName", userName },
            { "Password", password },
            { "__RequestVerificationToken", loginToken }
        }));
        Assert.AreEqual(HttpStatusCode.Found, loginResponse.StatusCode);

        // 5. Verify progress table is NOT visible
        manageResponse = await _http.GetAsync("/ManageOnboarding/Index");
        manageResponse.EnsureSuccessStatusCode();
        manageHtml = await manageResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Employee Onboarding Progress", manageHtml, "User with only CanManageOnboarding should NOT see the progress table");
        Assert.Contains("Manage Onboarding Tasks", manageHtml, "User should still see the tasks management page");

        // 6. Grant CanReadUsers to the role and verify it becomes visible
        using (var scope = _server!.Services.CreateScope())
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var role = await roleManager.FindByNameAsync("OnboardingManagerOnly");
            await roleManager.AddClaimAsync(role!, new Claim(AppPermissions.Type, AppPermissionNames.CanReadUsers));
        }

        // Re-login because claims are in the cookie
        await _http.GetAsync("/Account/LogOff");
        loginToken = await GetAntiCsrfToken("/Account/Login");
        loginResponse = await _http.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "EmailOrUserName", userName },
            { "Password", password },
            { "__RequestVerificationToken", loginToken }
        }));
        Assert.AreEqual(HttpStatusCode.Found, loginResponse.StatusCode);

        manageResponse = await _http.GetAsync("/ManageOnboarding/Index");
        manageResponse.EnsureSuccessStatusCode();
        manageHtml = await manageResponse.Content.ReadAsStringAsync();
        Assert.Contains("Employee Onboarding Progress", manageHtml, "User with both permissions should see the progress table");
    }
}
