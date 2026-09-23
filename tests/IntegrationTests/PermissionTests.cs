using Aiursoft.EmployeeCenter.Authorization;

namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class PermissionTests
{
    private readonly int _port;
    private readonly HttpClient _http;
    private IHost? _server;

    public PermissionTests()
    {
        var cookieContainer = new CookieContainer();
        var handler = new HttpClientHandler
        {
            CookieContainer = cookieContainer,
            AllowAutoRedirect = false // We want to check for redirects manually
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

    private async Task LoginAs(string username, string password)
    {
        // Clear cookies first? Logic dictates new HttpClient per session or Logout.
        // But here we might just overwrite. Safest is to logout first.
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
            var content = await loginResponse.Content.ReadAsStringAsync();
            throw new Exception($"Login failed for {username}. Status: {loginResponse.StatusCode}. Content: {content}");
        }
    }

    [TestMethod]
    public async Task RestrictedControllers_AreProtected()
    {
        // 1. Create a normal user
        var uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8);
        var userName = $"user-{uniqueId}";
        var email = $"{userName}@aiursoft.com";
        var password = "Test-Password-123";

        // Register
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

        // Get User ID (useful for SshKey test)
        string adminId;
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var admin = await db.Users.FirstAsync(u => u.UserName == "admin"); // admin is seeded
            adminId = admin.Id;
        }

        // Login as normal user
        await LoginAs(email, password);

        // List of restricted URLs to test
        var restrictedUrls = new List<string>
        {
            "/ManagePayroll/Index",
            "/ManagePayroll/Create",
            "/Users/Index",
            "/Users/Create",
            "/Roles/Index",
            "/Roles/Create",
            "/ManageOnboarding/Index",
            "/System/Index",
            "/ManageContract/Index",
            "/Assets/Index",
            "/ManageCertificate/Index",
            "/CompanyEntity/Manage"
        };

        foreach (var url in restrictedUrls)
        {
            var response = await _http.GetAsync(url);

            // Should be redirected to AccessDenied or Forbidden
            // AccessDenied path is /Error/Unauthorized

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                // Good
            }
            else if (response.StatusCode == HttpStatusCode.Found) // Redirect
            {
                var location = response.Headers.Location?.ToString();
                Assert.IsNotNull(location, $"Redirect location is null for {url}");
                Assert.IsTrue(location.Contains("/Error/Unauthorized") || location.Contains("AccessDenied") || location.Contains("/Error/Code403"),
                    $"URL {url} redirected to {location} instead of AccessDenied.");
            }
            else
            {
                Assert.Fail($"URL {url} was accessible ({response.StatusCode}) for a normal user.");
            }
        }

        // Test CompanyEntity/Index is accessible
        var companyEntityResponse = await _http.GetAsync("/CompanyEntity/Index");
        Assert.AreEqual(HttpStatusCode.OK, companyEntityResponse.StatusCode, "CompanyEntity/Index should be accessible for a normal user.");

        // Test SshKeys for another user (Admin)
        // SshKeysController returns Unauthorized() (401) manually.
        var sshUrl = $"/SshKeys/Index?userId={adminId}";
        var sshResponse = await _http.GetAsync(sshUrl);
        // 401 Unauthorized usually not handled by Cookie Auth Redirect in default setup unless Challenge is triggered. 
        // But let's see what happens.
        if (sshResponse.StatusCode == HttpStatusCode.Unauthorized)
        {
            // Acceptable
        }
        else if (sshResponse.StatusCode == HttpStatusCode.Found)
        {
            // Redirect to Login is also acceptable for 401
            var location = sshResponse.Headers.Location?.ToString();
            Assert.IsNotNull(location, "Redirect location is null for SshKeys");
            StringAssert.Contains(location, "/Account/Login");
        }
        else
        {
            Assert.Fail($"SshKeys for other user was accessible ({sshResponse.StatusCode}).");
        }
    }

    [TestMethod]
    public async Task CanManageSshKeys_Permission_Works()
    {
        // This test verifies that if we GIVE the permission, they CAN access it.
        // And if we take it away, they cannot.

        // 1. Create a user
        var uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8);
        var userName = $"user-perm-{uniqueId}";
        var email = $"{userName}@aiursoft.com";
        var password = "Test-Password-123";

        // Register
        await _http.GetAsync("/Account/LogOff");
        var registerToken = await GetAntiCsrfToken("/Account/Register");
        await _http.PostAsync("/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Email", email },
            { "Password", password },
            { "ConfirmPassword", password },
            { "__RequestVerificationToken", registerToken }
        }));

        string userId;
        string adminId;
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var user = await db.Users.FirstAsync(u => u.UserName == userName);
            userId = user.Id;
            adminId = (await db.Users.FirstAsync(u => u.UserName == "admin")).Id;
        }

        // Login
        await LoginAs(email, password);

        // Try to access admin SSH keys -> Fail
        var sshUrl = $"/SshKeys/Index?userId={adminId}";
        var response = await _http.GetAsync(sshUrl);
        Assert.IsTrue(response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Found);

        // Grant Permission
        using (var scope = _server!.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            // Create a role with permission
            var roleName = "SshManager";
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var role = new IdentityRole(roleName);
                await roleManager.CreateAsync(role);
                await roleManager.AddClaimAsync(role, new Claim(AppPermissions.Type, AppPermissionNames.CanManageSshKeys));
            }

            var user = await userManager.FindByIdAsync(userId);
            await userManager.AddToRoleAsync(user!, roleName);
        }

        // Re-login to refresh claims
        await LoginAs(email, password);

        // Try access again -> Success
        response = await _http.GetAsync(sshUrl);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task ContractPermissions_WorkAsExpected()
    {
        // 1. Create a user
        var uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8);
        var userName = $"user-contract-{uniqueId}";
        var email = $"{userName}@aiursoft.com";
        var password = "Test-Password-123";

        // Register
        await _http.GetAsync("/Account/LogOff");
        var registerToken = await GetAntiCsrfToken("/Account/Register");
        await _http.PostAsync("/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Email", email },
            { "Password", password },
            { "ConfirmPassword", password },
            { "__RequestVerificationToken", registerToken }
        }));

        string userId;
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var user = await db.Users.FirstAsync(u => u.UserName == userName);
            userId = user.Id;
        }

        // Login
        await LoginAs(email, password);

        // 2. Regular user -> /ManageContract/Index -> Forbidden (Already tested in RestrictedControllers_AreProtected, but good to verify specifically)
        var response = await _http.GetAsync("/ManageContract/Index");
        Assert.IsTrue(response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.Found);

        // 3. Grant CanViewContractHistory
        using (var scope = _server!.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            var roleName = "ContractViewer";
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var role = new IdentityRole(roleName);
                await roleManager.CreateAsync(role);
                await roleManager.AddClaimAsync(role, new Claim(AppPermissions.Type, AppPermissionNames.CanViewContractHistory));
            }

            var user = await userManager.FindByIdAsync(userId);
            await userManager.AddToRoleAsync(user!, roleName);
        }

        // Re-login
        await LoginAs(email, password);

        // 4. User with CanViewContractHistory -> /ManageContract/Index -> OK
        response = await _http.GetAsync("/ManageContract/Index");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        // 5. User with CanViewContractHistory -> /ManageContract/Create -> Forbidden
        response = await _http.GetAsync("/ManageContract/Create");
        Assert.IsTrue(response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.Found);
    }

    [TestMethod]
    public async Task RemovedGitLabProjectsRoute_ReturnsNotFound()
    {
        var response = await _http.GetAsync("/Projects/Index");
        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task CompanyEntityManagePermission_Works()
    {
        // 1. Create a user
        var uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8);
        var userName = $"user-entity-{uniqueId}";
        var email = $"{userName}@aiursoft.com";
        var password = "Test-Password-123";

        // Register
        await _http.GetAsync("/Account/LogOff");
        var registerToken = await GetAntiCsrfToken("/Account/Register");
        await _http.PostAsync("/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Email", email },
            { "Password", password },
            { "ConfirmPassword", password },
            { "__RequestVerificationToken", registerToken }
        }));

        string userId;
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var user = await db.Users.FirstAsync(u => u.UserName == userName);
            userId = user.Id;
        }

        // Login
        await LoginAs(email, password);

        // 2. Regular user -> /CompanyEntity/Manage -> Forbidden
        var response = await _http.GetAsync("/CompanyEntity/Manage");
        Assert.IsTrue(response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.Found);

        // 3. Grant CanManageCompanyEntities
        using (var scope = _server!.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            var roleName = "EntityManager";
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var role = new IdentityRole(roleName);
                await roleManager.CreateAsync(role);
                await roleManager.AddClaimAsync(role, new Claim(AppPermissions.Type, AppPermissionNames.CanManageCompanyEntities));
            }

            var user = await userManager.FindByIdAsync(userId);
            await userManager.AddToRoleAsync(user!, roleName);
        }

        // Re-login
        await LoginAs(email, password);

        // 4. User with CanManageCompanyEntities -> /CompanyEntity/Manage -> OK
        response = await _http.GetAsync("/CompanyEntity/Manage");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }
}
