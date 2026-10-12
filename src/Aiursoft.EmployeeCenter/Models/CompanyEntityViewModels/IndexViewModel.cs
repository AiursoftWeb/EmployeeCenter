using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.CompanyEntityViewModels;

public class IndexViewModel : UiStackLayoutViewModel
{
    public IndexViewModel()
    {
        PageTitle = "Company Entities";
    }

    [Display(Name = "Entities")]
    public IEnumerable<CompanyEntity> Entities { get; set; } = new List<CompanyEntity>();
}
