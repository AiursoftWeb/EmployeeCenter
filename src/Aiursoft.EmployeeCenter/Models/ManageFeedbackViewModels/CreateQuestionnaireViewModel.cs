using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Aiursoft.EmployeeCenter.Models.ManageFeedbackViewModels;

public class CreateQuestionnaireViewModel : UiStackLayoutViewModel
{
    public CreateQuestionnaireViewModel() => PageTitle = "Create Questionnaire";

    [Required(ErrorMessage = "The {0} is required.")]
    [MaxLength(200, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "Title")]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [Display(Name = "Is Active")]
    public bool IsActive { get; set; }

    [Display(Name = "Selected Question Ids")]
    public List<int> SelectedQuestionIds { get; set; } = new();

    [ValidateNever]
    public List<SignalQuestion> AllAvailableQuestions { get; set; } = new();
}
