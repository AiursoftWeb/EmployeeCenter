using Aiursoft.EmployeeCenter.Entities;
using System.ComponentModel.DataAnnotations;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.ServicesViewModels;

public class ManageDnsProvidersViewModel : UiStackLayoutViewModel
{
    public ManageDnsProvidersViewModel()
    {
        PageTitle = "Manage Dns Providers";
    }

    public List<DnsProvider> DnsProviders { get; set; } = new();

    [Required(ErrorMessage = "The {0} is required.")]
    [MaxLength(100, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "DNS Provider Name")]
    public string? NewName { get; set; }

    [MaxLength(200, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "Description")]
    public string? NewDescription { get; set; }
}
