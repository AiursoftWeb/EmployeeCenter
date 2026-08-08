using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Services.FileStorage;

namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class ContractTests
{
    private readonly int _port;
    private readonly HttpClient _http;
    private IHost? _server;

    public ContractTests()
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
        // Use a simpler regex that doesn't depend on many quotes
        var match = Regex.Match(html, @"__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""");
        if (!match.Success)
        {
            throw new InvalidOperationException($"Could not find anti-CSRF token on page: {url}");
        }

        return match.Groups[1].Value;
    }

    private T GetService<T>() where T : notnull
    {
        if (_server == null) throw new InvalidOperationException("Server is not started.");
        return _server.Services.GetRequiredService<T>();
    }

    [TestMethod]
    public async Task ManageContractTest()
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

        // 2. Create a PUBLIC contract
        // First upload the file via vault framework to get the logical path
        var createPublicContractToken = await GetAntiCsrfToken("/ManageContract/Create");

        string publicFilePath;
        using (var uploadContent = new MultipartFormDataContent())
        {
            var fileContent = new ByteArrayContent([1, 2, 3]);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
            uploadContent.Add(fileContent, "file", "policy.pdf");

            var storage = GetService<StorageService>();
            var uploadUrl = storage.GetUploadUrl("contract", isVault: false);
            var uploadResponse = await _http.PostAsync(uploadUrl, uploadContent);
            uploadResponse.EnsureSuccessStatusCode();
            var uploadResult = await uploadResponse.Content.ReadAsStringAsync();
            // Extract the Path from JSON response: {"Path":"contract/2026/01/15/policy.pdf","InternetPath":"..."}
            var pathMatch = Regex.Match(uploadResult, @"""Path"":""([^""]+)""");
            Assert.IsTrue(pathMatch.Success, "Failed to extract file path from upload response");
            publicFilePath = pathMatch.Groups[1].Value;
        }

        // Now submit the form with the logical path
        var createPublicContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Name", "Public Company Policy" },
            { "FilePath", publicFilePath },
            { "Status", "1" }, // Active
            { "IsPublic", "true" },
            { "__RequestVerificationToken", createPublicContractToken }
        });
        var createContractResponse = await _http.PostAsync("/ManageContract/Create", createPublicContent);
        Assert.AreEqual(HttpStatusCode.Found, createContractResponse.StatusCode);

        // 3. Create a PRIVATE contract
        // First upload the file via vault framework to get the logical path
        var createPrivateContractToken = await GetAntiCsrfToken("/ManageContract/Create");

        string privateFilePath;
        using (var uploadContent = new MultipartFormDataContent())
        {
            var fileContent = new ByteArrayContent([1, 2, 3]);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
            uploadContent.Add(fileContent, "file", "secret.pdf");

            var storage = GetService<StorageService>();
            var uploadUrl = storage.GetUploadUrl("contract", isVault: false);
            var uploadResponse = await _http.PostAsync(uploadUrl, uploadContent);
            uploadResponse.EnsureSuccessStatusCode();
            var uploadResult = await uploadResponse.Content.ReadAsStringAsync();
            var pathMatch = Regex.Match(uploadResult, @"""Path"":""([^""]+)""");
            Assert.IsTrue(pathMatch.Success, "Failed to extract file path from upload response");
            privateFilePath = pathMatch.Groups[1].Value;
        }

        // Now submit the form with the logical path
        var createPrivateContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Name", "Private Secret Document" },
            { "FilePath", privateFilePath },
            { "Status", "1" }, // Active
            { "IsPublic", "false" },
            { "__RequestVerificationToken", createPrivateContractToken }
        });
        var createPrivateResponse = await _http.PostAsync("/ManageContract/Create", createPrivateContent);
        Assert.AreEqual(HttpStatusCode.Found, createPrivateResponse.StatusCode);

        // 4. Create a normal user
        var uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8);
        var userName = $"user-{uniqueId}";
        var email = $"{userName}@aiursoft.com";
        var password = "Test-Password-123";

        // Log off admin first to register
        await _http.GetAsync("/Account/LogOff");

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

        // 5. Log in as the normal user and verify visibility
        await _http.GetAsync("/Account/LogOff");

        loginToken = await GetAntiCsrfToken("/Account/Login");
        loginResponse = await _http.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "EmailOrUserName", email },
            { "Password", password },
            { "__RequestVerificationToken", loginToken }
        }));
        Assert.AreEqual(HttpStatusCode.Found, loginResponse.StatusCode);

        var myContractsResponse = await _http.GetAsync("/Contract/Index");
        myContractsResponse.EnsureSuccessStatusCode();
        var myContractsHtml = await myContractsResponse.Content.ReadAsStringAsync();

        // Should see public contract
        Assert.Contains("Public Company Policy", myContractsHtml);
        // Should NOT see private contract
        Assert.DoesNotContain("Private Secret Document", myContractsHtml);

        // 6. Log in as admin and verify visibility
        await _http.GetAsync("/Account/LogOff");
        loginToken = await GetAntiCsrfToken("/Account/Login");
        await _http.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "EmailOrUserName", "admin" },
            { "Password", "Admin@123456!" },
            { "__RequestVerificationToken", loginToken }
        }));

        var adminContractsResponse = await _http.GetAsync("/Contract/Index");
        adminContractsResponse.EnsureSuccessStatusCode();
        var adminContractsHtml = await adminContractsResponse.Content.ReadAsStringAsync();

        // Should see both
        Assert.Contains("Public Company Policy", adminContractsHtml);
        Assert.Contains("Private Secret Document", adminContractsHtml);

        // 7. Verify Manage page
        var manageResponse = await _http.GetAsync("/ManageContract/Index");
        manageResponse.EnsureSuccessStatusCode();
        var manageHtml = await manageResponse.Content.ReadAsStringAsync();
        Assert.Contains("Public Company Policy", manageHtml);
        Assert.Contains("Private Secret Document", manageHtml);
    }

    [TestMethod]
    public async Task ManageFolderTest()
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

        // 2. Create Root Folder A
        var createFolderToken = await GetAntiCsrfToken("/ManageContract/CreateFolder");
        var createFolderAContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Name", "Folder A" },
            { "__RequestVerificationToken", createFolderToken }
        });
        var createFolderAResponse = await _http.PostAsync("/ManageContract/CreateFolder", createFolderAContent);
        Assert.AreEqual(HttpStatusCode.Found, createFolderAResponse.StatusCode);

        // Get Folder A Id
        var dbContext = GetService<EmployeeCenterDbContext>();
        var folderA = await dbContext.ContractFolders.FirstAsync(f => f.Name == "Folder A");

        // 3. Create Sub Folder A1 inside Folder A
        var createSubFolderToken = await GetAntiCsrfToken($"/ManageContract/CreateFolder/{folderA.Id}");
        var createSubFolderA1Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Name", "Sub A1" },
            { "ParentFolderId", folderA.Id.ToString() },
            { "__RequestVerificationToken", createSubFolderToken }
        });
        var createSubFolderA1Response = await _http.PostAsync("/ManageContract/CreateFolder", createSubFolderA1Content);
        Assert.AreEqual(HttpStatusCode.Found, createSubFolderA1Response.StatusCode);

        var subA1 = await dbContext.ContractFolders.FirstAsync(f => f.Name == "Sub A1");
        Assert.AreEqual(folderA.Id, subA1.ParentFolderId);

        // 4. Create a contract in Sub A1
        var createContractToken = await GetAntiCsrfToken($"/ManageContract/Create?folderId={subA1.Id}");
        var createContractContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Name", "Contract in Sub A1" },
            { "FilePath", "contract/mock-path.pdf" },
            { "Status", "1" },
            { "FolderId", subA1.Id.ToString() },
            { "__RequestVerificationToken", createContractToken }
        });
        var createContractResponse = await _http.PostAsync("/ManageContract/Create", createContractContent);
        Assert.AreEqual(HttpStatusCode.Found, createContractResponse.StatusCode);

        // 5. Verify navigation
        var subA1IndexResponse = await _http.GetAsync($"/ManageContract/Index/{subA1.Id}");
        var subA1IndexHtml = await subA1IndexResponse.Content.ReadAsStringAsync();
        Assert.Contains("Contract in Sub A1", subA1IndexHtml);

        // 6. Test Circular Reference: Move Folder A into Sub A1
        var editFolderAToken = await GetAntiCsrfToken($"/ManageContract/EditFolder/{folderA.Id}");
        var moveAtoSubA1Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Id", folderA.Id.ToString() },
            { "Name", "Folder A renamed" },
            { "ParentFolderId", subA1.Id.ToString() },
            { "__RequestVerificationToken", editFolderAToken }
        });
        var moveAtoSubA1Response = await _http.PostAsync("/ManageContract/EditFolder", moveAtoSubA1Content);
        // Should NOT be found (302), because it should return the view with error (200)
        Assert.AreEqual(HttpStatusCode.OK, moveAtoSubA1Response.StatusCode);
        var moveAtoSubA1Html = await moveAtoSubA1Response.Content.ReadAsStringAsync();
        Assert.Contains("Cannot move a folder to its own child!", moveAtoSubA1Html);

        // 7. Test Deletion Restriction: Delete non-empty Folder A
        var deleteFolderAToken = await GetAntiCsrfToken($"/ManageContract/Index/{folderA.ParentFolderId}");
        var deleteFolderAContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "__RequestVerificationToken", deleteFolderAToken }
        });
        var deleteFolderAResponse = await _http.PostAsync($"/ManageContract/DeleteFolder/{folderA.Id}", deleteFolderAContent);
        Assert.AreEqual(HttpStatusCode.BadRequest, deleteFolderAResponse.StatusCode);

        // 8. Clean up: Delete contract, then Sub A1, then Folder A
        var contract = await dbContext.Contracts.FirstAsync(c => c.Name == "Contract in Sub A1");
        var deleteContractToken = await GetAntiCsrfToken($"/ManageContract/Index/{subA1.Id}");
        await _http.PostAsync($"/ManageContract/Delete/{contract.Id}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "__RequestVerificationToken", deleteContractToken }
        }));

        var deleteSubA1Token = await GetAntiCsrfToken($"/ManageContract/Index/{folderA.Id}");
        await _http.PostAsync($"/ManageContract/DeleteFolder/{subA1.Id}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "__RequestVerificationToken", deleteSubA1Token }
        }));

        var deleteFolderAFinalToken = await GetAntiCsrfToken("/ManageContract/Index");
        var deleteFolderAFinalResponse = await _http.PostAsync($"/ManageContract/DeleteFolder/{folderA.Id}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "__RequestVerificationToken", deleteFolderAFinalToken }
        }));
        Assert.AreEqual(HttpStatusCode.Found, deleteFolderAFinalResponse.StatusCode);

        Assert.IsFalse(await dbContext.ContractFolders.AnyAsync(f => f.Id == folderA.Id));
    }

    [TestMethod]
    public async Task MoveContractTest()
    {
        var loginToken = await GetAntiCsrfToken("/Account/Login");
        var loginResponse = await _http.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "EmailOrUserName", "admin" },
            { "Password", "Admin@123456!" },
            { "__RequestVerificationToken", loginToken }
        }));
        Assert.AreEqual(HttpStatusCode.Found, loginResponse.StatusCode);

        int contractId;
        int rootAId;
        int rootBId;
        int childId;
        int grandchildId;
        using (var scope = _server!.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var rootA = new ContractFolder { Name = "Move Root A" };
            var rootB = new ContractFolder { Name = "Move Root B" };
            context.ContractFolders.AddRange(rootA, rootB);
            await context.SaveChangesAsync();

            var child = new ContractFolder { Name = "Move Child", ParentFolderId = rootA.Id };
            context.ContractFolders.Add(child);
            await context.SaveChangesAsync();

            var grandchild = new ContractFolder { Name = "Move Grandchild", ParentFolderId = child.Id };
            var contract = new Contract
            {
                Name = "Contract To Move",
                FilePath = "contract/move-test.pdf",
                Status = ContractStatus.Active,
                IsPublic = false
            };
            context.AddRange(grandchild, contract);
            await context.SaveChangesAsync();

            contractId = contract.Id;
            rootAId = rootA.Id;
            rootBId = rootB.Id;
            childId = child.Id;
            grandchildId = grandchild.Id;
        }

        // The contract list exposes the move entry to an administrator.
        var indexResponse = await _http.GetAsync("/ManageContract/Index");
        indexResponse.EnsureSuccessStatusCode();
        var indexHtml = await indexResponse.Content.ReadAsStringAsync();
        Assert.Contains("Contract To Move", indexHtml);
        Assert.Contains($"/ManageContract/Move/{contractId}", indexHtml);

        // Root browsing only shows direct root folders.
        var rootMoveResponse = await _http.GetAsync($"/ManageContract/Move/{contractId}");
        rootMoveResponse.EnsureSuccessStatusCode();
        var rootMoveHtml = await rootMoveResponse.Content.ReadAsStringAsync();
        Assert.Contains("Move Contract", rootMoveHtml);
        Assert.Contains("Contract To Move", rootMoveHtml);
        Assert.Contains("Move Root A", rootMoveHtml);
        Assert.Contains("Move Root B", rootMoveHtml);
        Assert.DoesNotContain("Move Child", rootMoveHtml);
        Assert.DoesNotContain("Move Grandchild", rootMoveHtml);

        // Browsing deeper shows direct children and a complete root-first breadcrumb.
        var childMoveResponse = await _http.GetAsync(
            $"/ManageContract/Move/{contractId}?browseFolderId={childId}");
        childMoveResponse.EnsureSuccessStatusCode();
        var childMoveHtml = await childMoveResponse.Content.ReadAsStringAsync();
        Assert.Contains("Move Grandchild", childMoveHtml);
        Assert.DoesNotContain("Move Root B", childMoveHtml);
        var rootPosition = childMoveHtml.IndexOf(">Root<", StringComparison.Ordinal);
        var rootAPosition = childMoveHtml.IndexOf(">Move Root A<", StringComparison.Ordinal);
        var childPosition = childMoveHtml.IndexOf(">Move Child<", StringComparison.Ordinal);
        Assert.IsTrue(rootPosition >= 0 && rootPosition < rootAPosition && rootAPosition < childPosition);

        var grandchildMoveResponse = await _http.GetAsync(
            $"/ManageContract/Move/{contractId}?browseFolderId={grandchildId}");
        grandchildMoveResponse.EnsureSuccessStatusCode();
        var grandchildMoveHtml = await grandchildMoveResponse.Content.ReadAsStringAsync();
        rootPosition = grandchildMoveHtml.IndexOf(">Root<", StringComparison.Ordinal);
        rootAPosition = grandchildMoveHtml.IndexOf(">Move Root A<", StringComparison.Ordinal);
        childPosition = grandchildMoveHtml.IndexOf(">Move Child<", StringComparison.Ordinal);
        var grandchildPosition = grandchildMoveHtml.IndexOf(">Move Grandchild<", StringComparison.Ordinal);
        Assert.IsTrue(rootPosition >= 0 && rootPosition < rootAPosition && rootAPosition < childPosition &&
                      childPosition < grandchildPosition);
        Assert.Contains("No subfolders here.", grandchildMoveHtml);

        Assert.AreEqual(
            HttpStatusCode.NotFound,
            (await _http.GetAsync($"/ManageContract/Move/{contractId}?browseFolderId=2147483647")).StatusCode);
        Assert.AreEqual(
            HttpStatusCode.NotFound,
            (await _http.GetAsync("/ManageContract/Move/2147483647")).StatusCode);

        // A valid anti-forgery token moves the contract to a nested folder.
        var moveToken = await GetAntiCsrfToken(
            $"/ManageContract/Move/{contractId}?browseFolderId={grandchildId}");
        var moveResponse = await _http.PostAsync($"/ManageContract/Move/{contractId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "targetFolderId", grandchildId.ToString() },
                { "__RequestVerificationToken", moveToken }
            }));
        Assert.AreEqual(HttpStatusCode.Found, moveResponse.StatusCode);
        Assert.Contains(grandchildId.ToString(), moveResponse.Headers.Location?.OriginalString ?? string.Empty);

        using (var scope = _server.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            Assert.AreEqual(grandchildId, (await context.Contracts.FindAsync(contractId))?.FolderId);
        }

        // Invalid identifiers and missing anti-forgery tokens cannot alter the current folder.
        moveToken = await GetAntiCsrfToken($"/ManageContract/Move/{contractId}");
        var invalidTargetResponse = await _http.PostAsync($"/ManageContract/Move/{contractId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "targetFolderId", "2147483647" },
                { "__RequestVerificationToken", moveToken }
            }));
        Assert.AreEqual(HttpStatusCode.NotFound, invalidTargetResponse.StatusCode);

        var invalidContractResponse = await _http.PostAsync("/ManageContract/Move/2147483647",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "targetFolderId", rootAId.ToString() },
                { "__RequestVerificationToken", moveToken }
            }));
        Assert.AreEqual(HttpStatusCode.NotFound, invalidContractResponse.StatusCode);

        var missingTokenResponse = await _http.PostAsync($"/ManageContract/Move/{contractId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "targetFolderId", rootBId.ToString() }
            }));
        Assert.AreEqual(HttpStatusCode.BadRequest, missingTokenResponse.StatusCode);

        using (var scope = _server.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            Assert.AreEqual(grandchildId, (await context.Contracts.FindAsync(contractId))?.FolderId);
        }

        // Moving to the current folder is an idempotent success.
        moveToken = await GetAntiCsrfToken(
            $"/ManageContract/Move/{contractId}?browseFolderId={grandchildId}");
        var idempotentResponse = await _http.PostAsync($"/ManageContract/Move/{contractId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "targetFolderId", grandchildId.ToString() },
                { "__RequestVerificationToken", moveToken }
            }));
        Assert.AreEqual(HttpStatusCode.Found, idempotentResponse.StatusCode);

        // An empty target moves the contract back to the root.
        moveToken = await GetAntiCsrfToken($"/ManageContract/Move/{contractId}");
        var moveToRootResponse = await _http.PostAsync($"/ManageContract/Move/{contractId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "targetFolderId", string.Empty },
                { "__RequestVerificationToken", moveToken }
            }));
        Assert.AreEqual(HttpStatusCode.Found, moveToRootResponse.StatusCode);
        using (var scope = _server.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            Assert.IsNull((await context.Contracts.FindAsync(contractId))?.FolderId);
        }

        // A viewer without CanCreateContract cannot see, browse, or submit the move action.
        const string viewerPassword = "Test-Password-123";
        var viewerEmail = $"contract-viewer-{Guid.NewGuid():N}@aiursoft.com";
        using (var scope = _server.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var viewer = new User
            {
                UserName = viewerEmail,
                Email = viewerEmail,
                DisplayName = "Contract Viewer",
                AvatarRelativePath = User.DefaultAvatarPath
            };
            Assert.IsTrue((await userManager.CreateAsync(viewer, viewerPassword)).Succeeded);

            const string roleName = "ContractMoveViewer";
            var role = new IdentityRole(roleName);
            Assert.IsTrue((await roleManager.CreateAsync(role)).Succeeded);
            Assert.IsTrue((await roleManager.AddClaimAsync(
                role,
                new Claim(AppPermissions.Type, AppPermissionNames.CanViewContractHistory))).Succeeded);
            Assert.IsTrue((await userManager.AddToRoleAsync(viewer, roleName)).Succeeded);
        }

        await _http.GetAsync("/Account/LogOff");
        loginToken = await GetAntiCsrfToken("/Account/Login");
        loginResponse = await _http.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "EmailOrUserName", viewerEmail },
            { "Password", viewerPassword },
            { "__RequestVerificationToken", loginToken }
        }));
        Assert.AreEqual(HttpStatusCode.Found, loginResponse.StatusCode);

        indexResponse = await _http.GetAsync("/ManageContract/Index");
        indexResponse.EnsureSuccessStatusCode();
        indexHtml = await indexResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain($"/ManageContract/Move/{contractId}", indexHtml);

        var forbiddenGetResponse = await _http.GetAsync($"/ManageContract/Move/{contractId}");
        Assert.IsTrue(forbiddenGetResponse.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Found);

        var viewerToken = await GetAntiCsrfToken("/Manage/ChangePassword");
        var forbiddenPostResponse = await _http.PostAsync($"/ManageContract/Move/{contractId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "targetFolderId", rootAId.ToString() },
                { "__RequestVerificationToken", viewerToken }
            }));
        Assert.IsTrue(forbiddenPostResponse.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Found);
        using (var scope = _server.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            Assert.IsNull((await context.Contracts.FindAsync(contractId))?.FolderId);
        }
    }
}
