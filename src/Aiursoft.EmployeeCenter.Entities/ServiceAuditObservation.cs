using System.ComponentModel.DataAnnotations;

namespace Aiursoft.EmployeeCenter.Entities;

public enum ObservedServiceHealth
{
    Unknown = 0,
    Healthy = 1,
    Degraded = 2,
    Unavailable = 3
}

public class ServiceAuditObservation
{
    [Key]
    public long Id { get; set; }

    public long ServiceAuditRunId { get; set; }
    public ServiceAuditRun ServiceAuditRun { get; set; } = null!;

    public int? ServiceId { get; set; }

    [MaxLength(255)]
    public required string Domain { get; set; }

    public ObservedServiceHealth Health { get; set; }

    public int? StatusCode { get; set; }

    [MaxLength(2000)]
    public string? Details { get; set; }

    public DateTime ObservedAt { get; set; } = DateTime.UtcNow;
}
