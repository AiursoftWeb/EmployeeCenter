using System.Security.Claims;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.Scanner.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace Aiursoft.EmployeeCenter.Services.Export;

public class ExportService(
    IDataFetcher dataFetcher,
    MarkdownExporter markdownExporter,
    ExportPathResolver pathResolver,
    UserManager<User> userManager) : IScopedDependency
{
    public async Task ExportAllForUser(ClaimsPrincipal user, string? rootOverride = null)
    {
        var currentUser = await userManager.GetUserAsync(user);
        if (currentUser == null) return;

        var root = rootOverride ?? pathResolver.GetUserExportRoot(currentUser.Id);

        // Export everything available
        await SaveEntities(await dataFetcher.GetVisibleWeeklyReports(user), root);
        await SaveEntities(await dataFetcher.GetVisibleAssets(user), root);
        await SaveEntities(await dataFetcher.GetVisibleLeaveApplications(user), root);
        await SaveEntities(await dataFetcher.GetVisibleRequirements(user), root);
        await SaveEntities(await dataFetcher.GetVisibleUsers(user), root);
        await SaveEntities(await dataFetcher.GetVisiblePasswords(user), root);
        await SaveEntities(await dataFetcher.GetVisibleBlueprints(user), root);
        await SaveEntities(await dataFetcher.GetVisibleServers(user), root);
        await SaveEntities(await dataFetcher.GetVisibleServices(user), root);
        await SaveEntities(await dataFetcher.GetVisiblePayrolls(user), root);
        await SaveEntities(await dataFetcher.GetVisibleContracts(user), root);
        await SaveEntities(await dataFetcher.GetVisibleCompanyEntities(user), root);
        await SaveEntities(await dataFetcher.GetVisibleFinanceAccounts(user), root);
        await SaveEntities(await dataFetcher.GetVisibleTransactions(user), root);
        await SaveEntities(await dataFetcher.GetVisibleIncidents(user), root);
        await SaveEntities(await dataFetcher.GetVisibleOnboardingTasks(user), root);
        await SaveEntities(await dataFetcher.GetVisibleIntangibleAssets(user), root);
        await SaveEntities(await dataFetcher.GetVisibleCustomerRelationships(user), root);
        await SaveEntities(await dataFetcher.GetVisibleMarketChannels(user), root);
    }

    private async Task SaveEntities<T>(IEnumerable<T> entities, string root) where T : class
    {
        foreach (var entity in entities)
        {
            var content = markdownExporter.ExportToMarkdown(entity);
            var relativePath = markdownExporter.GetRelativePath(entity);
            var fullPath = Path.Combine(root, relativePath);

            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            await File.WriteAllTextAsync(fullPath, content);
        }
    }
}
