
namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class CompanyEntityDeleteTests : TestBase
{
    [TestMethod]
    public async Task DeleteCompanyEntity_WithRetiredQualification_PreservesTraceability()
    {
        await LoginAsAdmin();

        int entityId;
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var entity = new CompanyEntity
            {
                CompanyName = "Qualification retention test",
                EntityCode = "QUALIFICATION-RETENTION"
            };
            db.CompanyEntities.Add(entity);
            await db.SaveChangesAsync();
            entityId = entity.Id;
            db.BusinessQualifications.Add(new BusinessQualification
            {
                CompanyEntityId = entityId,
                Name = "Business license",
                Version = "v1",
                OriginalPath = "qualifications/originals/retained.png",
                OriginalSha256 = new string('a', 64),
                CreatedById = "qualification-admin",
                IsActive = false
            });
            await db.SaveChangesAsync();
        }

        var response = await PostForm($"/CompanyEntity/Delete/{entityId}", new Dictionary<string, string>());
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        StringAssert.Contains(await response.Content.ReadAsStringAsync(),
            "1 business qualifications (retained for traceability)");

        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            Assert.IsNotNull(await db.CompanyEntities.FindAsync(entityId));
            Assert.AreEqual(1, await db.BusinessQualifications.CountAsync(q => q.CompanyEntityId == entityId));
        }
    }

    [TestMethod]
    public async Task DeleteCompanyEntity_WithDependencies_HandlesCorrectly()
    {
        // 1. Login as admin
        await LoginAsAdmin();

        // 2. Create a Company Entity
        var createResponse = await PostForm("/CompanyEntity/Create", new Dictionary<string, string>
        {
            { "CompanyName", "Delete Dependency Test" },
            { "EntityCode", "DEP123" }
        });
        Assert.AreEqual(HttpStatusCode.Found, createResponse.StatusCode);

        int entityId;
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var entity = await db.CompanyEntities.FirstAsync(e => e.EntityCode == "DEP123");
            entityId = entity.Id;

            // 3. Add a required dependency (FinanceAccount)
            var account = new FinanceAccount
            {
                AccountName = "Test Account",
                CompanyEntityId = entityId,
                Currency = "CNY"
            };
            db.FinanceAccounts.Add(account);
            await db.SaveChangesAsync();
        }

        // 4. Try to delete - should fail with error message
        var deleteResponse = await PostForm($"/CompanyEntity/Delete/{entityId}", new Dictionary<string, string>());
        Assert.AreEqual(HttpStatusCode.OK, deleteResponse.StatusCode); // Returns view with error
        var html = await deleteResponse.Content.ReadAsStringAsync();
        StringAssert.Contains(html, "Cannot delete this company entity because it is referenced by: 1 finance accounts (Ledger)");

        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();

            // 5. Remove the required dependency
            var account = await db.FinanceAccounts.FirstAsync(a => a.CompanyEntityId == entityId);
            db.FinanceAccounts.Remove(account);
            await db.SaveChangesAsync();

            // 6. Add infrastructure dependencies, which must be reassigned through
            // the infrastructure management surface rather than changed here.
            var server = new Server { Hostname = "test-server", CompanyEntityId = entityId };
            db.Servers.Add(server);

            var service = new Service
            {
                Name = "Test Service",
                PrimaryDomain = "delete-dependency.example.com",
                CompanyEntityId = entityId
            };
            db.Services.Add(service);

            var user = await db.Users.FirstAsync();
            user.SigningEntityId = entityId;

            // 7. Add some logs
            var log = new CompanyEntityLog
            {
                CompanyEntityId = entityId,
                UserId = user.Id,
                Action = "Test Log"
            };
            db.CompanyEntityLogs.Add(log);

            await db.SaveChangesAsync();
        }

        // 8. Delete again - infrastructure dependencies must block the operation.
        var deleteResponse2 = await PostForm($"/CompanyEntity/Delete/{entityId}", new Dictionary<string, string>());
        Assert.AreEqual(HttpStatusCode.OK, deleteResponse2.StatusCode);
        var dependencyHtml = await deleteResponse2.Content.ReadAsStringAsync();
        StringAssert.Contains(dependencyHtml, "1 servers (Infrastructure)");
        StringAssert.Contains(dependencyHtml, "1 services (Infrastructure)");

        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();

            // 9. Verify the company-entity endpoint did not mutate infrastructure.
            var server = await db.Servers.FirstAsync(s => s.Hostname == "test-server");
            Assert.AreEqual(entityId, server.CompanyEntityId);
            var service = await db.Services.FirstAsync(s => s.PrimaryDomain == "delete-dependency.example.com");
            Assert.AreEqual(entityId, service.CompanyEntityId);

            // Simulate an authorized reassignment through the infrastructure surface.
            server.CompanyEntityId = null;
            service.CompanyEntityId = null;
            await db.SaveChangesAsync();
        }

        // 10. Once infrastructure references are reassigned, deletion succeeds.
        var deleteResponse3 = await PostForm($"/CompanyEntity/Delete/{entityId}", new Dictionary<string, string>());
        AssertRedirect(deleteResponse3, "/CompanyEntity/Manage");

        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();

            // 11. Verify entity is gone and unrelated optional references are cleared.
            var entity = await db.CompanyEntities.FindAsync(entityId);
            Assert.IsNull(entity);

            var user = await db.Users.FirstAsync();
            Assert.IsNull(user.SigningEntityId);

            // 12. Verify logs are deleted
            var logsCount = await db.CompanyEntityLogs.CountAsync(l => l.CompanyEntityId == entityId);
            Assert.AreEqual(0, logsCount);

            // 13. Verify a deletion log exists (with CompanyEntityId = null)
            var deletionLog = await db.CompanyEntityLogs.OrderByDescending(l => l.LogTime).FirstAsync(l => l.Action == "Delete");
            Assert.IsNull(deletionLog.CompanyEntityId);
            StringAssert.Contains(deletionLog.Details, "Deleted company entity: Delete Dependency Test");
        }
    }
}
