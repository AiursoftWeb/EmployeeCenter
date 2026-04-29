using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.ServersViewModels;

public class CreateServerViewModel : UiStackLayoutViewModel
{
    public CreateServerViewModel()
    {
        PageTitle = "Create Server";
    }

    [MaxLength(100, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "IP Address")]
    public string? ServerIp { get; set; }

    [MaxLength(500, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "Detail Link")]
    public string? DetailLink { get; set; }

    [Display(Name = "Location")]
    public int? LocationId { get; set; }

    [MaxLength(100, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "Hostname")]
    public string? Hostname { get; set; }

    [Display(Name = "Owner")]
    public string? OwnerId { get; set; }

    [Display(Name = "Provider")]
    public int? ProviderId { get; set; }

    [Display(Name = "Company Entity")]
    public int? CompanyEntityId { get; set; }

    public IEnumerable<Location> AllLocations { get; set; } = new List<Location>();
    public IEnumerable<User> AllOwners { get; set; } = new List<User>();
    public IEnumerable<Provider> AllProviders { get; set; } = new List<Provider>();
    public IEnumerable<CompanyEntity> AllCompanyEntities { get; set; } = new List<CompanyEntity>();
}
