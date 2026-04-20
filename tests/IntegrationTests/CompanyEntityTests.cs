
namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class CompanyEntityTests : TestBase
{
    [TestMethod]
    public async Task CreateAndVerifyZipCodeTest()
    {
        // 1. Login as admin
        await LoginAsAdmin();

        // 2. Create a Company Entity with ZipCode
        var createResponse = await PostForm("/CompanyEntity/Create", new Dictionary<string, string>
        {
            { "CompanyName", "Test Company" },
            { "EntityCode", "123456789" },
            { "OfficeAddress", "123 Office St" },
            { "ZipCode", "200000" }
        });
        Assert.AreEqual(HttpStatusCode.Found, createResponse.StatusCode);

        // 3. Verify in Manage page (as admin)
        var manageResponse = await Http.GetAsync("/CompanyEntity/Manage");
        manageResponse.EnsureSuccessStatusCode();
        var manageHtml = await manageResponse.Content.ReadAsStringAsync();
        Assert.Contains("Test Company", manageHtml);

        // 4. Get the ID of the newly created entity
        var match = Regex.Match(manageHtml, @"/CompanyEntity/Details/(\d+)");
        Assert.IsTrue(match.Success, "Could not find Details link for the created company entity");
        var entityId = match.Groups[1].Value;

        // 5. Verify Details page (as admin)
        var detailsResponse = await Http.GetAsync($"/CompanyEntity/Details/{entityId}");
        detailsResponse.EnsureSuccessStatusCode();
        var detailsHtml = await detailsResponse.Content.ReadAsStringAsync();
        Assert.Contains("Test Company", detailsHtml);
        Assert.Contains("200000", detailsHtml);

        // 6. Login as normal user
        await Http.GetAsync("/Account/LogOff");
        await RegisterAndLoginAsync();

        // 7. Verify Details page (as normal user)
        var userDetailsResponse = await Http.GetAsync($"/CompanyEntity/Details/{entityId}");
        userDetailsResponse.EnsureSuccessStatusCode();
        var userDetailsHtml = await userDetailsResponse.Content.ReadAsStringAsync();
        Assert.Contains("Test Company", userDetailsHtml);
        Assert.Contains("200000", userDetailsHtml);
    }

    [TestMethod]
    public async Task CreateAndVerifyLegalRepresentativeLegalNameTest()
    {
        // 1. Login as admin
        await LoginAsAdmin();

        // 2. Create a Company Entity with LegalRepresentativeLegalName
        var createResponse = await PostForm("/CompanyEntity/Create", new Dictionary<string, string>
        {
            { "CompanyName", "Legal Name Test Company" },
            { "EntityCode", "LN123456789" },
            { "LegalRepresentative", "John Doe" },
            { "LegalRepresentativeLegalName", "Johnathan Doe" }
        });
        Assert.AreEqual(HttpStatusCode.Found, createResponse.StatusCode);

        // 3. Get the ID
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var entity = await db.CompanyEntities.FirstOrDefaultAsync(e => e.EntityCode == "LN123456789");
            Assert.IsNotNull(entity);
            Assert.AreEqual("Johnathan Doe", entity.LegalRepresentativeLegalName);

            // 4. Verify Details page
            var detailsResponse = await Http.GetAsync($"/CompanyEntity/Details/{entity.Id}");
            detailsResponse.EnsureSuccessStatusCode();
            var detailsHtml = await detailsResponse.Content.ReadAsStringAsync();
            Assert.Contains("Johnathan Doe", detailsHtml);

            // 5. Verify Edit page
            var editResponse = await Http.GetAsync($"/CompanyEntity/Edit/{entity.Id}");
            editResponse.EnsureSuccessStatusCode();
            var editHtml = await editResponse.Content.ReadAsStringAsync();
            Assert.Contains("Johnathan Doe", editHtml);

            // 6. Update LegalRepresentativeLegalName
            var updateResponse = await PostForm("/CompanyEntity/Edit", new Dictionary<string, string>
            {
                { "Id", entity.Id.ToString() },
                { "CompanyName", "Legal Name Test Company" },
                { "EntityCode", "LN123456789" },
                { "LegalRepresentative", "John Doe" },
                { "LegalRepresentativeLegalName", "Johnathan Doe Updated" }
            });
            Assert.AreEqual(HttpStatusCode.Found, updateResponse.StatusCode);

            // 7. Verify in DB
            await db.Entry(entity).ReloadAsync();
            Assert.AreEqual("Johnathan Doe Updated", entity.LegalRepresentativeLegalName);
        }
    }

    [TestMethod]
    public async Task CreateCompanyEntity_WithInvalidFiles_Fails()
    {
        await LoginAsAdmin();

        var formData = new Dictionary<string, string>
        {
            { "CompanyName", "Test Company" },
            { "EntityCode", "123456" },
            { "LicensePath", "company-certs/fake-license.pdf" }, // Not existing
            { "OrganizationCertificatePath", "company-certs/fake-cert.pdf" } // Not existing
        };

        var response = await PostForm("/CompanyEntity/Create", formData);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode); // Should return View (error), not Redirect
        var html = await response.Content.ReadAsStringAsync();
        StringAssert.Contains(html, "File not found or lost");
    }

    [TestMethod]
    public async Task CreateCompanyEntity_WithNoFiles_Succeeds()
    {
        await LoginAsAdmin();

        var formData = new Dictionary<string, string>
        {
            { "CompanyName", "Test Company 2" },
            { "EntityCode", "654321" }
        };

        var response = await PostForm("/CompanyEntity/Create", formData);

        AssertRedirect(response, "/CompanyEntity/Manage");

        // Verify in DB
        using var scope = Server!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
        var entity = await db.CompanyEntities.FirstOrDefaultAsync(e => e.EntityCode == "654321");
        Assert.IsNotNull(entity);
        Assert.AreEqual("Test Company 2", entity.CompanyName);
    }

    [TestMethod]
    public async Task DetailsPage_Buttons_VisibilityTest()
    {
        // 1. Login as admin
        await LoginAsAdmin();

        // 2. Create a Company Entity
        var createResponse = await PostForm("/CompanyEntity/Create", new Dictionary<string, string>
        {
            { "CompanyName", "Button Test Company" },
            { "EntityCode", "999888777" },
            { "RegisteredAddress", "123 Button St" }
        });
        Assert.AreEqual(HttpStatusCode.Found, createResponse.StatusCode);

        // 3. Get the ID
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var entity = await db.CompanyEntities.FirstOrDefaultAsync(e => e.EntityCode == "999888777");
            Assert.IsNotNull(entity);

            // 4. Verify Details page (as admin)
            var detailsResponse = await Http.GetAsync($"/CompanyEntity/Details/{entity.Id}");
            detailsResponse.EnsureSuccessStatusCode();
            var detailsHtml = await detailsResponse.Content.ReadAsStringAsync();

            Assert.Contains("Copy Summary", detailsHtml);
            Assert.Contains("Copy Full Info", detailsHtml);
            StringAssert.DoesNotMatch(detailsHtml, new Regex("Microsoft.AspNetCore.Mvc.Localization.LocalizedHtmlString"), "Copy text contains type name instead of value!");

            // 5. Login as normal user
            await Http.GetAsync("/Account/LogOff");
            await RegisterAndLoginAsync();

            // 6. Verify Details page (as normal user)
            var userDetailsResponse = await Http.GetAsync($"/CompanyEntity/Details/{entity.Id}");
            userDetailsResponse.EnsureSuccessStatusCode();
            var userDetailsHtml = await userDetailsResponse.Content.ReadAsStringAsync();

            Assert.Contains("Copy Summary", userDetailsHtml);
            Assert.DoesNotContain("Copy Full Info", userDetailsHtml);
            StringAssert.DoesNotMatch(userDetailsHtml, new Regex("Microsoft.AspNetCore.Mvc.Localization.LocalizedHtmlString"), "Copy text contains type name instead of value!");
        }
    }

    [TestMethod]
    public async Task InvoiceGuideTest()
    {
        // 1. Login as admin
        await LoginAsAdmin();

        // 2. Create a Company Entity
        var createResponse = await PostForm("/CompanyEntity/Create", new Dictionary<string, string>
        {
            { "CompanyName", "Invoice Guide Company" },
            { "EntityCode", "INV123456" },
            { "BankName", "Guide Bank" },
            { "BankAccount", "GB123456789" },
            { "CreateLedger", "true" }
        });
        Assert.AreEqual(HttpStatusCode.Found, createResponse.StatusCode);

        // 3. Get the ID
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var entity = await db.CompanyEntities.FirstOrDefaultAsync(e => e.EntityCode == "INV123456");
            Assert.IsNotNull(entity);

            // 4. Verify Details page
            var detailsResponse = await Http.GetAsync($"/CompanyEntity/Details/{entity.Id}");
            detailsResponse.EnsureSuccessStatusCode();
            var detailsHtml = await detailsResponse.Content.ReadAsStringAsync();

            Assert.Contains("Invoice Issuance Guide", detailsHtml);
            Assert.Contains("Instructions for Employees", detailsHtml);
            Assert.Contains("Guide Bank", detailsHtml);
            Assert.Contains("GB123456789", detailsHtml);
            Assert.Contains("Prefer Electronic Invoices", detailsHtml);
        }
    }

    [TestMethod]
    public async Task StaffListVisibilityTest()
    {
        // 1. Login as admin
        await LoginAsAdmin();

        // 2. Create a Company Entity
        var createResponse = await PostForm("/CompanyEntity/Create", new Dictionary<string, string>
        {
            { "CompanyName", "Staff Test Company" },
            { "EntityCode", "ST123456" }
        });
        Assert.AreEqual(HttpStatusCode.Found, createResponse.StatusCode);

        // 3. Get the ID and update a user to be signed with this entity
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var entity = await db.CompanyEntities.FirstOrDefaultAsync(e => e.EntityCode == "ST123456");
            Assert.IsNotNull(entity);

            var user = await db.Users.FirstAsync();
            user.SigningEntityId = entity.Id;
            user.JobLevel = "Senior Engineer";
            user.Title = "Fullstack Developer";
            await db.SaveChangesAsync();

            // 4. Verify Details page
            var detailsResponse = await Http.GetAsync($"/CompanyEntity/Details/{entity.Id}");
            detailsResponse.EnsureSuccessStatusCode();
            var detailsHtml = await detailsResponse.Content.ReadAsStringAsync();

            Assert.Contains("Staff List", detailsHtml);
            Assert.Contains(user.DisplayName, detailsHtml);
            Assert.Contains("Senior Engineer", detailsHtml);
            Assert.Contains("Fullstack Developer", detailsHtml);
        }
    }
}
