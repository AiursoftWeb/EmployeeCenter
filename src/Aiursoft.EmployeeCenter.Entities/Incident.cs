using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace Aiursoft.EmployeeCenter.Entities;

public class Incident
{
    [Key]
    public Guid Id { get; init; } = Guid.NewGuid();

    [MaxLength(100)]
    public required string Title { get; set; }

    [MaxLength(2000)]
    public required string Description { get; set; }

    public IncidentSeverity Severity { get; set; }

    [MaxLength(100)]
    public required string TargetRole { get; set; }

    [MaxLength(255)]
    public string? OwnerId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(OwnerId))]
    public User? Owner { get; set; }

    [MaxLength(255)]
    public string? IMId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IMId))]
    public User? IM { get; set; }

    public IncidentStatus Status { get; set; } = IncidentStatus.Open;

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? MitigatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }

    [MaxLength(2000)]
    public string? MitigationReason { get; set; }

    [MaxLength(2000)]
    public string? ResolutionReason { get; set; }

    [MaxLength(8192)]
    public string? PostMortem { get; set; }

    [InverseProperty(nameof(IncidentComment.Incident))]
    public IEnumerable<IncidentComment> Comments { get; init; } = new List<IncidentComment>();
}
