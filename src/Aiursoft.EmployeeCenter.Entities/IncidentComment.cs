using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

namespace Aiursoft.EmployeeCenter.Entities;

public class IncidentComment
{
    [Key]
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid IncidentId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IncidentId))]
    [NotNull]
    public Incident? Incident { get; set; }

    [MaxLength(255)]
    public string? AuthorId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(AuthorId))]
    public User? Author { get; set; }

    [MaxLength(2000)]
    public required string Content { get; set; }

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public bool IsSystemComment { get; set; }
}
