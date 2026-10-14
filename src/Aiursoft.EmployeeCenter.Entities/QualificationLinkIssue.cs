using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

namespace Aiursoft.EmployeeCenter.Entities;

/// <summary>A ZIP link issuance, not proof that any bytes were downloaded.</summary>
public class QualificationLinkIssue
{
    [Key]
    public Guid Id { get; init; } = Guid.NewGuid();

    public required Guid QualificationDownloadId { get; init; }

    [JsonIgnore]
    [ForeignKey(nameof(QualificationDownloadId))]
    [NotNull]
    public QualificationDownload? QualificationDownload { get; set; }

    [MaxLength(255)]
    public required string IssuedToId { get; init; }

    [MaxLength(100)]
    public required string IssuedToName { get; init; }

    public DateTime IssuedAt { get; init; } = DateTime.UtcNow;
}
