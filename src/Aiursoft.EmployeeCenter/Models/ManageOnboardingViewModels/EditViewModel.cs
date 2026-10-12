using System.ComponentModel.DataAnnotations;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.ManageOnboardingViewModels;

public class EditViewModel : UiStackLayoutViewModel
{
    public EditViewModel() => PageTitle = "Edit Onboarding Task";

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Id")]
    public int Id { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Order")]
    public int Order { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Expected Duration (Seconds)")]
    public int ExpectedDurationSeconds { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [MaxLength(100, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "Title")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} is required.")]
    [DataType(DataType.MultilineText)]
    [Display(Name = "Description")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} is required.")]
    [MaxLength(500, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Url(ErrorMessage = "The {0} is not a valid URL.")]
    [Display(Name = "Start Link")]
    public string StartLink { get; set; } = string.Empty;
}
