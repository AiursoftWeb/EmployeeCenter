using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.ServersViewModels;

public class IndexServerViewModel : UiStackLayoutViewModel
{
    public IndexServerViewModel()
    {
        PageTitle = "Index Server";
    }

    [Display(Name = "Servers")]
    public IEnumerable<Server> Servers { get; set; } = new List<Server>();
}
