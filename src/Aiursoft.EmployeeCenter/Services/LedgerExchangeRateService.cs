using Aiursoft.EmployeeCenter.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.EmployeeCenter.Services;

/// <summary>
/// Service for looking up the latest exchange rates between currencies
/// based on historical transaction data.
/// </summary>
public class LedgerExchangeRateService(EmployeeCenterDbContext dbContext)
{
    /// <summary>
    /// Gets the latest exchange rates for all currencies used by active accounts of an entity,
    /// relative to the specified base currency.
    /// Rates are derived from the most recent transaction between each currency pair.
    /// </summary>
    /// <param name="entityId">The company entity ID.</param>
    /// <param name="baseCurrency">The base currency to convert to (e.g. "CNY").</param>
    /// <returns>
    /// A dictionary where key = currency code, value = how many units of base currency per 1 unit of that currency.
    /// The base currency itself always has rate = 1.
    /// </returns>
    public async Task<Dictionary<string, decimal>> GetLatestExchangeRatesAsync(int entityId, string baseCurrency)
    {
        var rates = new Dictionary<string, decimal> { [baseCurrency] = 1 };
        var accounts = await dbContext.FinanceAccounts
            .Where(a => a.CompanyEntityId == entityId && !a.IsArchived)
            .ToListAsync();

        var currencies = accounts.Select(a => a.Currency).Distinct().Where(c => c != baseCurrency).ToList();

        foreach (var currency in currencies)
        {
            var rate = await dbContext.Transactions
                .Where(t => (t.SourceAccount!.Currency == currency && t.DestinationAccount!.Currency == baseCurrency) ||
                            (t.SourceAccount!.Currency == baseCurrency && t.DestinationAccount!.Currency == currency))
                .OrderByDescending(t => t.TransactionTime)
                .Select(t => t.SourceAccount!.Currency == currency ? t.ExchangeRate : 1 / t.ExchangeRate)
                .FirstOrDefaultAsync();
            rates[currency] = rate == 0 ? 1 : rate;
        }

        return rates;
    }
}
