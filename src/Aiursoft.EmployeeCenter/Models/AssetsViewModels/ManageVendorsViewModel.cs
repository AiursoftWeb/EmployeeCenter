using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.AssetsViewModels;

public class ManageVendorsViewModel : UiStackLayoutViewModel
{
    public ManageVendorsViewModel()
    {
        PageTitle = "Manage Vendors";
    }

    public List<Vendor> Vendors { get; set; } = new();

    [Required(ErrorMessage = "The {0} is required.")]
    [MaxLength(100, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "New Name")]
    public string NewName { get; set; } = string.Empty;

    [MaxLength(200, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "New Contact Info")]
    public string? NewContactInfo { get; set; }
}
