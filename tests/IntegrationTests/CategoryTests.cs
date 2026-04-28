
namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class CategoryTests
{
    private readonly int _port;
    private readonly HttpClient _http;
    private IHost? _server;

    public CategoryTests()
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
    public async Task DeleteCategoryTest()
    {
        // 1. Login as admin
        var loginToken = await GetAntiCsrfToken("/Account/Login");
        var loginContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "EmailOrUserName", "admin" },
            { "Password", "Admin@123456!" },
            { "__RequestVerificationToken", loginToken }
        });
        await _http.PostAsync("/Account/Login", loginContent);

        // 2. Create Category that will be used
        await _http.PostAsync("/Assets/CreateCategory", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "NewName", "Used Category" },
            { "NewCode", "USED" },
            { "__RequestVerificationToken", await GetAntiCsrfToken("/Assets/Categories") }
        }));

        int usedCategoryId;
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var cat = await db.AssetCategories.FirstAsync(c => c.Name == "Used Category");
            usedCategoryId = cat.Id;
        }

        // 3. Create Model using this category
        await _http.PostAsync("/Assets/CreateModel", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "NewCategoryId", usedCategoryId.ToString() },
            { "NewBrand", "Brand" },
            { "NewModelName", "Model X" },
            { "__RequestVerificationToken", await GetAntiCsrfToken("/Assets/Models") }
        }));

        // 4. Try to delete Used Category (Should Fail)
        var deleteUsedResponse = await _http.PostAsync("/Assets/DeleteCategory", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "id", usedCategoryId.ToString() },
            { "__RequestVerificationToken", await GetAntiCsrfToken("/Assets/Categories") }
        }));
        Assert.AreEqual(HttpStatusCode.BadRequest, deleteUsedResponse.StatusCode);

        // 5. Create Category that will remain unused
        await _http.PostAsync("/Assets/CreateCategory", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "NewName", "Unused Category" },
            { "NewCode", "UNUSED" },
            { "__RequestVerificationToken", await GetAntiCsrfToken("/Assets/Categories") }
        }));

        int unusedCategoryId;
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var cat = await db.AssetCategories.FirstAsync(c => c.Name == "Unused Category");
            unusedCategoryId = cat.Id;
        }

        // 6. Try to delete Unused Category (Should Succeed)
        var deleteUnusedResponse = await _http.PostAsync("/Assets/DeleteCategory", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "id", unusedCategoryId.ToString() },
            { "__RequestVerificationToken", await GetAntiCsrfToken("/Assets/Categories") }
        }));

        // Redirect implies success in this controller logic
        Assert.AreEqual(HttpStatusCode.Redirect, deleteUnusedResponse.StatusCode);

        // 7. Verify deletion
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var exists = await db.AssetCategories.AnyAsync(c => c.Id == unusedCategoryId);
            Assert.IsFalse(exists);
        }
    }
}
