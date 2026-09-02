using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.CompanyEntityViewModels;

public class DetailsViewModel : UiStackLayoutViewModel
{
    public DetailsViewModel()
    {
        PageTitle = "Company Entity Details";
    }

    [Display(Name = "Entity")]
    public required CompanyEntity Entity { get; set; }

    [Display(Name = "Servers")]
    public IEnumerable<Server> Servers { get; set; } = new List<Server>();

    public bool CanViewInfrastructure { get; set; }

    [Display(Name = "Intangible Assets")]
    public IEnumerable<IntangibleAsset> IntangibleAssets { get; set; } = new List<IntangibleAsset>();

    [Display(Name = "Signed Employees")]
    public IEnumerable<User> SignedEmployees { get; set; } = new List<User>();
}
