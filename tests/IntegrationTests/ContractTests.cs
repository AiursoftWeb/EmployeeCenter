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
}
