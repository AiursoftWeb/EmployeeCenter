using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.QualificationsViewModels;

public class PreviewViewModel : UiStackLayoutViewModel
{
    public PreviewViewModel() => PageTitle = "Watermarked Qualification Download";
    public required QualificationDownload Record { get; set; }
}
