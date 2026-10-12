using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.LeaveViewModels;

public class IndexViewModel : UiStackLayoutViewModel
{
    public IndexViewModel()
    {
        PageTitle = "Leave";
    }

    /// <summary>
    /// Total annual leave allocated for current year
    /// </summary>
    [Display(Name = "Annual Leave Allocation")]
    public decimal AnnualLeaveAllocation { get; set; }

    /// <summary>
    /// Annual leave carried over from previous year
    /// </summary>
    [Display(Name = "Carried Over Annual Leave")]
    public decimal CarriedOverAnnualLeave { get; set; }

    /// <summary>
    /// Remaining annual leave (including carried over)
    /// </summary>
    [Display(Name = "Remaining Annual Leave")]
    public decimal RemainingAnnualLeave { get; set; }

    /// <summary>
    /// Total sick leave allocated for current year
    /// </summary>
    [Display(Name = "Sick Leave Allocation")]
    public decimal SickLeaveAllocation { get; set; }

    /// <summary>
    /// Remaining sick leave for current year
    /// </summary>
    [Display(Name = "Remaining Sick Leave")]
    public decimal RemainingSickLeave { get; set; }

    /// <summary>
    /// All leave applications (pending and historical)
    /// </summary>
    [Display(Name = "Leave History")]
    public List<LeaveApplication> LeaveHistory { get; set; } = new();

    /// <summary>
    /// Whether user has remaining leave days to apply
    /// </summary>
    [Display(Name = "Can Apply Leave")]
    public bool CanApplyLeave { get; set; }

    /// <summary>
    /// Current year
    /// </summary>
    [Display(Name = "Current Year")]
    public int CurrentYear { get; set; }

    /// <summary>
    /// Next upcoming approved leave starting within 14 days (if any)
    /// </summary>
    [Display(Name = "Next Upcoming Leave")]
    public LeaveApplication? NextUpcomingLeave { get; set; }

    /// <summary>
    /// Whether there is an approved leave starting within 14 days
    /// </summary>
    [Display(Name = "Has Upcoming Leave Within 14 Days")]
    public bool HasUpcomingLeaveWithin14Days { get; set; }

    /// <summary>
    /// Number of pending leave applications
    /// </summary>
    [Display(Name = "Pending Count")]
    public int PendingCount { get; set; }

    /// <summary>
    /// Annual leave days allocated per year as per company policy
    /// </summary>
    [Display(Name = "Annual Leave Per Year Policy")]
    public decimal AnnualLeavePerYearPolicy { get; set; }

    /// <summary>
    /// Days until next approved leave starts (null if no upcoming leave)
    /// </summary>
    [Display(Name = "Days Until Next Leave")]
    public int? DaysUntilNextLeave { get; set; }
}
