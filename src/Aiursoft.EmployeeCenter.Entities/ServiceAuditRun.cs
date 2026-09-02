using System.ComponentModel.DataAnnotations;

namespace Aiursoft.EmployeeCenter.Entities;

public enum ServiceAuditRunStatus
{
    Pending = 1,
    Running = 2,
    Succeeded = 3,
    Failed = 4,
    NotConfigured = 5
}

public class ServiceAuditRun
{
    [Key]
    public long Id { get; set; }

    public ServiceAuditRunStatus Status { get; set; } = ServiceAuditRunStatus.Pending;

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    [MaxLength(255)]
    public string? RequestedByUserId { get; set; }

    [MaxLength(2000)]
    public string? ErrorMessage { get; set; }

    public int AuditedHostnameCount { get; set; }
    public int ZoneCount { get; set; }
    public int RecordCount { get; set; }
    public int AvailabilityCheckedCount { get; set; }
    public int AvailabilityHealthyCount { get; set; }
    public int CriticalCount { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public int InfoCount { get; set; }

    public List<ServiceAuditIssue> Issues { get; set; } = new();
    public List<ServiceAuditObservation> Observations { get; set; } = new();
}
