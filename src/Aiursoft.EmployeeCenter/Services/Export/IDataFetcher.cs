using System.Security.Claims;
using Aiursoft.EmployeeCenter.Entities;

namespace Aiursoft.EmployeeCenter.Services.Export;

public interface IDataFetcher
{
    Task<IEnumerable<WeeklyReport>> GetVisibleWeeklyReports(ClaimsPrincipal user);
    Task<IEnumerable<Asset>> GetVisibleAssets(ClaimsPrincipal user);
    Task<IEnumerable<LeaveApplication>> GetVisibleLeaveApplications(ClaimsPrincipal user);
    Task<IEnumerable<Requirement>> GetVisibleRequirements(ClaimsPrincipal user);
    Task<IEnumerable<User>> GetVisibleUsers(ClaimsPrincipal user);
    Task<IEnumerable<Password>> GetVisiblePasswords(ClaimsPrincipal user);
    Task<IEnumerable<Blueprint>> GetVisibleBlueprints(ClaimsPrincipal user);
    Task<IEnumerable<Server>> GetVisibleServers(ClaimsPrincipal user);
    Task<IEnumerable<Service>> GetVisibleServices(ClaimsPrincipal user);
    Task<IEnumerable<Payroll>> GetVisiblePayrolls(ClaimsPrincipal user);
    Task<IEnumerable<Contract>> GetVisibleContracts(ClaimsPrincipal user);
    Task<IEnumerable<CompanyEntity>> GetVisibleCompanyEntities(ClaimsPrincipal user);
    Task<IEnumerable<FinanceAccount>> GetVisibleFinanceAccounts(ClaimsPrincipal user);
    Task<IEnumerable<Transaction>> GetVisibleTransactions(ClaimsPrincipal user);
    Task<IEnumerable<Incident>> GetVisibleIncidents(ClaimsPrincipal user);
    Task<IEnumerable<OnboardingTask>> GetVisibleOnboardingTasks(ClaimsPrincipal user);
    Task<IEnumerable<IntangibleAsset>> GetVisibleIntangibleAssets(ClaimsPrincipal user);
    Task<IEnumerable<CustomerRelationship>> GetVisibleCustomerRelationships(ClaimsPrincipal user);
    Task<IEnumerable<MarketChannel>> GetVisibleMarketChannels(ClaimsPrincipal user);
}
