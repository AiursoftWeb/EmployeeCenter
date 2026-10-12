using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Aiursoft.EmployeeCenter.Models.FeedbackViewModels;

public class FillViewModel : UiStackLayoutViewModel
{
    public FillViewModel() => PageTitle = "Fill Questionnaire";

    [ValidateNever]
    public SignalQuestionnaire? Questionnaire { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Questionnaire Id")]
    public int QuestionnaireId { get; set; }

    [Display(Name = "Answers")]
    public Dictionary<int, string> Answers { get; set; } = new();
}
