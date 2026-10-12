using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Aiursoft.EmployeeCenter.Models.RolesViewModels;

public class IdentityRoleWithCount
{
    [Display(Name = "Role")]
    public required IdentityRole Role { get; init; }

    [Display(Name = "User Count")]
    public required int UserCount { get; init; }

    [Display(Name = "Permission Count")]
    public required int PermissionCount { get; init; }

    [Display(Name = "Permission Names")]
    public required List<string> PermissionNames { get; init; }
}
