using System.ComponentModel.DataAnnotations;

namespace Aiursoft.EmployeeCenter.Models.UsersViewModels;

public class UserRoleViewModel
{

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Role Name")]
    public required string RoleName { get; set; }

    [Display(Name = "Is Selected")]
    public bool IsSelected { get; set; }
}
