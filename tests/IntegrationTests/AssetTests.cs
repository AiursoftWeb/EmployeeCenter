
namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class AssetTests
{
    private readonly int _port;
    private readonly HttpClient _http;
    private IHost? _server;

    public AssetTests()
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
    public async Task AssetLifecycleTest()
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

        // 2. Create Category, Model, Location, Vendor
        await _http.PostAsync("/Assets/CreateCategory", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "NewName", "Laptop" },
            { "NewCode", "LAP" },
            { "__RequestVerificationToken", await GetAntiCsrfToken("/Assets/Categories") }
        }));

        int categoryId;
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var cat = await db.AssetCategories.FirstAsync(c => c.Name == "Laptop");
            categoryId = cat.Id;
        }

        await _http.PostAsync("/Assets/CreateModel", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "NewCategoryId", categoryId.ToString() },
            { "NewBrand", "Apple" },
            { "NewModelName", "MacBook Pro" },
            { "__RequestVerificationToken", await GetAntiCsrfToken("/Assets/Models") }
        }));

        int modelId;
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var model = await db.AssetModels.FirstAsync(m => m.ModelName == "MacBook Pro");
            modelId = model.Id;
        }

        await _http.PostAsync("/Assets/CreateLocation", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "NewName", "HQ" },
            { "__RequestVerificationToken", await GetAntiCsrfToken("/Assets/Locations") }
        }));

        await _http.PostAsync("/Assets/CreateVendor", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "NewName", "Apple Store" },
            { "__RequestVerificationToken", await GetAntiCsrfToken("/Assets/Vendors") }
        }));

        // 3. Create Asset
        var createAssetToken = await GetAntiCsrfToken("/Assets/Create");
        await _http.PostAsync("/Assets/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "AssetTag", "LAP-001" },
            { "ModelId", modelId.ToString() },
            { "Status", ((int)AssetStatus.Idle).ToString() },
            { "__RequestVerificationToken", createAssetToken }
        }));

        Guid assetId;
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var asset = await db.Assets.FirstAsync(a => a.AssetTag == "LAP-001");
            assetId = asset.Id;
        }

        // 4. Create a user to assign to
        var uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8);
        var userName = $"user-{uniqueId}";
        var email = $"{userName}@aiursoft.com";
        var password = "Test-Password-123";

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

        // 5. Assign asset (as admin)
        await _http.GetAsync("/Account/LogOff");
        loginToken = await GetAntiCsrfToken("/Account/Login");
        await _http.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "EmailOrUserName", "admin" },
            { "Password", "Admin@123456!" },
            { "__RequestVerificationToken", loginToken }
        }));

        var assignToken = await GetAntiCsrfToken("/Assets/Assign/" + assetId);
        await _http.PostAsync("/Assets/Assign", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "AssetId", assetId.ToString() },
            { "AssigneeId", userId },
            { "Notes", "Testing assignment" },
            { "__RequestVerificationToken", assignToken }
        }));

        // 6. User confirms receipt
        await _http.GetAsync("/Account/LogOff");
        loginToken = await GetAntiCsrfToken("/Account/Login");
        await _http.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "EmailOrUserName", email },
            { "Password", password },
            { "__RequestVerificationToken", loginToken }
        }));

        var confirmToken = await GetAntiCsrfToken("/MyAssets/Index");
        await _http.PostAsync("/MyAssets/Confirm/" + assetId, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "__RequestVerificationToken", confirmToken }
        }));

        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var asset = await db.Assets.FirstAsync(a => a.Id == assetId);
            Assert.AreEqual(AssetStatus.InUse, asset.Status);
        }

        // 7. Admin returns asset
        await _http.GetAsync("/Account/LogOff");
        loginToken = await GetAntiCsrfToken("/Account/Login");
        await _http.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "EmailOrUserName", "admin" },
            { "Password", "Admin@123456!" },
            { "__RequestVerificationToken", loginToken }
        }));

        var indexToken = await GetAntiCsrfToken("/Assets/Index");
        await _http.PostAsync("/Assets/Return", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "id", assetId.ToString() },
            { "reason", "End of test" },
            { "__RequestVerificationToken", indexToken }
        }));

        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var asset = await db.Assets.FirstAsync(a => a.Id == assetId);
            Assert.AreEqual(AssetStatus.Idle, asset.Status);
            Assert.IsNull(asset.AssigneeId);
        }
    }

    [TestMethod]
    public async Task AssignAssetDuringCreationTest()
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

        // 2. Ensure we have a model and a user
        int modelId;
        string userId;
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var cat = new AssetCategory { Name = "Test Cat", Code = "TC" };
            db.AssetCategories.Add(cat);
            await db.SaveChangesAsync();

            var model = new AssetModel { ModelName = "Test Model", Brand = "Test", CategoryId = cat.Id };
            db.AssetModels.Add(model);
            await db.SaveChangesAsync();
            modelId = model.Id;

            var user = await db.Users.FirstAsync(u => u.UserName == "admin");
            userId = user.Id;
        }

        // 3. Create Asset with Assignee
        var createAssetToken = await GetAntiCsrfToken("/Assets/Create");
        await _http.PostAsync("/Assets/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "AssetTag", "ASSIGN-ON-CREATE" },
            { "ModelId", modelId.ToString() },
            { "Status", ((int)AssetStatus.PendingAccept).ToString() },
            { "AssigneeId", userId },
            { "__RequestVerificationToken", createAssetToken }
        }));

        // 4. Verify
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var asset = await db.Assets.FirstOrDefaultAsync(a => a.AssetTag == "ASSIGN-ON-CREATE");
            Assert.IsNotNull(asset);
            Assert.AreEqual(userId, asset.AssigneeId);
            Assert.AreEqual(AssetStatus.PendingAccept, asset.Status);
        }
    }

    [TestMethod]
    public async Task AssetWithCompanyEntityTest()
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

        // 2. Create Company Entity
        await _http.PostAsync("/CompanyEntity/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "CompanyName", "Test Entity" },
            { "EntityCode", "TE001" },
            { "__RequestVerificationToken", await GetAntiCsrfToken("/CompanyEntity/Create") }
        }));

        int entityId;
        int modelId;
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var entity = await db.CompanyEntities.FirstAsync(e => e.CompanyName == "Test Entity");
            entityId = entity.Id;

            var cat = new AssetCategory { Name = "Entity Test Cat", Code = "ETC" };
            db.AssetCategories.Add(cat);
            await db.SaveChangesAsync();

            var model = new AssetModel { ModelName = "Entity Test Model", Brand = "Test", CategoryId = cat.Id };
            db.AssetModels.Add(model);
            await db.SaveChangesAsync();
            modelId = model.Id;
        }

        // 3. Create Asset with Company Entity
        var createAssetToken = await GetAntiCsrfToken("/Assets/Create");
        await _http.PostAsync("/Assets/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "AssetTag", "ENTITY-ASSET" },
            { "ModelId", modelId.ToString() },
            { "Status", ((int)AssetStatus.Idle).ToString() },
            { "CompanyEntityId", entityId.ToString() },
            { "__RequestVerificationToken", createAssetToken }
        }));

        // 4. Verify in DB
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var asset = await db.Assets.Include(a => a.CompanyEntity).FirstOrDefaultAsync(a => a.AssetTag == "ENTITY-ASSET");
            Assert.IsNotNull(asset);
            Assert.AreEqual(entityId, asset.CompanyEntityId);
            Assert.AreEqual("Test Entity", asset.CompanyEntity!.CompanyName);
        }

        // 5. Verify in Index UI
        var indexResponse = await _http.GetAsync("/Assets/Index");
        var indexHtml = await indexResponse.Content.ReadAsStringAsync();
        Assert.Contains("ENTITY-ASSET", indexHtml);
        Assert.Contains("Test Entity", indexHtml);

        // 6. Verify in Details UI
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var asset = await db.Assets.FirstAsync(a => a.AssetTag == "ENTITY-ASSET");

            var detailsResponse = await _http.GetAsync($"/Assets/Details/{asset.Id}");
            var detailsHtml = await detailsResponse.Content.ReadAsStringAsync();
            Assert.Contains("ENTITY-ASSET", detailsHtml);
            Assert.Contains("Test Entity", detailsHtml);
        }

        int anotherEntityId;
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var asset = await db.Assets.FirstAsync(a => a.AssetTag == "ENTITY-ASSET");
            modelId = asset.ModelId;

            // 7. Update Company Entity
            await _http.PostAsync("/CompanyEntity/Create", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "CompanyName", "Another Entity" },
                { "EntityCode", "AE002" },
                { "__RequestVerificationToken", await GetAntiCsrfToken("/CompanyEntity/Create") }
            }));

            using (var scopeInner = _server!.Services.CreateScope())
            {
                var dbInner = scopeInner.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
                var anotherEntity = await dbInner.CompanyEntities.FirstAsync(e => e.CompanyName == "Another Entity");
                anotherEntityId = anotherEntity.Id;
            }

            var editToken = await GetAntiCsrfToken($"/Assets/Edit/{asset.Id}");
            await _http.PostAsync("/Assets/Edit", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "Id", asset.Id.ToString() },
                { "AssetTag", "ENTITY-ASSET-UPDATED" },
                { "ModelId", modelId.ToString() },
                { "Status", ((int)AssetStatus.Idle).ToString() },
                { "CompanyEntityId", anotherEntityId.ToString() },
                { "__RequestVerificationToken", editToken }
            }));
        }

        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var updatedAsset = await db.Assets.Include(a => a.CompanyEntity).FirstAsync(a => a.AssetTag == "ENTITY-ASSET-UPDATED");
            Assert.AreEqual(anotherEntityId, updatedAsset.CompanyEntityId);
            Assert.AreEqual("Another Entity", updatedAsset.CompanyEntity!.CompanyName);
        }
    }

    [TestMethod]
    public async Task MyAssetsDetailsTest()
    {
        // 1. Login as admin to setup
        var loginToken = await GetAntiCsrfToken("/Account/Login");
        await _http.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "EmailOrUserName", "admin" },
            { "Password", "Admin@123456!" },
            { "__RequestVerificationToken", loginToken }
        }));

        // 2. Create Category, Model and User
        int modelId;
        string user1Id;
        string user1Email = "user1@aiursoft.com";
        string user2Email = "user2@aiursoft.com";
        string password = "Test-Password-123";

        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

            var cat = new AssetCategory { Name = "DetailsTest Cat", Code = "DTC" };
            db.AssetCategories.Add(cat);
            await db.SaveChangesAsync();

            var model = new AssetModel { ModelName = "DetailsTest Model", Brand = "Test", CategoryId = cat.Id };
            db.AssetModels.Add(model);
            await db.SaveChangesAsync();
            modelId = model.Id;

            var u1 = new User { UserName = "user1", Email = user1Email, DisplayName = "User 1", AvatarRelativePath = User.DefaultAvatarPath };
            await userManager.CreateAsync(u1, password);
            user1Id = u1.Id;

            var u2 = new User { UserName = "user2", Email = user2Email, DisplayName = "User 2", AvatarRelativePath = User.DefaultAvatarPath };
            await userManager.CreateAsync(u2, password);
        }

        // 3. Create Asset assigned to user1
        var createAssetToken = await GetAntiCsrfToken("/Assets/Create");
        await _http.PostAsync("/Assets/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "AssetTag", "U1-ASSET" },
            { "ModelId", modelId.ToString() },
            { "Status", ((int)AssetStatus.InUse).ToString() },
            { "AssigneeId", user1Id },
            { "__RequestVerificationToken", createAssetToken }
        }));

        Guid assetId;
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            assetId = (await db.Assets.FirstAsync(a => a.AssetTag == "U1-ASSET")).Id;
        }

        // 4. Login as user1 and view details
        await _http.GetAsync("/Account/LogOff");
        loginToken = await GetAntiCsrfToken("/Account/Login");
        await _http.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "EmailOrUserName", user1Email },
            { "Password", password },
            { "__RequestVerificationToken", loginToken }
        }));

        var detailsResponse = await _http.GetAsync($"/MyAssets/Details/{assetId}");
        detailsResponse.EnsureSuccessStatusCode();
        var detailsHtml = await detailsResponse.Content.ReadAsStringAsync();
        Assert.Contains("U1-ASSET", detailsHtml);
        Assert.Contains("DetailsTest Model", detailsHtml);

        // 5. Login as user2 and try to view user1's asset details
        await _http.GetAsync("/Account/LogOff");
        loginToken = await GetAntiCsrfToken("/Account/Login");
        await _http.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "EmailOrUserName", user2Email },
            { "Password", password },
            { "__RequestVerificationToken", loginToken }
        }));

        var unauthorizedResponse = await _http.GetAsync($"/MyAssets/Details/{assetId}");
        Assert.AreEqual(HttpStatusCode.NotFound, unauthorizedResponse.StatusCode);
    }
}

