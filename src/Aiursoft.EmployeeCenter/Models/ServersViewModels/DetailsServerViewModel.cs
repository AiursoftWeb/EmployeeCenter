using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.ServersViewModels;

public class DetailsServerViewModel : UiStackLayoutViewModel
{
    public required Server Server { get; init; }
}
