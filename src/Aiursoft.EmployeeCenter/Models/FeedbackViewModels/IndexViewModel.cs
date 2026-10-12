using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.FeedbackViewModels;

public class IndexViewModel : UiStackLayoutViewModel
{
    public IndexViewModel() => PageTitle = "Employee Signals";

    [Display(Name = "Active Questionnaires")]
    public List<SignalQuestionnaire> ActiveQuestionnaires { get; set; } = new();
}
