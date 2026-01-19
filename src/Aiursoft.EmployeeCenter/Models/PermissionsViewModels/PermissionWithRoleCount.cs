using Aiursoft.EmployeeCenter.Authorization;

namespace Aiursoft.EmployeeCenter.Models.PermissionsViewModels;

public class PermissionWithRoleCount
{
    public required PermissionDescriptor Permission { get; init; }
    public required int RoleCount { get; init; }
    public required int UserCount { get; init; }
}
