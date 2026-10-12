using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.DashboardViewModels;

public class IndexViewModel : UiStackLayoutViewModel
{
    public IndexViewModel()
    {
        PageTitle = "Dashboard";
    }

    [Display(Name = "Tasks")]
    public required List<OnboardingTask> Tasks { get; set; }

    [Display(Name = "Logs")]
    public required List<OnboardingTaskLog> Logs { get; set; }

    [Display(Name = "User")]
    public required User User { get; set; }
}
