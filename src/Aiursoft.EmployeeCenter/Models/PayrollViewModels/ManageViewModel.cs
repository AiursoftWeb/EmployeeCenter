using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.PayrollViewModels;

public class ManageViewModel : UiStackLayoutViewModel
{
    public ManageViewModel()
    {
        PageTitle = "Manage Payrolls";
    }

    [Display(Name = "Payrolls")]
    public List<Payroll> Payrolls { get; set; } = [];

    [Display(Name = "All Users")]
    public List<User> AllUsers { get; set; } = [];
}
