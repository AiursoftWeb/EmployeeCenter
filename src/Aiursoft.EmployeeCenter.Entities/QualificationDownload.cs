using System.ComponentModel.DataAnnotations;

namespace Aiursoft.EmployeeCenter.Entities;

public class QualificationDownload
{
    public Guid Id { get; set; }
    public int BusinessQualificationId { get; set; }
    public BusinessQualification? BusinessQualification { get; set; }
    // Snapshots deliberately survive account deletion and subsequent profile/company changes.
    [MaxLength(255)] public required string ApplicantId { get; set; }
    [MaxLength(100)] public required string ApplicantName { get; set; }
    [MaxLength(100)] public required string QualificationName { get; set; }
    [MaxLength(200)] public required string CompanyName { get; set; }
    [MaxLength(50)] public required string Version { get; set; }
    [MaxLength(64)] public required string OriginalSha256 { get; set; }
    [MaxLength(120)] public required string Purpose { get; set; }
    [MaxLength(100)] public required string Recipient { get; set; }
    [MaxLength(500)] public required string GeneratedPath { get; set; }
    [MaxLength(64)] public required string GeneratedSha256 { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastDownloadedAt { get; set; }
    public int DownloadCount { get; set; }
}
