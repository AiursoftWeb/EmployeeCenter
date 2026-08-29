namespace Aiursoft.EmployeeCenter.Authorization;

/// <summary>
/// Defines all permission keys as constants. This is the single source of truth.
/// </summary>
public static class AppPermissionNames
{
    // User Management
    public const string CanReadUsers = nameof(CanReadUsers);
    public const string CanDeleteUsers = nameof(CanDeleteUsers);
    public const string CanAddUsers = nameof(CanAddUsers);
    public const string CanEditAndViewDetailsOfUsers = nameof(CanEditAndViewDetailsOfUsers);
    public const string CanViewUserOperationLog = nameof(CanViewUserOperationLog);
    public const string CanEditUsers = nameof(CanEditUsers);
    public const string CanAssignRoleToUser = nameof(CanAssignRoleToUser);

    // Role Management
    public const string CanReadRoles = nameof(CanReadRoles);
    public const string CanDeleteRoles = nameof(CanDeleteRoles);
    public const string CanAddRoles = nameof(CanAddRoles);
    public const string CanEditRoles = nameof(CanEditRoles);

    // System Management

    // Payroll Management
    public const string CanManagePayroll = nameof(CanManagePayroll);

    // Password Management
    public const string CanAddGlobalPassword = nameof(CanAddGlobalPassword);
    public const string CanManageAnyPassword = nameof(CanManageAnyPassword);

    // SSH Key Management
    public const string CanManageSshKeys = nameof(CanManageSshKeys);

    // Report Line Management
    public const string CanViewReportLine = nameof(CanViewReportLine);

    // Leave Management
    public const string CanApproveAnyLeave = nameof(CanApproveAnyLeave);

    // Onboarding Task Management
    public const string CanManageOnboarding = nameof(CanManageOnboarding);

    // Certificate Printing
    public const string CanPrintCertificates = nameof(CanPrintCertificates);
    public const string CanPrintSelfCertificates = nameof(CanPrintSelfCertificates);

    // Company Entity Management
    public const string CanManageCompanyEntities = nameof(CanManageCompanyEntities);
    public const string CanManageCustomerRelationships = nameof(CanManageCustomerRelationships);
    public const string CanManageMarketChannels = nameof(CanManageMarketChannels);

    // Finance Management
    public const string CanManageLedger = nameof(CanManageLedger);

    // Asset Management
    public const string CanManageAssets = nameof(CanManageAssets);

    // Weekly Report Management
    public const string CanCreateWeeklyReport = nameof(CanCreateWeeklyReport);
    public const string CanManageAnyoneWeeklyReport = nameof(CanManageAnyoneWeeklyReport);

    // Contract Management
    public const string CanViewContractHistory = nameof(CanViewContractHistory);
    public const string CanCreateContract = nameof(CanCreateContract);

    // Collection Channel Management
    public const string CanViewCollectionChannels = nameof(CanViewCollectionChannels);
    public const string CanManageCollectionChannels = nameof(CanManageCollectionChannels);

    // Feedback Management (Employee Signals)
    public const string CanSubmitFeedback = nameof(CanSubmitFeedback);
    public const string CanManageFeedback = nameof(CanManageFeedback);

    // Service Management
    public const string CanManageServices = nameof(CanManageServices);
    public const string CanAuditDns = nameof(CanAuditDns);

    // Permission Management
    public const string CanReadPermissions = nameof(CanReadPermissions);

    // System Management
    public const string CanViewSystemContext = nameof(CanViewSystemContext);
    public const string CanRebootThisApp = nameof(CanRebootThisApp);
    public const string CanViewBackgroundJobs = nameof(CanViewBackgroundJobs);
    public const string CanManageGlobalSettings = nameof(CanManageGlobalSettings);
    public const string CanManageBlueprints = nameof(CanManageBlueprints);
    public const string CanApproveProjectRequirements = nameof(CanApproveProjectRequirements);

    // Reimbursement Management
    public const string CanSubmitReimbursement = nameof(CanSubmitReimbursement);
    public const string CanApproveReimbursement = nameof(CanApproveReimbursement);

    // AI Management
    public const string CanChatWithAi = nameof(CanChatWithAi);

    // Audio Management
    public const string CanManageAudio = nameof(CanManageAudio);
}
