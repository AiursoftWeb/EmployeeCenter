using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.AssetsViewModels;

public class IndexViewModel : UiStackLayoutViewModel
{
    public IndexViewModel()
    {
        PageTitle = "Index";
    }

    [Display(Name = "Assets")]
    public List<Asset> Assets { get; set; } = new();

    public int? SelectedCompanyEntityId { get; set; }

    public List<CompanyEntity> AllCompanyEntities { get; set; } = new();
}
