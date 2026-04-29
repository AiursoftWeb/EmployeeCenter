using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.ServicesViewModels;

public class ManageProvidersViewModel : UiStackLayoutViewModel
{
    public ManageProvidersViewModel()
    {
        PageTitle = "Manage Providers";
    }

    public IEnumerable<Provider> Providers { get; set; } = new List<Provider>();

    [Required(ErrorMessage = "The {0} is required.")]
    [MaxLength(100, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "New Provider Name")]
    public string NewName { get; set; } = string.Empty;
}
