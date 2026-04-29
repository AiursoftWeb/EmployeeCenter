using System.ComponentModel.DataAnnotations;

namespace Aiursoft.EmployeeCenter.Models.RolesViewModels;

public class RoleClaimViewModel
{

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Key")]
    public required string Key { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Name")]
    public required string Name { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Description")]
    public required string Description { get; set; }

    [Display(Name = "Is Selected")]
    public bool IsSelected { get; set; }
}
