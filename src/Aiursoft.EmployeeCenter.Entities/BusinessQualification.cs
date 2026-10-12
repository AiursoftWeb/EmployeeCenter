using System.ComponentModel.DataAnnotations;

namespace Aiursoft.EmployeeCenter.Entities;

public class BusinessQualification
{
    public int Id { get; set; }
    public int CompanyEntityId { get; set; }
    public CompanyEntity? CompanyEntity { get; set; }
    [MaxLength(100)] public required string Name { get; set; }
    [MaxLength(50)] public required string Version { get; set; }
    [MaxLength(500)] public required string OriginalPath { get; set; }
    [MaxLength(64)] public required string OriginalSha256 { get; set; }
    [MaxLength(255)] public required string CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}
