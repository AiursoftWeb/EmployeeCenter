using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Aiursoft.EmployeeCenter.Entities;

public class CompanyEntityLog
{
    [Key]
    public int Id { get; set; }

    public int? CompanyEntityId { get; set; }

    [ForeignKey(nameof(CompanyEntityId))]
    public CompanyEntity? CompanyEntity { get; set; }

    [Required]
    public required string UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    public DateTime LogTime { get; set; } = DateTime.UtcNow;

    [Required]
    public required string Action { get; set; } // Create, Update, Delete

    public string? Details { get; set; }

    public string? Snapshot { get; set; }
}
