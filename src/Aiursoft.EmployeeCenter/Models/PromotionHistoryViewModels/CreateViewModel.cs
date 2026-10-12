using System.ComponentModel.DataAnnotations;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.PromotionHistoryViewModels;

public class CreateViewModel : UiStackLayoutViewModel
{
    public CreateViewModel()
    {
        PageTitle = "Create Promotion";
    }

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "User")]
    public string? UserId { get; set; }

    [Display(Name = "Old Job Level")]
    [MaxLength(100, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    public string? OldJobLevel { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "New Job Level")]
    [MaxLength(100, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    public string? NewJobLevel { get; set; }

    [Display(Name = "Old Title")]
    [MaxLength(100, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    public string? OldTitle { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "New Title")]
    [MaxLength(100, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    public string? NewTitle { get; set; }

    [Display(Name = "User Display Name")]
    public string? UserDisplayName { get; set; }
}
