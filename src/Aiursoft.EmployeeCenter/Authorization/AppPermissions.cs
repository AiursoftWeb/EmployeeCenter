namespace Aiursoft.EmployeeCenter.Authorization;

/// <summary>
/// A static class that provides all application permissions.
/// It uses a fake localizer to ensure permission names and descriptions are picked up by localization tools.
/// This class serves as the single source of truth for all permissions in the application.
/// </summary>
public class AppPermissions
{
    public const string Type = "Permission";

    /// <summary>
    /// A fake localizer that returns the input string as is.
    /// This is used to trick auto scanning tools to detect these strings for localization.
    /// </summary>
    public class FakeLocalizer
    {
        public string this[string name] => name;
    }

    public static List<PermissionDescriptor> GetAllPermissions()
    {
        // Make a fake localizer. This returns as is.
        // This trick is to make auto scanning tools to detect these strings for localization.
        var localizer = new FakeLocalizer();
        List<PermissionDescriptor> allPermission =
        [
            new(AppPermissionNames.CanReadUsers,
                localizer["Read Users"],
                localizer["Allows viewing the list of all users."]),
            new(AppPermissionNames.CanDeleteUsers,
                localizer["Delete Users"],
                localizer["Allows the permanent deletion of user accounts."]),
            new(AppPermissionNames.CanAddUsers,
                localizer["Add New Users"],
                    localizer["Grants permission to create new user accounts."]),
            new(AppPermissionNames.CanEditAndViewDetailsOfUsers,
                localizer["Edit and View Details of Users"],
                    localizer["Allows modification of user details like email and roles, resetting user passwords, and viewing sensitive user details like payroll."]),
            new(AppPermissionNames.CanEditUsers,
                localizer["Edit User Information"],
                    localizer["Allows modification of user details like email and roles, and can also reset user passwords."]),
            new(AppPermissionNames.CanAssignRoleToUser,
                localizer["Assign Roles to Users"],
                localizer["Allows assigning or removing roles for any user."]),
            new(AppPermissionNames.CanViewUserOperationLog,
                localizer["View User Operation Log"],
                localizer["Allows viewing the operation logs of any user, including their bank card change history."]),
            new(AppPermissionNames.CanReadRoles,
                localizer["Read Roles"],
                    localizer["Allows viewing the list of roles and their assigned permissions."]),
            new(AppPermissionNames.CanDeleteRoles,
                localizer["Delete Roles"],
                localizer["Allows the permanent deletion of roles."]),
            new(AppPermissionNames.CanAddRoles,
                localizer["Add New Roles"],
                localizer["Grants permission to create new roles."]),
            new(AppPermissionNames.CanEditRoles,
                localizer["Edit Role Information"],
                localizer["Allows modification of role names and their assigned permissions."]),
            new(AppPermissionNames.CanReadPermissions,
                localizer["Read Permissions"],
                localizer["Allows viewing the list of all permissions and their assignments to roles and users."]),
            new(AppPermissionNames.CanViewSystemContext,
                localizer["View System Context"],
                localizer["Allows viewing system-level information and settings."]),
            new(AppPermissionNames.CanRebootThisApp,
                localizer["Reboot This App"],
                localizer["Grants permission to restart the application instance. May cause availability interruptions but all settings and cache will be reloaded."]),
            new(AppPermissionNames.CanViewBackgroundJobs,
                localizer["View Background Jobs"],
                localizer["Allows viewing the background job dashboard and managing jobs."]),
            new(AppPermissionNames.CanManageGlobalSettings,
                localizer["Manage Global Settings"],
                localizer["Allows viewing and modifying global application settings."]),
            new(AppPermissionNames.CanManagePayroll,
                localizer["Manage Payroll"],
                localizer["Allows issuing and managing payroll slips for all users."]),
            new(AppPermissionNames.CanAddGlobalPassword,
                localizer["Add Global Password"],
                localizer["Allows creating new global passwords that can be shared with others."]),
            new(AppPermissionNames.CanManageAnyPassword,
                localizer["Manage Any Password"],
                localizer["Allows managing and deleting any global password created by any user."]),
            new(AppPermissionNames.CanManageSshKeys,
                localizer["Manage SSH Keys"],
                localizer["Allows viewing, editing, and deleting any user's SSH keys."]),
            new(AppPermissionNames.CanViewReportLine,
                localizer["View Report Line"],
                localizer["Allows viewing the report line of any user, including their manager, subordinates, and peers."]),
            new(AppPermissionNames.CanApproveAnyLeave,
                localizer["Approve Any Leave"],
                localizer["Allows approving or rejecting any leave application, regardless of the reporting line."]),
            new(AppPermissionNames.CanManageOnboarding,
                localizer["Manage Onboarding Tasks"],
                localizer["Allows creating and managing onboarding task templates for new employees."]),
            new(AppPermissionNames.CanPrintCertificates,
                localizer["Print Certificates"],
                localizer["Allows printing employment and income certificates for any employee."]),
            new(AppPermissionNames.CanManageCompanyEntities,
                localizer["Manage Company Entity Information"],
                localizer["Allows creating, updating, and deleting company entity information. Every user can view company entity information by default."]),
            new(AppPermissionNames.CanManageAssets,
                localizer["Manage Assets"],
                localizer["Allows managing the company's IT assets, including categories, models, and individual items. Also allows assigning and recovering assets to/from employees."]),
            new(AppPermissionNames.CanCreateWeeklyReport,
                localizer["Create Weekly Report"],
                localizer["Allows the user to create their own weekly reports."]),
            new(AppPermissionNames.CanManageAnyoneWeeklyReport,
                localizer["Manage anyone's weekly report"],
                localizer["Allows creating, editing, and deleting weekly reports on behalf of any other user."]),
            new(AppPermissionNames.CanViewContractHistory,
                localizer["View any company contract, including private ones"],
                localizer["Allows viewing all company contracts, including those marked as private. Everyone can view public contracts by default"]),
            new(AppPermissionNames.CanCreateContract,
                localizer["Create company contracts"],
                localizer["Allows creating contracts for the company. Every user can view public contracts by default"]),
            new(AppPermissionNames.CanSubmitFeedback,
                localizer["Submit Feedback"],
                localizer["Allows the user to fill out and submit feedback questionnaires."]),
            new(AppPermissionNames.CanManageFeedback,
                localizer["Manage Feedback"],
                localizer["Allows creating questions, questionnaires, and viewing feedback responses."])
        ];
        return allPermission;
    }
}

