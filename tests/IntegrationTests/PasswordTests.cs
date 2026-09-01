using Aiursoft.EmployeeCenter.Services.FileStorage;
using Aiursoft.EmployeeCenter.Services;

namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class PasswordTests
{
    private readonly int _port;
    private readonly HttpClient _http;
    private IHost? _server;

    public PasswordTests()
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
    public async Task ManagePasswordTest()
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

        // 2. Create a global password
        var createToken = await GetAntiCsrfToken("/Passwords/Create");
        var passwordTitle = "Test Global Password";
        var passwordSecret = "Secret123!";
        var createContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Title", passwordTitle },
            { "Account", "admin-account" },
            { "Secret", passwordSecret },
            { "Note", "Some note" },
            { "__RequestVerificationToken", createToken }
        });
        var createResponse = await _http.PostAsync("/Passwords/Create", createContent);
        Assert.AreEqual(HttpStatusCode.Found, createResponse.StatusCode);

        // 3. Verify password in Index
        var indexResponse = await _http.GetAsync("/Passwords/Index");
        indexResponse.EnsureSuccessStatusCode();
        var indexHtml = await indexResponse.Content.ReadAsStringAsync();
        StringAssert.Contains(indexHtml, passwordTitle);

        // 4. Create a normal user
        var uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8);
        var userName = $"user-{uniqueId}";
        var email = $"{userName}@aiursoft.com";
        var userPassword = "Test-Password-123";

        await _http.GetAsync("/Account/LogOff");
        var registerToken = await GetAntiCsrfToken("/Account/Register");
        await _http.PostAsync("/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Email", email },
            { "Password", userPassword },
            { "ConfirmPassword", userPassword },
            { "__RequestVerificationToken", registerToken }
        }));

        string userId;
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == email);
            userId = user.Id;
        }

        // 5. Verify normal user cannot see the password yet
        await _http.GetAsync("/Account/LogOff");
        loginToken = await GetAntiCsrfToken("/Account/Login");
        await _http.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "EmailOrUserName", email },
            { "Password", userPassword },
            { "__RequestVerificationToken", loginToken }
        }));

        indexResponse = await _http.GetAsync("/Passwords/Index");
        indexHtml = await indexResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain(passwordTitle, indexHtml);

        // 6. Login as admin again to share the password
        await _http.GetAsync("/Account/LogOff");
        loginToken = await GetAntiCsrfToken("/Account/Login");
        await _http.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "EmailOrUserName", "admin" },
            { "Password", "Admin@123456!" },
            { "__RequestVerificationToken", loginToken }
        }));

        Guid passwordId;
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var p = await db.Passwords.FirstAsync(p => p.Title == passwordTitle);
            Assert.AreNotEqual(passwordSecret, p.Secret);
            StringAssert.StartsWith(p.Secret, "protected:v1:");
            Assert.AreNotEqual("Some note", p.Note);
            StringAssert.StartsWith(p.Note, "protected:v1:");
            passwordId = p.Id;
        }

        var shareToken = await GetAntiCsrfToken("/Passwords/ManageShares/" + passwordId);
        var shareContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "TargetUserId", userId },
            { "Permission", "0" }, // ReadOnly
            { "__RequestVerificationToken", shareToken }
        });
        var shareResponse = await _http.PostAsync("/Passwords/AddShare/" + passwordId, shareContent);
        Assert.AreEqual(HttpStatusCode.Found, shareResponse.StatusCode);

        // 7. Login as normal user and verify they can see it now
        await _http.GetAsync("/Account/LogOff");
        loginToken = await GetAntiCsrfToken("/Account/Login");
        await _http.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "EmailOrUserName", email },
            { "Password", userPassword },
            { "__RequestVerificationToken", loginToken }
        }));

        indexResponse = await _http.GetAsync("/Passwords/Index");
        indexHtml = await indexResponse.Content.ReadAsStringAsync();
        StringAssert.Contains(indexHtml, passwordTitle);

        // 8. Verify they can see details but NOT edit
        var detailsResponse = await _http.GetAsync("/Passwords/Details/" + passwordId);
        detailsResponse.EnsureSuccessStatusCode();
        var detailsHtml = await detailsResponse.Content.ReadAsStringAsync();
        StringAssert.Contains(detailsHtml, passwordSecret);
        StringAssert.Contains(detailsHtml, "Some note");
        Assert.DoesNotContain("Edit Password", detailsHtml); // Should not have Edit button if ReadOnly

        var editResponse = await _http.GetAsync("/Passwords/Edit/" + passwordId);
        Assert.AreEqual(HttpStatusCode.Unauthorized, editResponse.StatusCode);

        // 9. Login as admin again to change permission to Editable
        await _http.GetAsync("/Account/LogOff");
        loginToken = await GetAntiCsrfToken("/Account/Login");
        await _http.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "EmailOrUserName", "admin" },
            { "Password", "Admin@123456!" },
            { "__RequestVerificationToken", loginToken }
        }));

        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var s = await db.PasswordShares.FirstAsync(s => s.PasswordId == passwordId && s.SharedWithUserId == userId);

            // Remove ReadOnly share and add Editable share
            db.PasswordShares.Remove(s);
            await db.SaveChangesAsync();
        }

        shareToken = await GetAntiCsrfToken("/Passwords/ManageShares/" + passwordId);
        shareContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "TargetUserId", userId },
            { "Permission", "1" }, // Editable
            { "__RequestVerificationToken", shareToken }
        });
        await _http.PostAsync("/Passwords/AddShare/" + passwordId, shareContent);

        // 10. Login as normal user and verify they can now edit
        await _http.GetAsync("/Account/LogOff");
        loginToken = await GetAntiCsrfToken("/Account/Login");
        await _http.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "EmailOrUserName", email },
            { "Password", userPassword },
            { "__RequestVerificationToken", loginToken }
        }));

        editResponse = await _http.GetAsync("/Passwords/Edit/" + passwordId);
        editResponse.EnsureSuccessStatusCode();

        var editToken = await GetAntiCsrfToken("/Passwords/Edit/" + passwordId);
        var newTitle = "Updated Password Title";
        var editPostContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Id", passwordId.ToString() },
            { "Title", newTitle },
            { "Secret", "NewSecret123!" },
            { "__RequestVerificationToken", editToken }
        });
        var editPostResponse = await _http.PostAsync("/Passwords/Edit", editPostContent);
        Assert.AreEqual(HttpStatusCode.Found, editPostResponse.StatusCode);

        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var p = await db.Passwords.FindAsync(passwordId);
            Assert.AreEqual(newTitle, p!.Title);
            Assert.AreNotEqual("NewSecret123!", p.Secret);
            StringAssert.StartsWith(p.Secret, "protected:v1:");
        }
    }

    [TestMethod]
    public async Task LegacyPlaintextSecretsAreMigratedExactlyOnce()
    {
        const string legacySecret = "legacy-plaintext-secret";
        const string legacyNote = "legacy plaintext note";

        using var scope = _server!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
        var admin = await db.Users.FirstAsync(user => user.UserName == "admin");
        var password = new Password
        {
            Title = "Legacy password",
            Secret = legacySecret,
            Note = legacyNote,
            CreatorId = admin.Id
        };
        db.Passwords.Add(password);
        await db.SaveChangesAsync();

        var secretService = scope.ServiceProvider.GetRequiredService<PasswordSecretService>();
        Assert.AreEqual(2, await secretService.MigrateLegacySecretsAsync());

        var protectedValue = password.Secret;
        Assert.AreNotEqual(legacySecret, protectedValue);
        StringAssert.StartsWith(protectedValue, "protected:v1:");
        Assert.AreEqual(legacySecret, secretService.UnprotectStoredValue(protectedValue));
        Assert.AreNotEqual(legacyNote, password.Note);
        StringAssert.StartsWith(password.Note, "protected:v1:");
        Assert.AreEqual(legacyNote, secretService.UnprotectStoredNote(password.Note));

        Assert.AreEqual(0, await secretService.MigrateLegacySecretsAsync());
        Assert.AreEqual(protectedValue, password.Secret);
    }

    [TestMethod]
    public async Task TestPasswordWithAttachment()
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

        // 2. Create a dummy file in the Vault to pass validation
        string logicalPath = "password-attachments/test-file.txt";
        using (var scope = _server!.Services.CreateScope())
        {
            var folders = scope.ServiceProvider.GetRequiredService<FeatureFoldersProvider>();
            var vaultRoot = folders.GetVaultFolder();
            var physicalPath = Path.Combine(vaultRoot, logicalPath);
            Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);
            await File.WriteAllTextAsync(physicalPath, "test content");
        }

        // 3. Create a password with attachment
        var createToken = await GetAntiCsrfToken("/Passwords/Create");
        var createContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Title", "Password with Attachment" },
            { "Secret", "Secret123!" },
            { "FilePath", logicalPath },
            { "__RequestVerificationToken", createToken }
        });
        var createResponse = await _http.PostAsync("/Passwords/Create", createContent);
        Assert.AreEqual(HttpStatusCode.Found, createResponse.StatusCode);

        Guid passwordId;
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var p = await db.Passwords.FirstAsync(p => p.Title == "Password with Attachment");
            Assert.AreEqual(logicalPath, p.FilePath);
            passwordId = p.Id;
        }

        // 4. Verify attachment link in Details
        var detailsResponse = await _http.GetAsync("/Passwords/Details/" + passwordId);
        detailsResponse.EnsureSuccessStatusCode();
        var detailsHtml = await detailsResponse.Content.ReadAsStringAsync();
        StringAssert.Contains(detailsHtml, "Download Attachment");
        StringAssert.Contains(detailsHtml, "/download-private/password-attachments/test-file.txt");

        // 5. Edit password and change/remove attachment
        var editToken = await GetAntiCsrfToken("/Passwords/Edit/" + passwordId);
        var editContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Id", passwordId.ToString() },
            { "Title", "Updated Password" },
            { "Secret", "NewSecret123!" },
            { "FilePath", "" }, // Remove attachment
            { "__RequestVerificationToken", editToken }
        });
        var editResponse = await _http.PostAsync("/Passwords/Edit", editContent);
        Assert.AreEqual(HttpStatusCode.Found, editResponse.StatusCode);

        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var p = await db.Passwords.FindAsync(passwordId);
            Assert.IsNull(p!.FilePath);
        }
    }
}
