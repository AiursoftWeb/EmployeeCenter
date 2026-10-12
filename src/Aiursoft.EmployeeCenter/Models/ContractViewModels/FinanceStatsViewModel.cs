using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.ContractViewModels;

public class FinanceStatsViewModel : UiStackLayoutViewModel
{
    public FinanceStatsViewModel()
    {
        PageTitle = "Contract Finance Statistics";
    }

    public List<ContractFinanceSummary> Summaries { get; set; } = [];
}

public class ContractFinanceSummary
{
    public required Contract Contract { get; set; }
    public long TotalExpectedIncome { get; set; }
    public long TotalActualIncome { get; set; }
    public long TotalPendingIncome => TotalExpectedIncome - TotalActualIncome;
    public long TotalExpectedExpense { get; set; }
    public long TotalActualExpense { get; set; }
    public long TotalPendingExpense => TotalExpectedExpense - TotalActualExpense;
    public string Currency { get; set; } = "CNY";
}
