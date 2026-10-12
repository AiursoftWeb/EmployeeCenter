using Aiursoft.EmployeeCenter.Authorization;

namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class ReimbursementTests
{
    private readonly int _port;
    private readonly HttpClient _http;
    private IHost? _server;

    public ReimbursementTests()
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
    public async Task ReimbursementFlowTest()
    {
        // 1. Login as a normal user
        var userEmail = $"user-{Guid.NewGuid()}@aiursoft.com";
        var password = "Test-Password-123";
        string userId;

        using (var scope = _server!.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = new User
            {
                UserName = userEmail.Split('@')[0],
                Email = userEmail,
                DisplayName = "Test User",
                AvatarRelativePath = User.DefaultAvatarPath
            };
            await userManager.CreateAsync(user, password);
            userId = user.Id;

            // Grant permission to submit reimbursement
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            db.UserClaims.Add(new IdentityUserClaim<string>
            {
                UserId = userId,
                ClaimType = "Permission",
                ClaimValue = AppPermissionNames.CanSubmitReimbursement
            });
            await db.SaveChangesAsync();
        }

        await Login(userEmail, password);

        // 2. Submit a reimbursement request
        var createToken = await GetAntiCsrfToken("/Reimbursement/Create");
        var createContent = new MultipartFormDataContent();
        createContent.Add(new StringContent(DateTime.UtcNow.Date.AddDays(-1).ToString("yyyy-MM-dd")), "ExpenseTime");
        createContent.Add(new StringContent("Transport"), "Category");
        createContent.Add(new StringContent("Taxi to client"), "Purpose");
        createContent.Add(new StringContent("50.5"), "Amount");
        createContent.Add(new StringContent("HKD"), "Currency");
        createContent.Add(new StringContent("https://example.com/invoice"), "InvoiceSourceUrl");
        createContent.Add(new StringContent("false"), "SaveAsDraft");
        createContent.Add(new StringContent(createToken), "__RequestVerificationToken");

        var createResponse = await _http.PostAsync("/Reimbursement/Create", createContent);
        Assert.AreEqual(HttpStatusCode.Found, createResponse.StatusCode);

        // 3. Verify it exists in DB
        int reimbursementId;
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var reimbursement = await db.Reimbursements.FirstAsync(r => r.SubmitterId == userId);
            Assert.AreEqual("Transport", reimbursement.Category);
            Assert.AreEqual(50.5m, reimbursement.Amount);
            Assert.AreEqual("HKD", reimbursement.Currency);
            Assert.AreEqual("https://example.com/invoice", reimbursement.InvoiceSourceUrl);
            Assert.AreEqual(ReimbursementStatus.Applying, reimbursement.Status);
            reimbursementId = reimbursement.Id;
        }

        // 4. Login as admin/approver
        await Login("admin@default.com", "Admin@123456!");

        // 5. Acknowledge the request
        var manageToken = await GetAntiCsrfToken("/Reimbursement/Manage");
        var acknowledgeResponse = await _http.PostAsync($"/Reimbursement/Acknowledge/{reimbursementId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "__RequestVerificationToken", manageToken }
        }));
        Assert.AreEqual(HttpStatusCode.Found, acknowledgeResponse.StatusCode);

        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var reimbursement = await db.Reimbursements.FindAsync(reimbursementId);
            Assert.AreEqual(ReimbursementStatus.Acknowledged, reimbursement!.Status);
        }

        // 6. Finalize (Reimburse)
        var detailsToken = await GetAntiCsrfToken($"/Reimbursement/Details/{reimbursementId}");
        var reimburseContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Id", reimbursementId.ToString() },
            { "Comment", "Paid via bank transfer." },
            { "__RequestVerificationToken", detailsToken }
        });
        var reimburseResponse = await _http.PostAsync("/Reimbursement/Reimburse", reimburseContent);
        Assert.AreEqual(HttpStatusCode.Found, reimburseResponse.StatusCode);

        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var reimbursement = await db.Reimbursements.FindAsync(reimbursementId);
            Assert.AreEqual(ReimbursementStatus.Reimbursed, reimbursement!.Status);
            Assert.AreEqual("Paid via bank transfer.", reimbursement.Comment);
        }
    }

    [TestMethod]
    public async Task CannotApproveSelfTest()
    {
        // 1. Login as admin (who has both Submit and Approve permissions)
        await Login("admin@default.com", "Admin@123456!");

        // 2. Submit a reimbursement request
        var createToken = await GetAntiCsrfToken("/Reimbursement/Create");
        var createContent = new MultipartFormDataContent();
        createContent.Add(new StringContent(DateTime.UtcNow.Date.ToString("yyyy-MM-dd")), "ExpenseTime");
        createContent.Add(new StringContent("Meals"), "Category");
        createContent.Add(new StringContent("Dinner with team"), "Purpose");
        createContent.Add(new StringContent("100"), "Amount");
        createContent.Add(new StringContent("false"), "SaveAsDraft");
        createContent.Add(new StringContent(createToken), "__RequestVerificationToken");

        var createResponse = await _http.PostAsync("/Reimbursement/Create", createContent);
        Assert.AreEqual(HttpStatusCode.Found, createResponse.StatusCode);

        int reimbursementId;
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var reimbursement = await db.Reimbursements.OrderByDescending(r => r.Id).FirstAsync();
            reimbursementId = reimbursement.Id;
        }

        // 3. Try to acknowledge self request
        var manageToken = await GetAntiCsrfToken("/Reimbursement/Manage");
        var acknowledgeResponse = await _http.PostAsync($"/Reimbursement/Acknowledge/{reimbursementId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "__RequestVerificationToken", manageToken }
        }));
        
        // Should be BadRequest
        Assert.AreEqual(HttpStatusCode.BadRequest, acknowledgeResponse.StatusCode);
    }
}
