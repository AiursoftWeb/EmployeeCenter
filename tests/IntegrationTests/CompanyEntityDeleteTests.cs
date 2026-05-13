
namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class CompanyEntityDeleteTests : TestBase
{
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

            // 6. Add optional dependencies
            var server = new Server { Hostname = "test-server", CompanyEntityId = entityId };
            db.Servers.Add(server);
            
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

        // 8. Delete again - should succeed
        var deleteResponse2 = await PostForm($"/CompanyEntity/Delete/{entityId}", new Dictionary<string, string>());
        AssertRedirect(deleteResponse2, "/CompanyEntity/Manage");

        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            
            // 9. Verify entity is gone
            var entity = await db.CompanyEntities.FindAsync(entityId);
            Assert.IsNull(entity);

            // 10. Verify optional dependencies are null
            var server = await db.Servers.FirstAsync(s => s.Hostname == "test-server");
            Assert.IsNull(server.CompanyEntityId);

            var user = await db.Users.FirstAsync();
            Assert.IsNull(user.SigningEntityId);

            // 11. Verify logs are deleted
            var logsCount = await db.CompanyEntityLogs.CountAsync(l => l.CompanyEntityId == entityId);
            Assert.AreEqual(0, logsCount);

            // 12. Verify a deletion log exists (with CompanyEntityId = null)
            var deletionLog = await db.CompanyEntityLogs.OrderByDescending(l => l.LogTime).FirstAsync(l => l.Action == "Delete");
            Assert.IsNull(deletionLog.CompanyEntityId);
            StringAssert.Contains(deletionLog.Details, "Deleted company entity: Delete Dependency Test");
        }
    }
}
