using System.Security.Claims;
using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.Scanner.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.EmployeeCenter.Services.Export;

public class DataFetcher(
    EmployeeCenterDbContext dbContext,
    IAuthorizationService authorizationService,
    UserManager<User> userManager) : IDataFetcher, IScopedDependency
{
    private async Task<HashSet<string>> GetAllSubordinatesRecursivelyAsync(string userId)
    {
        var result = new HashSet<string>();
        var toProcess = new Queue<string>();
        toProcess.Enqueue(userId);

        while (toProcess.Count > 0)
        {
            var currentUserId = toProcess.Dequeue();

            var directReports = await dbContext.Users
                .Where(u => u.ManagerId == currentUserId)
                .Select(u => u.Id)
                .ToListAsync();

            foreach (var reportId in directReports)
            {
                if (!result.Contains(reportId))
                {
                    result.Add(reportId);
                    toProcess.Enqueue(reportId);
                }
            }
        }

        return result;
    }

    private async Task<bool> HasPermission(ClaimsPrincipal user, string permissionName)
    {
        return (await authorizationService.AuthorizeAsync(user, permissionName)).Succeeded;
    }

    public async Task<IEnumerable<WeeklyReport>> GetVisibleWeeklyReports(ClaimsPrincipal user)
    {
        var currentUser = await userManager.GetUserAsync(user);
        if (currentUser == null) return Enumerable.Empty<WeeklyReport>();

        if (await HasPermission(user, AppPermissionNames.CanManageAnyoneWeeklyReport))
        {
            return await dbContext.WeeklyReports
                .Include(r => r.User)
                .Include(r => r.WeeklyReportRequirements)
                .ThenInclude(req => req.Requirement)
                .AsNoTracking()
                .ToListAsync();
        }

        var subordinateIds = await GetAllSubordinatesRecursivelyAsync(currentUser.Id);
        return await dbContext.WeeklyReports
            .Include(r => r.User)
            .Include(r => r.WeeklyReportRequirements)
            .ThenInclude(req => req.Requirement)
            .Where(r => r.UserId == currentUser.Id || subordinateIds.Contains(r.UserId))
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IEnumerable<Asset>> GetVisibleAssets(ClaimsPrincipal user)
    {
        var currentUser = await userManager.GetUserAsync(user);
        if (currentUser == null) return Enumerable.Empty<Asset>();

        var query = dbContext.Assets
            .Include(a => a.Model)
            .ThenInclude(m => m.Category)
            .Include(a => a.Assignee)
            .Include(a => a.Location)
            .Include(a => a.CompanyEntity)
            .AsNoTracking();

        if (await HasPermission(user, AppPermissionNames.CanManageAssets))
        {
            return await query.ToListAsync();
        }

        return await query
            .Where(a => a.AssigneeId == currentUser.Id)
            .ToListAsync();
    }

    public async Task<IEnumerable<LeaveApplication>> GetVisibleLeaveApplications(ClaimsPrincipal user)
    {
        var currentUser = await userManager.GetUserAsync(user);
        if (currentUser == null) return Enumerable.Empty<LeaveApplication>();

        var query = dbContext.LeaveApplications
            .Include(la => la.User)
            .Include(la => la.ReviewedBy)
            .AsNoTracking();

        if (await HasPermission(user, AppPermissionNames.CanApproveAnyLeave))
        {
            return await query.ToListAsync();
        }

        var subordinateIds = await GetAllSubordinatesRecursivelyAsync(currentUser.Id);
        return await query
            .Where(la => la.UserId == currentUser.Id || subordinateIds.Contains(la.UserId))
            .ToListAsync();
    }

    public async Task<IEnumerable<Requirement>> GetVisibleRequirements(ClaimsPrincipal user)
    {
        return await dbContext.Requirements
            .Include(r => r.Comments)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IEnumerable<User>> GetVisibleUsers(ClaimsPrincipal user)
    {
        var currentUser = await userManager.GetUserAsync(user);
        if (currentUser == null) return Enumerable.Empty<User>();

        if (await HasPermission(user, AppPermissionNames.CanReadUsers) ||
            await HasPermission(user, AppPermissionNames.CanEditUsers))
        {
            return await dbContext.Users
                .AsNoTracking()
                .ToListAsync();
        }

        var subordinateIds = await GetAllSubordinatesRecursivelyAsync(currentUser.Id);
        var managerId = currentUser.ManagerId;

        return await dbContext.Users
            .Where(u => u.Id == currentUser.Id ||
                        u.Id == managerId ||
                        (managerId != null && u.ManagerId == managerId) ||
                        subordinateIds.Contains(u.Id))
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IEnumerable<Password>> GetVisiblePasswords(ClaimsPrincipal user)
    {
        var currentUser = await userManager.GetUserAsync(user);
        if (currentUser == null) return Enumerable.Empty<Password>();

        if (await HasPermission(user, AppPermissionNames.CanManageAnyPassword))
        {
            return await dbContext.Passwords
                .Include(p => p.Creator)
                .AsNoTracking()
                .ToListAsync();
        }

        var sharedPasswordIds = await dbContext.PasswordShares
            .Where(s => s.SharedWithUserId == currentUser.Id)
            .Select(s => s.PasswordId)
            .ToListAsync();

        return await dbContext.Passwords
            .Include(p => p.Creator)
            .Where(p => p.CreatorId == currentUser.Id || sharedPasswordIds.Contains(p.Id))
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IEnumerable<Blueprint>> GetVisibleBlueprints(ClaimsPrincipal user)
    {
        return await dbContext.Blueprints
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IEnumerable<Server>> GetVisibleServers(ClaimsPrincipal user)
    {
        return await dbContext.Servers
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IEnumerable<Service>> GetVisibleServices(ClaimsPrincipal user)
    {
        return await dbContext.Services
            .Include(s => s.DnsProvider)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IEnumerable<Payroll>> GetVisiblePayrolls(ClaimsPrincipal user)
    {
        var currentUser = await userManager.GetUserAsync(user);
        if (currentUser == null) return Enumerable.Empty<Payroll>();

        if (await HasPermission(user, AppPermissionNames.CanManagePayroll))
        {
            return await dbContext.Payrolls
                .Include(p => p.Owner)
                .AsNoTracking()
                .ToListAsync();
        }

        return await dbContext.Payrolls
            .Include(p => p.Owner)
            .Where(p => p.OwnerId == currentUser.Id)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IEnumerable<Contract>> GetVisibleContracts(ClaimsPrincipal user)
    {
        if (await HasPermission(user, AppPermissionNames.CanViewContractHistory))
        {
            return await dbContext.Contracts
                .AsNoTracking()
                .ToListAsync();
        }

        return await dbContext.Contracts
            .Where(c => c.IsPublic)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IEnumerable<CompanyEntity>> GetVisibleCompanyEntities(ClaimsPrincipal user)
    {
        return await dbContext.CompanyEntities
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IEnumerable<FinanceAccount>> GetVisibleFinanceAccounts(ClaimsPrincipal user)
    {
        if (await HasPermission(user, AppPermissionNames.CanManageLedger))
        {
            return await dbContext.FinanceAccounts
                .AsNoTracking()
                .ToListAsync();
        }
        return Enumerable.Empty<FinanceAccount>();
    }

    public async Task<IEnumerable<Transaction>> GetVisibleTransactions(ClaimsPrincipal user)
    {
        if (await HasPermission(user, AppPermissionNames.CanManageLedger))
        {
            return await dbContext.Transactions
                .Include(t => t.SourceAccount)
                .Include(t => t.DestinationAccount)
                .AsNoTracking()
                .ToListAsync();
        }
        return Enumerable.Empty<Transaction>();
    }

    public async Task<IEnumerable<Incident>> GetVisibleIncidents(ClaimsPrincipal user)
    {
        return await dbContext.Incidents
            .Include(i => i.Owner)
            .Include(i => i.IM)
            .Include(i => i.Comments)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IEnumerable<OnboardingTask>> GetVisibleOnboardingTasks(ClaimsPrincipal user)
    {
        if (await HasPermission(user, AppPermissionNames.CanManageOnboarding))
        {
            return await dbContext.OnboardingTasks
                .AsNoTracking()
                .ToListAsync();
        }
        return Enumerable.Empty<OnboardingTask>();
    }

    public async Task<IEnumerable<IntangibleAsset>> GetVisibleIntangibleAssets(ClaimsPrincipal user)
    {
        if (await HasPermission(user, AppPermissionNames.CanManageAssets))
        {
            return await dbContext.IntangibleAssets
                .AsNoTracking()
                .ToListAsync();
        }
        return Enumerable.Empty<IntangibleAsset>();
    }

    public async Task<IEnumerable<CustomerRelationship>> GetVisibleCustomerRelationships(ClaimsPrincipal user)
    {
        return await dbContext.CustomerRelationships
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IEnumerable<MarketChannel>> GetVisibleMarketChannels(ClaimsPrincipal user)
    {
        return await dbContext.MarketChannels
            .AsNoTracking()
            .ToListAsync();
    }
}
