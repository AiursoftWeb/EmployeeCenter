using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

namespace Aiursoft.EmployeeCenter.Entities;

public class BusinessQualification
{
    [Key]
    public int Id { get; init; }

    public required int CompanyEntityId { get; init; }

    [JsonIgnore]
    [ForeignKey(nameof(CompanyEntityId))]
    [NotNull]
    public CompanyEntity? CompanyEntity { get; set; }

    [MaxLength(100)]
    public required string Name { get; init; }

    [MaxLength(50)]
    public required string Version { get; init; }

    [MaxLength(500)]
    public required string OriginalPath { get; init; }

    [MaxLength(64)]
    public required string OriginalSha256 { get; init; }

    // The creator ID is a snapshot, not a cascading account relationship.
    [MaxLength(255)]
    public required string CreatedById { get; init; }

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}
