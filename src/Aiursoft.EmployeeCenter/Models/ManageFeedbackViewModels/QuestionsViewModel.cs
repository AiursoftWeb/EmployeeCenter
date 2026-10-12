using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.ManageFeedbackViewModels;

public class QuestionsViewModel : UiStackLayoutViewModel
{
    public QuestionsViewModel() => PageTitle = "Manage Questions";

    [Display(Name = "Questions")]
    public List<SignalQuestion> Questions { get; set; } = new();
}
