using System.Net;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Services.Export;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class ExportTests : TestBase
{
    [TestMethod]
    public async Task TestExportSync()
    {
        // 1. Register and login
        var (email, password) = await RegisterAndLoginAsync();
        
        // 2. Add some data for the user
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == email);
            
            db.WeeklyReports.Add(new WeeklyReport
            {
                UserId = user.Id,
                Content = "This is a test weekly report.",
                WeekStartDate = DateTime.UtcNow.Date,
                CreateTime = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        // 3. Trigger sync via API
        var response = await PostForm("/Export/Sync", new Dictionary<string, string>());
        Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
        
        var json = await response.Content.ReadAsStringAsync();
        Assert.IsTrue(json.Contains("Synchronization complete!"));

        // 4. Verify files exist in the export directory
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == email);
            var pathResolver = scope.ServiceProvider.GetRequiredService<ExportPathResolver>();
            var exportRoot = pathResolver.GetUserExportRoot(user.Id);
            
            Assert.IsTrue(Directory.Exists(exportRoot));
            var files = Directory.GetFiles(exportRoot, "*.md", SearchOption.AllDirectories);
            Assert.IsTrue(files.Length > 0, "No markdown files were exported.");
            
            // Check if our weekly report is there
            var weeklyReportFiles = Directory.GetFiles(exportRoot, "*_*.md", SearchOption.AllDirectories)
                .Where(f => f.Contains("WeeklyReports"))
                .ToList();
            Assert.IsTrue(weeklyReportFiles.Count > 0, "Weekly report file not found in export.");
            
            var content = await File.ReadAllTextAsync(weeklyReportFiles[0]);
            Assert.IsTrue(content.Contains("This is a test weekly report."));
        }
    }
}
