using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.ManageFeedbackViewModels;

public class ResponsesViewModel : UiStackLayoutViewModel
{
    public ResponsesViewModel() => PageTitle = "Questionnaire Responses";

    [Display(Name = "Questionnaire")]
    public SignalQuestionnaire Questionnaire { get; set; } = null!;

    [Display(Name = "Responses")]
    public List<SignalResponse> Responses { get; set; } = new();
}
