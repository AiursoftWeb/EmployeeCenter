using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Aiursoft.EmployeeCenter.Entities;

public class AssetHistory
{
    [Key]
    public long Id { get; set; }

    [Required]
    public Guid AssetId { get; set; }

    [ForeignKey(nameof(AssetId))]
    public Asset Asset { get; set; } = null!;

    [Required]
    [MaxLength(20)]
    public required string ActionType { get; set; }

    [MaxLength(50)]
    public string? FieldName { get; set; }

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    [Required]
    [MaxLength(255)]
    public required string OperatorId { get; set; }

    [ForeignKey(nameof(OperatorId))]
    public User Operator { get; set; } = null!;

    [MaxLength(200)]
    public string? Reason { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
