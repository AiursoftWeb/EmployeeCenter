using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.PayrollViewModels;

public class IndexViewModel : UiStackLayoutViewModel
{
    public IndexViewModel()
    {
        PageTitle = "My Payrolls";
    }

    [Display(Name = "Payrolls")]
    public List<Payroll> Payrolls { get; set; } = [];

    // Chart data for salary trend visualization
    [Display(Name = "Chart Labels")]
    public List<string> ChartLabels { get; set; } = [];

    [Display(Name = "Chart Total Amounts")]
    public List<decimal> ChartTotalAmounts { get; set; } = [];

    [Display(Name = "Chart Base Salaries")]
    public List<decimal> ChartBaseSalaries { get; set; } = [];

    [Display(Name = "Chart Currencies")]
    public List<string> ChartCurrencies { get; set; } = [];
}
