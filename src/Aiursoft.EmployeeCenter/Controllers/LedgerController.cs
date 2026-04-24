using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.LedgerViewModels;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.EmployeeCenter.Services.FileStorage;
using Aiursoft.UiStack.Navigation;
using Aiursoft.WebTools.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Microsoft.Extensions.Localization;

namespace Aiursoft.EmployeeCenter.Controllers;

[Authorize(Policy = AppPermissionNames.CanManageLedger)]
[LimitPerMin]
public class LedgerController(
    EmployeeCenterDbContext dbContext,
    LedgerExchangeRateService exchangeRateService,
    LedgerBalanceService balanceService,
    LedgerStatisticsService statisticsService,
    StorageService storageService,
    IStringLocalizer<LedgerController> localizer) : Controller
{
    // ═══════════════════════════════════════════════
    //  Page actions (return HTML views)
    // ═══════════════════════════════════════════════

    [HttpGet]
    [RenderInNavBar(
        NavGroupName = "Administration",
        NavGroupOrder = 3,
        CascadedLinksGroupName = "Finance",
        CascadedLinksIcon = "shopping-cart",
        CascadedLinksOrder = 4,
        LinkText = "Manage Ledger",
        LinkOrder = 1)]
    public async Task<IActionResult> Index()
    {
        var entities = await dbContext.CompanyEntities
            .Where(t => t.CreateLedger)
            .OrderByDescending(t => t.CreationTime)
            .ToListAsync();
        var model = new IndexViewModel
        {
            Entities = entities,
            PageTitle = localizer["Manage Ledger"]
        };
        return this.StackView(model);
    }

    /// <summary>
    /// Renders the Dashboard page skeleton.
    /// All heavy statistics (balances, charts, transactions, health metrics) are loaded
    /// asynchronously via JavaScript calling the DashboardXxxApi endpoints below.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Dashboard(int id, int? accountId, int? year, int? month)
    {
        var entity = await dbContext.CompanyEntities.FindAsync(id);
        if (entity == null || !entity.CreateLedger)
        {
            return NotFound();
        }

        year ??= DateTime.UtcNow.Year;

        FinanceAccount? filteredAccount = null;
        if (accountId.HasValue)
        {
            filteredAccount = await dbContext.FinanceAccounts.FindAsync(accountId.Value);
            if (filteredAccount == null || filteredAccount.CompanyEntityId != id)
            {
                return NotFound();
            }
        }

        var model = new DashboardViewModel
        {
            Entity = entity,
            FilteredAccount = filteredAccount,
            Year = year.Value,
            Month = month,
            PageTitle = $"{entity.CompanyName} - {(filteredAccount != null ? filteredAccount.AccountName : localizer["Ledger Dashboard"])}"
        };

        return this.StackView(model);
    }

    [HttpGet]
    public async Task<IActionResult> Accounts(int id)
    {
        var entity = await dbContext.CompanyEntities.FindAsync(id);
        if (entity == null || !entity.CreateLedger)
        {
            return NotFound();
        }

        var accountsWithBalance = await balanceService.GetAllAccountBalancesAsync(id);

        var model = new AccountsViewModel
        {
            EntityId = id,
            EntityName = entity.CompanyName,
            Accounts = accountsWithBalance,
            PageTitle = $"{entity.CompanyName} - {localizer["Manage Accounts"]}"
        };
        return this.StackView(model);
    }

    // ═══════════════════════════════════════════════
    //  JSON API actions (called by Dashboard JS)
    // ═══════════════════════════════════════════════

    /// <summary>
    /// Returns account balances, financial snapshot, flow totals, and health metrics.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> DashboardSummaryApi(int id, int? accountId, int? year, int? month)
    {
        var entity = await dbContext.CompanyEntities.FindAsync(id);
        if (entity == null || !entity.CreateLedger) return NotFound();

        year ??= DateTime.UtcNow.Year;
        var (startTime, endTime) = GetPeriodRange(year.Value, month);

        var accountsWithBalance = await balanceService.GetAccountBalancesAsync(id, endTime);
        var rates = await exchangeRateService.GetLatestExchangeRatesAsync(id, entity.BaseCurrency);
        var snapshot = LedgerBalanceService.GetFinancialSnapshot(accountsWithBalance, rates);
        var flow = await statisticsService.GetFlowSummaryAsync(id, accountId, startTime, endTime, rates);
        var burnRate = await statisticsService.GetBurnRateAsync(id, rates);

        decimal totalAssetsToRun;
        if (accountId.HasValue)
        {
            var account = accountsWithBalance.FirstOrDefault(a => a.Account.Id == accountId.Value);
            totalAssetsToRun = account != null && account.Account.AccountType == FinanceAccountType.Asset
                ? account.Balance
                : 0;
        }
        else
        {
            totalAssetsToRun = snapshot.TotalAssets;
        }

        var response = new DashboardSummaryResponse
        {
            Accounts = accountsWithBalance
                .Where(a => a.Account.ShowInDashboard)
                .Select(a => new AccountBalanceDto
                {
                    Id = a.Account.Id,
                    AccountName = a.Account.AccountName,
                    AccountType = a.Account.AccountType,
                    Currency = a.Account.Currency,
                    Balance = a.Balance,
                })
                .ToList(),
            FilteredAccountBalance = accountId.HasValue
                ? accountsWithBalance.FirstOrDefault(a => a.Account.Id == accountId)?.Balance ?? 0
                : 0,
            TotalAssets = snapshot.TotalAssets,
            TotalLiabilities = snapshot.TotalLiabilities,
            TotalEquity = snapshot.TotalEquity,
            TotalInflow = flow.TotalInflow,
            TotalOutflow = flow.TotalOutflow,
            RevenueForPeriod = flow.TotalInflow,
            ExpensesForPeriod = flow.TotalOutflow,
            NetIncomeForPeriod = flow.TotalInflow - flow.TotalOutflow,
            MonthlyBurnRate = burnRate,
            RunwayMonths = LedgerStatisticsService.CalculateRunway(totalAssetsToRun, burnRate),
        };

        return Json(response);
    }

    /// <summary>
    /// Returns monthly inflow/outflow chart data for a year.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> DashboardChartApi(int id, int? accountId, int? year)
    {
        var entity = await dbContext.CompanyEntities.FindAsync(id);
        if (entity == null || !entity.CreateLedger) return NotFound();

        year ??= DateTime.UtcNow.Year;
        var rates = await exchangeRateService.GetLatestExchangeRatesAsync(id, entity.BaseCurrency);
        var chart = await statisticsService.GetMonthlyChartDataAsync(id, accountId, year.Value, rates);

        return Json(new DashboardChartResponse
        {
            Labels = chart.Labels,
            InflowData = chart.InflowData,
            OutflowData = chart.OutflowData,
        });
    }

    /// <summary>
    /// Returns recent transactions for the Dashboard table.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> DashboardTransactionsApi(int id, int? accountId, int? year, int? month)
    {
        var entity = await dbContext.CompanyEntities.FindAsync(id);
        if (entity == null || !entity.CreateLedger) return NotFound();

        year ??= DateTime.UtcNow.Year;
        var (startTime, endTime) = GetPeriodRange(year.Value, month);
        var transactions = await statisticsService.GetRecentTransactionsAsync(id, accountId, startTime, endTime);

        var dtos = transactions.Select(t => new TransactionDto
        {
            Id = t.Id,
            TransactionTime = t.TransactionTime.ToString("yyyy-MM-dd HH:mm"),
            Description = t.Description,
            SourceAccountName = t.SourceAccount?.AccountName,
            DestinationAccountName = t.DestinationAccount?.AccountName,
            Amount = t.Amount,
            SourceCurrency = t.SourceAccount?.Currency,
            ExchangeRate = t.ExchangeRate,
            ConvertedAmount = t.Amount * t.ExchangeRate,
            DestinationCurrency = t.DestinationAccount?.Currency,
            InvoiceUrl = string.IsNullOrEmpty(t.InvoicePath) ? null : storageService.RelativePathToInternetUrl(t.InvoicePath, true),
            MT103Url = string.IsNullOrEmpty(t.MT103Path) ? null : storageService.RelativePathToInternetUrl(t.MT103Path, true),
            PaymentVoucherUrl = string.IsNullOrEmpty(t.PaymentVoucherPath) ? null : storageService.RelativePathToInternetUrl(t.PaymentVoucherPath, true),
            EntityId = id,
        }).ToList();

        return Json(dtos);
    }

    /// <summary>
    /// Returns inflow/outflow distribution data for pie charts (only when filtering by a specific account).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> DashboardDistributionApi(int id, int accountId, int? year, int? month)
    {
        var entity = await dbContext.CompanyEntities.FindAsync(id);
        if (entity == null || !entity.CreateLedger) return NotFound();

        year ??= DateTime.UtcNow.Year;
        var (startTime, endTime) = GetPeriodRange(year.Value, month);
        var distribution = await statisticsService.GetFlowDistributionAsync(id, accountId, startTime, endTime);

        return Json(new DashboardDistributionResponse
        {
            InflowDistribution = distribution.InflowDistribution,
            OutflowDistribution = distribution.OutflowDistribution,
        });
    }

    // ═══════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════

    private static (DateTime StartTime, DateTime EndTime) GetPeriodRange(int year, int? month)
    {
        if (month.HasValue)
        {
            var start = new DateTime(year, month.Value, 1, 0, 0, 0, DateTimeKind.Utc);
            return (start, start.AddMonths(1));
        }
        else
        {
            var start = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return (start, start.AddYears(1));
        }
    }

    [HttpGet]
    public IActionResult CreateAccount(int id)
    {
        return this.StackView(new CreateAccountViewModel
        {
            EntityId = id,
            PageTitle = localizer["Create New Account"]
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAccount(CreateAccountViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.PageTitle = localizer["Create New Account"];
            return this.StackView(model);
        }

        var account = new FinanceAccount
        {
            AccountName = model.AccountName,
            AccountType = model.AccountType,
            CompanyEntityId = model.EntityId,
            Currency = model.Currency,
            ShowInDashboard = model.ShowInDashboard
        };

        dbContext.FinanceAccounts.Add(account);
        await dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(Accounts), new { id = model.EntityId });
    }

    [HttpGet]
    public async Task<IActionResult> EditAccount(int id)
    {
        var account = await dbContext.FinanceAccounts.FindAsync(id);
        if (account == null)
        {
            return NotFound();
        }

        var model = new EditAccountViewModel
        {
            Id = account.Id,
            EntityId = account.CompanyEntityId,
            AccountName = account.AccountName,
            AccountType = account.AccountType,
            Currency = account.Currency,
            ShowInDashboard = account.ShowInDashboard,
            IsArchived = account.IsArchived,
            PageTitle = $"{localizer["Edit Account"]} - {account.AccountName}"
        };

        return this.StackView(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditAccount(EditAccountViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.PageTitle = $"{localizer["Edit Account"]} - {model.AccountName}";
            return this.StackView(model);
        }

        var account = await dbContext.FinanceAccounts.FindAsync(model.Id);
        if (account == null)
        {
            return NotFound();
        }

        account.AccountName = model.AccountName;
        account.AccountType = model.AccountType;
        account.Currency = model.Currency;
        account.ShowInDashboard = model.ShowInDashboard;
        account.IsArchived = model.IsArchived;

        await dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(Accounts), new { id = account.CompanyEntityId });
    }

    [HttpGet]
    public async Task<IActionResult> Transactions(int id)
    {
        var entity = await dbContext.CompanyEntities.FindAsync(id);
        if (entity == null || !entity.CreateLedger)
        {
            return NotFound();
        }

        var transactions = await dbContext.Transactions
            .Include(t => t.SourceAccount)
            .Include(t => t.DestinationAccount)
            .Where(t => t.SourceAccount!.CompanyEntityId == id || t.DestinationAccount!.CompanyEntityId == id)
            .OrderByDescending(t => t.TransactionTime)
            .ToListAsync();

        var model = new TransactionsViewModel
        {
            Entity = entity,
            Transactions = transactions,
            PageTitle = $"{entity.CompanyName} - {localizer["Transactions"]}"
        };

        return this.StackView(model);
    }

    [HttpGet]
    public async Task<IActionResult> CreateTransaction(int id)
    {
        var accounts = await dbContext.FinanceAccounts
            .Where(a => a.CompanyEntityId == id && !a.IsArchived)
            .ToListAsync();

        // Also allow selecting accounts from other entities for inter-entity transactions?
        // User said "一人掌控两地公司", so maybe.
        // But for MVP, let's stick to accounts of the current entity + maybe global accounts if any.
        // Actually, "Anduin's Wallet" might be shared or per-entity.
        // Requirement says "Belongs To Entity" for accounts.

        ViewBag.Accounts = accounts;
        return this.StackView(new CreateTransactionViewModel
        {
            EntityId = id,
            TransactionTime = DateTime.UtcNow,
            PageTitle = localizer["New Transaction"]
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTransaction(CreateTransactionViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Accounts = await dbContext.FinanceAccounts
                .Where(a => a.CompanyEntityId == model.EntityId && !a.IsArchived)
                .ToListAsync();
            model.PageTitle = localizer["New Transaction"];
            return this.StackView(model);
        }

        var sourceAccount = await dbContext.FinanceAccounts.FindAsync(model.SourceAccountId);
        var destinationAccount = await dbContext.FinanceAccounts.FindAsync(model.DestinationAccountId);

        if (sourceAccount == null || destinationAccount == null)
        {
            return NotFound();
        }

        if (sourceAccount.CompanyEntityId != model.EntityId && destinationAccount.CompanyEntityId != model.EntityId)
        {
            ModelState.AddModelError(string.Empty, "At least one of the accounts must belong to the current entity.");
            ViewBag.Accounts = await dbContext.FinanceAccounts
                .Where(a => a.CompanyEntityId == model.EntityId && !a.IsArchived)
                .ToListAsync();
            model.PageTitle = localizer["New Transaction"];
            return this.StackView(model);
        }

        var transaction = new Transaction
        {
            Description = model.Description,
            SourceAccountId = model.SourceAccountId,
            DestinationAccountId = model.DestinationAccountId,
            Amount = model.Amount,
            ExchangeRate = model.ExchangeRate,
            InvoicePath = model.InvoicePath,
            MT103Path = model.MT103Path,
            PaymentVoucherPath = model.PaymentVoucherPath,
            TransactionTime = model.TransactionTime
        };

        dbContext.Transactions.Add(transaction);
        await dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(Dashboard), new { id = model.EntityId });
    }

    [HttpGet]
    public async Task<IActionResult> EditTransaction(int id, int entityId)
    {
        var transaction = await dbContext.Transactions.FindAsync(id);
        if (transaction == null)
        {
            return NotFound();
        }

        var accounts = await dbContext.FinanceAccounts
            .Where(a => a.CompanyEntityId == entityId && !a.IsArchived)
            .ToListAsync();

        ViewBag.Accounts = accounts;
        var model = new EditTransactionViewModel
        {
            EntityId = entityId,
            TransactionId = transaction.Id,
            Description = transaction.Description,
            SourceAccountId = transaction.SourceAccountId,
            DestinationAccountId = transaction.DestinationAccountId,
            Amount = transaction.Amount,
            ExchangeRate = transaction.ExchangeRate,
            InvoicePath = transaction.InvoicePath,
            MT103Path = transaction.MT103Path,
            PaymentVoucherPath = transaction.PaymentVoucherPath,
            TransactionTime = transaction.TransactionTime,
            PageTitle = localizer["Edit Transaction"]
        };

        return this.StackView(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditTransaction(EditTransactionViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Accounts = await dbContext.FinanceAccounts
                .Where(a => a.CompanyEntityId == model.EntityId && !a.IsArchived)
                .ToListAsync();
            model.PageTitle = localizer["Edit Transaction"];
            return this.StackView(model);
        }

        var transaction = await dbContext.Transactions.FindAsync(model.TransactionId);
        if (transaction == null)
        {
            return NotFound();
        }

        transaction.Description = model.Description;
        transaction.SourceAccountId = model.SourceAccountId;
        transaction.DestinationAccountId = model.DestinationAccountId;
        transaction.Amount = model.Amount;
        transaction.ExchangeRate = model.ExchangeRate;
        transaction.InvoicePath = model.InvoicePath;
        transaction.MT103Path = model.MT103Path;
        transaction.PaymentVoucherPath = model.PaymentVoucherPath;
        transaction.TransactionTime = model.TransactionTime;

        await dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(Dashboard), new { id = model.EntityId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTransaction(int id, int entityId)
    {
        var transaction = await dbContext.Transactions.FindAsync(id);
        if (transaction == null)
        {
            return NotFound();
        }

        dbContext.Transactions.Remove(transaction);
        await dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(Dashboard), new { id = entityId });
    }

    [HttpGet]
    public async Task<IActionResult> OcrResults(int id)
    {
        var transaction = await dbContext.Transactions
            .Include(t => t.SourceAccount)
            .Include(t => t.DestinationAccount)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (transaction == null)
        {
            return NotFound();
        }

        var results = await dbContext.TransactionOcrResults
            .Where(r => r.TransactionId == id)
            .ToListAsync();

        var model = new OcrResultsViewModel
        {
            Transaction = transaction,
            OcrResults = results,
            PageTitle = $"{localizer["OCR Results"]} - {transaction.Description}"
        };

        return this.StackView(model);
    }
}
