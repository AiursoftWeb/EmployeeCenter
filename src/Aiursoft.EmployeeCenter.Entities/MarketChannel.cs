using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Aiursoft.EmployeeCenter.Entities;

public class MarketChannel
{
    [Key]
    public int Id { get; init; }

    [MaxLength(200)]
    public required string Name { get; set; }

    public required string ManagerId { get; set; }

    [ForeignKey(nameof(ManagerId))]
    public User? Manager { get; set; }

    [MaxLength(200)]
    public required string TargetAudience { get; set; }

    public string? Description { get; set; } // Markdown

    public DateTime CreationTime { get; init; } = DateTime.UtcNow;

    public DateTime UpdateTime { get; set; } = DateTime.UtcNow;
}
