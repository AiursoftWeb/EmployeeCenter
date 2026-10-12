using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.LeaveViewModels;

public class TeamCalendarViewModel : UiStackLayoutViewModel
{
    public TeamCalendarViewModel()
    {
        PageTitle = "Team Calendar";
    }

    [Display(Name = "Team Leaves")]
    public List<TeamMemberLeave> TeamLeaves { get; set; } = new();

    [Display(Name = "Can Approve Any Leave")]
    public bool CanApproveAnyLeave { get; set; }

    [Display(Name = "Searched User")]
    public User? SearchedUser { get; set; }

    [Display(Name = "Searched User Leaves")]
    public List<LeaveApplication> SearchedUserLeaves { get; set; } = new();
}
