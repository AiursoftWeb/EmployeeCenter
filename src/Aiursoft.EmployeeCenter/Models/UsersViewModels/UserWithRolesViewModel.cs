using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;

namespace Aiursoft.EmployeeCenter.Models.UsersViewModels;

public class UserWithRolesViewModel
{

    [Display(Name = "User")]
    public required User User { get; set; }

    [Display(Name = "Roles")]
    public required IList<string> Roles { get; set; }
}
