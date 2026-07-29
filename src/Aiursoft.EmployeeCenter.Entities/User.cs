using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace Aiursoft.EmployeeCenter.Entities;

public class User : IdentityUser
{
    public const string DefaultAvatarPath = "avatar/default-avatar.jpg";

    [MaxLength(30)]
    [MinLength(2)]
    public required string DisplayName { get; set; }

    [MaxLength(150)]
    [MinLength(2)]
    public required string AvatarRelativePath { get; set; } = DefaultAvatarPath;

    public DateTime CreationTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The job level of this user. (职级)
    /// If null, it means the user has no job level assigned yet.
    /// </summary>
    [MaxLength(100)]
    public string? JobLevel { get; set; }

    /// <summary>
    /// The title of this user. (职务)
    /// If null, it means the user has no title assigned yet.
    /// </summary>
    [MaxLength(100)]
    public string? Title { get; set; }

    [MaxLength(100)]
    public string? LegalName { get; set; }

    public decimal BaseSalary { get; set; }

    /// <summary>
    /// The bank account of this user. (银行卡号)
    /// If null, it means the user has not provided a bank account yet.
    /// </summary>
    [MaxLength(30)]
    public string? BankAccount { get; set; }

    /// <summary>
    /// The bank name of this user. (开户行)
    /// If null, it means the user has not provided a bank name yet.
    /// </summary>
    [MaxLength(30)]
    public string? BankName { get; set; }

    /// <summary>
    /// The bank account name of this user. (户名)
    /// If null, it means the user has not provided a bank account name yet.
    /// </summary>
    [MaxLength(30)]
    public string? BankAccountName { get; set; }

    [Newtonsoft.Json.JsonIgnore]
    [InverseProperty(nameof(Password.Creator))]
    public IEnumerable<Password> CreatedPasswords { get; init; } = new List<Password>();

    [Newtonsoft.Json.JsonIgnore]
    [InverseProperty(nameof(PasswordShare.SharedWithUser))]
    public IEnumerable<PasswordShare> PasswordsSharedWithMe { get; init; } = new List<PasswordShare>();

    [Newtonsoft.Json.JsonIgnore]
    [InverseProperty(nameof(Payroll.Owner))]
    public IEnumerable<Payroll> Payrolls { get; init; } = new List<Payroll>();

    [Newtonsoft.Json.JsonIgnore]
    [InverseProperty(nameof(SshKey.Owner))]
    public IEnumerable<SshKey> SshKeys { get; init; } = new List<SshKey>();

    [Newtonsoft.Json.JsonIgnore]
    [InverseProperty(nameof(Incident.Owner))]
    public IEnumerable<Incident> OwnedIncidents { get; init; } = new List<Incident>();

    [Newtonsoft.Json.JsonIgnore]
    [InverseProperty(nameof(Incident.IM))]
    public IEnumerable<Incident> ManagedIncidents { get; init; } = new List<Incident>();

    [Newtonsoft.Json.JsonIgnore]
    [InverseProperty(nameof(IncidentComment.Author))]
    public IEnumerable<IncidentComment> IncidentComments { get; init; } = new List<IncidentComment>();

    [MaxLength(255)]
    public string? ManagerId { get; set; }

    [ForeignKey(nameof(ManagerId))]
    public User? Manager { get; set; }

    [Newtonsoft.Json.JsonIgnore]
    [InverseProperty(nameof(Manager))]
    public IEnumerable<User> Reports { get; init; } = new List<User>();

    [Newtonsoft.Json.JsonIgnore]
    [InverseProperty(nameof(LeaveBalance.User))]
    public IEnumerable<LeaveBalance> LeaveBalances { get; init; } = new List<LeaveBalance>();

    [Newtonsoft.Json.JsonIgnore]
    [InverseProperty(nameof(LeaveApplication.User))]
    public IEnumerable<LeaveApplication> LeaveApplications { get; init; } = new List<LeaveApplication>();

    [Newtonsoft.Json.JsonIgnore]
    [InverseProperty(nameof(OnboardingTaskLog.User))]
    public IEnumerable<OnboardingTaskLog> OnboardingTaskLogs { get; init; } = new List<OnboardingTaskLog>();

    [Newtonsoft.Json.JsonIgnore]
    [InverseProperty(nameof(Asset.Assignee))]
    public IEnumerable<Asset> AssignedAssets { get; init; } = new List<Asset>();

    [Newtonsoft.Json.JsonIgnore]
    [InverseProperty(nameof(AssetHistory.Operator))]
    public IEnumerable<AssetHistory> AssetHistories { get; init; } = new List<AssetHistory>();

    [Newtonsoft.Json.JsonIgnore]
    [InverseProperty(nameof(WeeklyReport.User))]
    public IEnumerable<WeeklyReport> WeeklyReports { get; init; } = new List<WeeklyReport>();

    [Newtonsoft.Json.JsonIgnore]
    [InverseProperty(nameof(Notepad.User))]
    public Notepad? Notepad { get; set; }

    public int? SigningEntityId { get; set; }

    [ForeignKey(nameof(SigningEntityId))]
    public CompanyEntity? SigningEntity { get; set; }
}
