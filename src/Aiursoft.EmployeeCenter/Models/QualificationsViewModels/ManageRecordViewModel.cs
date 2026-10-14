using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.QualificationsViewModels;

public class ManageRecordViewModel : UiStackLayoutViewModel
{
    public ManageRecordViewModel() => PageTitle = "Qualification Issuance Record";
    public required QualificationDownload Record { get; set; }
}
