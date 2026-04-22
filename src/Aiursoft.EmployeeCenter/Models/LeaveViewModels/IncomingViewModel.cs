using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;

using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.LeaveViewModels;

public class IncomingViewModel : UiStackLayoutViewModel
{
    public IncomingViewModel()
    {
        PageTitle = "Incoming Leave";
    }


    [Display(Name = "Incoming Leaves")]
    public required List<LeaveApplication> IncomingLeaves { get; set; }

    [Display(Name = "Approval History")]
    public required List<LeaveApplication> ApprovalHistory { get; set; }

    [Display(Name = "Pending Count")]
    public int PendingCount { get; set; }

    [Display(Name = "Approved This Month")]
    public int ApprovedThisMonth { get; set; }

    [Display(Name = "Rejected This Month")]
    public int RejectedThisMonth { get; set; }

    [Display(Name = "Team On Leave Count")]
    public int TeamOnLeaveCount { get; set; }

    [Display(Name = "Can Approve Any Leave")]
    public bool CanApproveAnyLeave { get; set; }

    [Display(Name = "Show All")]
    public bool ShowAll { get; set; }
}
