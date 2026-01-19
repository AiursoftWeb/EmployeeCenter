using Aiursoft.EmployeeCenter.Entities;

namespace Aiursoft.EmployeeCenter.Models.UsersViewModels;

public class UserWithRolesViewModel
{
    public required User User { get; set; }
    public required IList<string> Roles { get; set; }
}
