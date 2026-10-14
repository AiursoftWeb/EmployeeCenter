using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

namespace Aiursoft.EmployeeCenter.Entities;

public class QualificationDownload
{
    [Key]
    public Guid Id { get; init; }

    public required int BusinessQualificationId { get; init; }

    [JsonIgnore]
    [ForeignKey(nameof(BusinessQualificationId))]
    [NotNull]
    public BusinessQualification? BusinessQualification { get; set; }

    // Snapshots deliberately survive account deletion and subsequent profile/company changes.
    [MaxLength(255)]
    public required string ApplicantId { get; init; }

    [MaxLength(100)]
    public required string ApplicantName { get; init; }

    [MaxLength(100)]
    public required string QualificationName { get; init; }

    [MaxLength(200)]
    public required string CompanyName { get; init; }

    [MaxLength(50)]
    public required string Version { get; init; }

    [MaxLength(64)]
    public required string OriginalSha256 { get; init; }

    [MaxLength(120)]
    public required string Purpose { get; init; }

    [MaxLength(100)]
    public required string Recipient { get; init; }

    [MaxLength(500)]
    public required string GeneratedPath { get; init; }

    [MaxLength(64)]
    public required string GeneratedSha256 { get; init; }

    [MaxLength(500)]
    public string? ArchivePath { get; set; }

    [MaxLength(64)]
    public string? ArchiveSha256 { get; set; }

    public int LinkIssuedCount { get; set; }

    public DateTime? LastLinkIssuedAt { get; set; }

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>Legacy download statistics retained for existing data; no longer updated.</summary>
    public DateTime? LastDownloadedAt { get; set; }

    public int DownloadCount { get; set; }
}
