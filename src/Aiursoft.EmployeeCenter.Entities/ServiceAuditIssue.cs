using System.ComponentModel.DataAnnotations;

namespace Aiursoft.EmployeeCenter.Entities;

public class ServiceAuditIssue
{
    [Key]
    public long Id { get; set; }

    public long ServiceAuditRunId { get; set; }
    public ServiceAuditRun ServiceAuditRun { get; set; } = null!;

    public int? ServiceId { get; set; }
    public int? DomainAliasId { get; set; }

    [MaxLength(64)]
    public required string Type { get; set; }

    [MaxLength(32)]
    public required string Severity { get; set; }

    [MaxLength(255)]
    public required string Domain { get; set; }

    [MaxLength(2000)]
    public required string Details { get; set; }

    public DateTime ObservedAt { get; set; } = DateTime.UtcNow;
}
