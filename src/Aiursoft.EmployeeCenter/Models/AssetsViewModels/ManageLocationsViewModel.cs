using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.AssetsViewModels;

public class ManageLocationsViewModel : UiStackLayoutViewModel
{
    public ManageLocationsViewModel()
    {
        PageTitle = "Manage Locations";
    }

    public List<Location> Locations { get; set; } = new();

    [Required(ErrorMessage = "The {0} is required.")]
    [MaxLength(100, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "New Name")]
    public string NewName { get; set; } = string.Empty;
}
