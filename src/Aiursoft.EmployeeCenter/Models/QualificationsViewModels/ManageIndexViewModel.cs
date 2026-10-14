using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.QualificationsViewModels;

public class ManageIndexViewModel : UiStackLayoutViewModel
{
    public ManageIndexViewModel() => PageTitle = "Manage Business Qualifications";
    public List<BusinessQualification> Qualifications { get; set; } = [];
}
