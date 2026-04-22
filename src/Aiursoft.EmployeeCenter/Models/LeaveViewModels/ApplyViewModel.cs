using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.LeaveViewModels;

public class ApplyViewModel : UiStackLayoutViewModel
{
    public ApplyViewModel()
    {
        PageTitle = "Apply Leave";
    }


    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Leave Type")]
    public LeaveType LeaveType { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [DataType(DataType.Date)]
    [Display(Name = "Start Date")]
    public DateTime StartDate { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [DataType(DataType.Date)]
    [Display(Name = "End Date")]
    public DateTime EndDate { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [MaxLength(500, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "Reason")]
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Calculated total working days (set by controller after validation)
    /// </summary>
    [Display(Name = "Total Days")]
    public decimal TotalDays { get; set; }
}
