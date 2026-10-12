using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.ContractViewModels;

public class FinanceDetailsViewModel : UiStackLayoutViewModel
{
    public FinanceDetailsViewModel()
    {
        PageTitle = "Contract Finance Details";
    }

    public required Contract Contract { get; set; }

    public long TotalExpectedIncome { get; set; }
    public long TotalActualIncome { get; set; }
    public long TotalPendingIncome => TotalExpectedIncome - TotalActualIncome;

    public long TotalExpectedExpense { get; set; }
    public long TotalActualExpense { get; set; }
    public long TotalPendingExpense => TotalExpectedExpense - TotalActualExpense;
    
    public string Currency { get; set; } = "CNY";
}
