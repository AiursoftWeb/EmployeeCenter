using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.WeeklyReportViewModels;

public class EditViewModel : UiStackLayoutViewModel
{
    public EditViewModel()
    {
        PageTitle = "Edit Weekly Report";
    }

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Id")]
    public int Id { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Content")]
    public string Content { get; set; } = string.Empty;

    [Display(Name = "Week Start Date")]
    public DateTime WeekStartDate { get; set; }

    [Display(Name = "User")]
    public User? User { get; set; }

    [Display(Name = "Approved Projects")]
    public List<Requirement> ApprovedProjects { get; set; } = new();

    [Display(Name = "Requirements")]
    public List<WeeklyReportRequirementViewModel> Requirements { get; set; } = new();

    [Display(Name = "Force Project Association")]
    public bool ForceProjectAssociation { get; set; }
}
