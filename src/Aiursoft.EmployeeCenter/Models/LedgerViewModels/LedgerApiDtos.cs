using Aiursoft.EmployeeCenter.Entities;

namespace Aiursoft.EmployeeCenter.Models.LedgerViewModels;

// ──────────────────────────────────────────────
//  JSON DTOs returned by LedgerController API endpoints
// ──────────────────────────────────────────────

/// <summary>
/// Response for /Ledger/DashboardSummaryApi
/// Contains account balances, financial snapshot, flow totals, and health metrics.
/// </summary>
public class DashboardSummaryResponse
{
    // Account balance cards
    public List<AccountBalanceDto> Accounts { get; set; } = [];
    public decimal FilteredAccountBalance { get; set; }

    // Financial summary (company-wide, base currency)
    public decimal TotalAssets { get; set; }
    public decimal TotalLiabilities { get; set; }
    public decimal TotalEquity { get; set; }

    // Period flow totals
    public decimal TotalInflow { get; set; }
    public decimal TotalOutflow { get; set; }
    public decimal RevenueForPeriod { get; set; }
    public decimal ExpensesForPeriod { get; set; }
    public decimal NetIncomeForPeriod { get; set; }

    // Health metrics
    public decimal MonthlyBurnRate { get; set; }
    public decimal? RunwayMonths { get; set; }
}

public class AccountBalanceDto
{
    public int Id { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public FinanceAccountType AccountType { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal Balance { get; set; }
}

/// <summary>
/// Response for /Ledger/DashboardChartApi
/// </summary>
public class DashboardChartResponse
{
    public string[] Labels { get; set; } = [];
    public decimal[] InflowData { get; set; } = [];
    public decimal[] OutflowData { get; set; } = [];
}

/// <summary>
/// Response for /Ledger/DashboardTransactionsApi
/// </summary>
public class TransactionDto
{
    public int Id { get; set; }
    public string TransactionTime { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? SourceAccountName { get; set; }
    public string? DestinationAccountName { get; set; }
    public decimal Amount { get; set; }
    public string? SourceCurrency { get; set; }
    public decimal ExchangeRate { get; set; }
    public decimal ConvertedAmount { get; set; }
    public string? DestinationCurrency { get; set; }
    public string? InvoiceUrl { get; set; }
    public string? MT103Url { get; set; }
    public string? PaymentVoucherUrl { get; set; }
    public int EntityId { get; set; }
}

/// <summary>
/// Response for /Ledger/DashboardAssetTrendApi
/// </summary>
public class AssetTrendResponse
{
    public string[] Labels { get; set; } = [];
    public decimal?[] AssetData { get; set; } = [];
    public decimal?[] LiabilityData { get; set; } = [];
}

/// <summary>
/// Response for /Ledger/DashboardDistributionApi
/// </summary>
public class DashboardDistributionResponse
{
    public Dictionary<string, decimal> InflowDistribution { get; set; } = new();
    public Dictionary<string, decimal> OutflowDistribution { get; set; } = new();
}
