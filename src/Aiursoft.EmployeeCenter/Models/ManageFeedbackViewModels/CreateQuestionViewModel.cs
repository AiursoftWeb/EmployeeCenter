using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.ManageFeedbackViewModels;

public class CreateQuestionViewModel : UiStackLayoutViewModel
{
    public CreateQuestionViewModel() => PageTitle = "Create Question";

    [Required(ErrorMessage = "The {0} is required.")]
    [MaxLength(500, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "Title")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Type")]
    public SignalQuestionType Type { get; set; }

    [MaxLength(200, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "Tags")]
    public string? Tags { get; set; }

    [Display(Name = "Meta")]
    public string? Meta { get; set; }
}
