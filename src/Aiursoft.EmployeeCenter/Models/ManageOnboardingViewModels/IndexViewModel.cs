using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.ManageOnboardingViewModels;

public class IndexViewModel : UiStackLayoutViewModel
{
    public IndexViewModel() => PageTitle = "Manage Onboarding Tasks";

    [Display(Name = "Tasks")]
    public required List<OnboardingTask> Tasks { get; set; }

    [Display(Name = "Employee Progresses")]
    public List<EmployeeProgress>? EmployeeProgresses { get; set; }
}
