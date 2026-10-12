using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Aiursoft.EmployeeCenter.Entities;

public class PromotionHistory
{
    public int Id { get; set; }

    [MaxLength(450)]
    public required string UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    [MaxLength(100)]
    public string? OldJobLevel { get; set; }

    [MaxLength(100)]
    public string? NewJobLevel { get; set; }

    [MaxLength(100)]
    public string? OldTitle { get; set; }

    [MaxLength(100)]
    public string? NewTitle { get; set; }

    public DateTime ChangeTime { get; set; } = DateTime.UtcNow;

    [MaxLength(450)]
    public string? PromoterId { get; set; }

    [ForeignKey(nameof(PromoterId))]
    public User? Promoter { get; set; }
}
