namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class CustomerRelationshipsTests : TestBase
{
    [TestMethod]
    public async Task AdminCanAccessManagePage()
    {
        await LoginAsAdmin();
        var response = await Http.GetAsync("/CustomerRelationships/Manage");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task AdminCanCreateRelationship()
    {
        await LoginAsAdmin();

        var name = "Test Customer " + Guid.NewGuid();

        var response = await PostForm("/CustomerRelationships/Create", new Dictionary<string, string>
        {
            { "Name", name },
            { "Email", "test@customer.com" },
            { "Phone", "1234567890" },
            { "Address", "Test Address" },
            { "Remark", "Test Remark" }
        });

        AssertRedirect(response, "/CustomerRelationships/Manage");

        // Verify it's created
        var managePage = await Http.GetStringAsync("/CustomerRelationships/Manage");
        Assert.IsTrue(managePage.Contains(name));
    }

    [TestMethod]
    public async Task AdminCanCreateRelationshipWithNonLedgerEntity()
    {
        await LoginAsAdmin();

        // 1. Create an entity with CreateLedger = false
        var companyName = "NonLedger Company " + Guid.NewGuid();
        var entityResponse = await PostForm("/CompanyEntity/Create", new Dictionary<string, string>
        {
            { "CompanyName", companyName },
            { "EntityCode", "USC" + Guid.NewGuid().ToString().Substring(0, 8) },
            { "CreateLedger", "false" }
        });
        AssertRedirect(entityResponse, "/CompanyEntity/Manage");

        // Get the entity from DB
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var entity = await db.CompanyEntities.FirstOrDefaultAsync(e => e.CompanyName == companyName);
            Assert.IsNotNull(entity);
            Assert.IsFalse(entity.CreateLedger);

            // 2. Access Create Relationship page and check if it is listed
            var createPage = await Http.GetStringAsync("/CustomerRelationships/Create");
            Assert.IsTrue(createPage.Contains(companyName), $"The company {companyName} should be listed in the select box even if it has no ledger.");

            // 3. Create the relationship
            var relationshipName = "Test Relationship " + Guid.NewGuid();
            var createRelationshipResponse = await PostForm("/CustomerRelationships/Create", new Dictionary<string, string>
            {
                { "Name", relationshipName },
                { "Email", "test@customer.com" },
                { "CompanyEntityId", entity.Id.ToString() }
            });

            AssertRedirect(createRelationshipResponse, "/CustomerRelationships/Manage");

            // 4. Verify it's created and correctly associated
            var managePage = await Http.GetStringAsync("/CustomerRelationships/Manage");
            Assert.IsTrue(managePage.Contains(relationshipName));
            Assert.IsTrue(managePage.Contains(companyName));
        }
    }

    [TestMethod]
    public async Task UserCanAccessIndexButNotManage()
    {
        await RegisterAndLoginAsync();

        // Can access Index
        var indexResponse = await Http.GetAsync("/CustomerRelationships/Index");
        Assert.AreEqual(HttpStatusCode.OK, indexResponse.StatusCode);

        // Cannot access Manage (should redirect to Code403)
        var manageResponse = await Http.GetAsync("/CustomerRelationships/Manage");
        Assert.AreEqual(HttpStatusCode.Found, manageResponse.StatusCode);
        var location = manageResponse.Headers.Location?.ToString();
        Assert.IsTrue(location?.Contains("Code403") == true, $"Expected Code403, but got {location}");

        // Cannot access Create
        var createResponse = await Http.GetAsync("/CustomerRelationships/Create");
        Assert.AreEqual(HttpStatusCode.Found, createResponse.StatusCode);
        location = createResponse.Headers.Location?.ToString();
        Assert.IsTrue(location?.Contains("Code403") == true, $"Expected Code403, but got {location}");
    }
}
