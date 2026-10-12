using System.ComponentModel.DataAnnotations;

namespace Aiursoft.EmployeeCenter.Entities;

public class InfrastructureChangeLog
{
    [Key]
    public long Id { get; set; }

    [MaxLength(32)]
    public required string ResourceType { get; set; }

    public required int ResourceId { get; set; }

    [MaxLength(32)]
    public required string Action { get; set; }

    [MaxLength(255)]
    public string? ActorUserId { get; set; }

    public string? BeforeJson { get; set; }

    public string? AfterJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
