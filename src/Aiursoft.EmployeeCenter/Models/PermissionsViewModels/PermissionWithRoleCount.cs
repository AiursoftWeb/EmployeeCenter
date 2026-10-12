using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Authorization;

namespace Aiursoft.EmployeeCenter.Models.PermissionsViewModels;

public class PermissionWithRoleCount
{
    [Display(Name = "Permission")]
    public required PermissionDescriptor Permission { get; init; }

    [Display(Name = "Role Count")]
    public required int RoleCount { get; init; }

    [Display(Name = "User Count")]
    public required int UserCount { get; init; }
}
