using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;

namespace Aiursoft.EmployeeCenter.Models.ManageOnboardingViewModels;

public class EmployeeProgress
{
    [Display(Name = "User")]
    public required User User { get; set; }

    [Display(Name = "Completed Tasks Count")]
    public int CompletedTasksCount { get; set; }

    [Display(Name = "Total Tasks Count")]
    public int TotalTasksCount { get; set; }

    [Display(Name = "Progress Percentage")]
    public double ProgressPercentage => TotalTasksCount == 0 ? 0 : (double)CompletedTasksCount / TotalTasksCount * 100;
}
