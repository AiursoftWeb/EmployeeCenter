using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

namespace Aiursoft.EmployeeCenter.Entities;

// 实体资产领用/流转记录
public class PhysicalAssetUsage
{
    [Key]
    public Guid Id { get; init; } = Guid.NewGuid();

    public required Guid AssetId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(AssetId))]
    [NotNull]
    public PhysicalAsset? Asset { get; set; }

    public required Guid UserId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(UserId))]
    [NotNull]
    public User? User { get; set; }

    // 状态: Available, InUse, InRepair, Lost, Frozen
    public required AssetStatus Status { get; set; }

    // 具体的资产标签/序列号 (领用时分配)
    [MaxLength(100)]
    public string? AssignedSerialNumber { get; set; }

    public DateTime ApplyTime { get; init; } = DateTime.UtcNow;
    public DateTime? ReturnTime { get; set; }
}
