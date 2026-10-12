using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.ManageFeedbackViewModels;

public class ResponseDetailViewModel : UiStackLayoutViewModel
{
    public ResponseDetailViewModel() => PageTitle = "Response Detail";

    [Display(Name = "Response")]
    public SignalResponse Response { get; set; } = null!;
}
