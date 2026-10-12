using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.LedgerViewModels;

public class AccountWithBalance
{
    [Display(Name = "Account")]
    public required FinanceAccount Account { get; set; }

    [Display(Name = "Balance")]
    public decimal Balance { get; set; }
}

/// <summary>
/// Lightweight view model for the Dashboard page skeleton.
/// All heavy statistics are loaded asynchronously via JavaScript calling separate API endpoints.
/// </summary>
public class DashboardViewModel : UiStackLayoutViewModel
{
    public DashboardViewModel()
    {
        PageTitle = "Dashboard";
    }

    [Display(Name = "Entity")]
    public required CompanyEntity Entity { get; set; }

    [Display(Name = "Filtered Account")]
    public FinanceAccount? FilteredAccount { get; set; }

    [Display(Name = "Year")]
    public int Year { get; set; }

    [Display(Name = "Month")]
    public int? Month { get; set; }
}
