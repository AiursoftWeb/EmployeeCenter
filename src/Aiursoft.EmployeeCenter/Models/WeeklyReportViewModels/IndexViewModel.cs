using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.WeeklyReportViewModels;

public class IndexViewModel : UiStackLayoutViewModel
{
    public IndexViewModel()
    {
        PageTitle = "Weekly Report";
    }

    [Display(Name = "Reports")]
    public List<WeeklyReport> Reports { get; set; } = new();

    [Display(Name = "All Users")]
    public List<User> AllUsers { get; set; } = new();

    [Display(Name = "Can Manage Anyone Weekly Report")]
    public bool CanManageAnyoneWeeklyReport { get; set; }

    [Display(Name = "Can Create")]
    public bool CanCreate { get; set; }

    [Display(Name = "Current Week Submitted")]
    public bool CurrentWeekSubmitted { get; set; }

    [Display(Name = "Notepad Content")]
    public string? NotepadContent { get; set; }

    [Display(Name = "Available Weeks")]
    public Dictionary<DateTime, string> AvailableWeeks { get; set; } = new();

    [Display(Name = "Has Recent Missing Reports")]
    public bool HasRecentMissingReports { get; set; }

    [Display(Name = "Critical Missing Reports")]
    public bool CriticalMissingReports { get; set; }

    [Display(Name = "Missing Weeks Count")]
    public int MissingWeeksCount { get; set; }

    [Display(Name = "Filter User Id")]
    public string? FilterUserId { get; set; }

    [Display(Name = "Filter User")]
    public User? FilterUser { get; set; }

    [Display(Name = "Approved Projects")]
    public List<Requirement> ApprovedProjects { get; set; } = new();

    [Display(Name = "Force Project Association")]
    public bool ForceProjectAssociation { get; set; }
}
