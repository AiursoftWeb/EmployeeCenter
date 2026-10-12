using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.LedgerViewModels;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.EmployeeCenter.Services;

/// <summary>
/// Service for calculating account balances based on double-entry bookkeeping.
/// 
/// Accounting rules:
///   - Asset / Expense accounts:  Balance = Σ(destination inflows) − Σ(source outflows)
///   - Liability / Equity / Income accounts: Balance = Σ(source outflows) − Σ(destination inflows)
///
/// "Source outflows" uses the raw Amount (source currency).
/// "Destination inflows" uses Amount × ExchangeRate (destination currency).
/// </summary>
public class LedgerBalanceService(EmployeeCenterDbContext dbContext)
{
    /// <summary>
    /// Calculates the balance for every active (non-archived) account of the given entity,
    /// considering only transactions that occurred strictly before <paramref name="beforeTime"/>.
    /// </summary>
    public async Task<List<AccountWithBalance>> GetAccountBalancesAsync(int entityId, DateTime beforeTime)
    {
        var allActiveAccounts = await dbContext.FinanceAccounts
            .Where(a => a.CompanyEntityId == entityId && !a.IsArchived)
            .ToListAsync();

        var sourceSums = await dbContext.Transactions
            .Where(t => t.SourceAccount!.CompanyEntityId == entityId && t.TransactionTime < beforeTime)
            .GroupBy(t => t.SourceAccountId)
            .Select(g => new { AccountId = g.Key, Sum = g.Sum(t => t.Amount) })
            .ToDictionaryAsync(x => x.AccountId, x => x.Sum);

        var destinationSums = await dbContext.Transactions
            .Where(t => t.DestinationAccount!.CompanyEntityId == entityId && t.TransactionTime < beforeTime)
            .GroupBy(t => t.DestinationAccountId)
            .Select(g => new { AccountId = g.Key, Sum = g.Sum(t => t.Amount * t.ExchangeRate) })
            .ToDictionaryAsync(x => x.AccountId, x => x.Sum);

        return allActiveAccounts.Select(a => new AccountWithBalance
        {
            Account = a,
            Balance = a.AccountType switch
            {
                FinanceAccountType.Asset or FinanceAccountType.Expense =>
                    destinationSums.GetValueOrDefault(a.Id) - sourceSums.GetValueOrDefault(a.Id),
                FinanceAccountType.Liability or FinanceAccountType.Equity or FinanceAccountType.Income =>
                    sourceSums.GetValueOrDefault(a.Id) - destinationSums.GetValueOrDefault(a.Id),
                _ => 0
            }
        }).ToList();
    }

    /// <summary>
    /// Calculates the balance for ALL accounts (including archived) of the given entity.
    /// Used by the Accounts management page.
    /// </summary>
    public async Task<List<AccountWithBalance>> GetAllAccountBalancesAsync(int entityId)
    {
        var accounts = await dbContext.FinanceAccounts
            .Where(a => a.CompanyEntityId == entityId)
            .OrderBy(a => a.AccountType)
            .ThenBy(a => a.AccountName)
            .ToListAsync();

        var sourceSums = await dbContext.Transactions
            .Where(t => t.SourceAccount!.CompanyEntityId == entityId)
            .GroupBy(t => t.SourceAccountId)
            .Select(g => new { AccountId = g.Key, Sum = g.Sum(t => t.Amount) })
            .ToDictionaryAsync(x => x.AccountId, x => x.Sum);

        var destinationSums = await dbContext.Transactions
            .Where(t => t.DestinationAccount!.CompanyEntityId == entityId)
            .GroupBy(t => t.DestinationAccountId)
            .Select(g => new { AccountId = g.Key, Sum = g.Sum(t => t.Amount * t.ExchangeRate) })
            .ToDictionaryAsync(x => x.AccountId, x => x.Sum);

        return accounts.Select(a => new AccountWithBalance
        {
            Account = a,
            Balance = a.AccountType switch
            {
                FinanceAccountType.Asset or FinanceAccountType.Expense =>
                    destinationSums.GetValueOrDefault(a.Id) - sourceSums.GetValueOrDefault(a.Id),
                FinanceAccountType.Liability or FinanceAccountType.Equity or FinanceAccountType.Income =>
                    sourceSums.GetValueOrDefault(a.Id) - destinationSums.GetValueOrDefault(a.Id),
                _ => 0
            }
        }).ToList();
    }

    /// <summary>
    /// Computes the total Assets, Liabilities, and Equity values converted to base currency.
    /// </summary>
    public static FinancialSnapshot GetFinancialSnapshot(
        List<AccountWithBalance> accountsWithBalance,
        Dictionary<string, decimal> rates)
    {
        return new FinancialSnapshot
        {
            TotalAssets = accountsWithBalance
                .Where(a => a.Account.AccountType == FinanceAccountType.Asset)
                .Sum(a => a.Balance * rates.GetValueOrDefault(a.Account.Currency, 1)),
            TotalLiabilities = accountsWithBalance
                .Where(a => a.Account.AccountType == FinanceAccountType.Liability)
                .Sum(a => a.Balance * rates.GetValueOrDefault(a.Account.Currency, 1)),
            TotalEquity = accountsWithBalance
                .Where(a => a.Account.AccountType == FinanceAccountType.Equity)
                .Sum(a => a.Balance * rates.GetValueOrDefault(a.Account.Currency, 1)),
        };
    }
}

public class FinancialSnapshot
{
    public decimal TotalAssets { get; init; }
    public decimal TotalLiabilities { get; init; }
    public decimal TotalEquity { get; init; }
}
