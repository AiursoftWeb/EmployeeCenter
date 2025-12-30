using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Aiursoft.EmployeeCenter.Entities;

// 实体资产定义 (库存维度)
public class PhysicalAsset
{
    [Key]
    public Guid Id { get; init; } = Guid.NewGuid();

    [MaxLength(100)]
    public required string Name { get; set; } // e.g. "MacBook Pro M2 16GB"

    [MaxLength(1000)]
    public string? Description { get; init; }

    // 库存逻辑
    public int TotalStock { get; set; }
    public int FrozenStock { get; set; } // 预占
    public int UsedStock { get; set; }   // 已领用

    // 乐观锁版本控制 (支持高并发库存扣减)
    [Timestamp]
    public byte[]? RowVersion { get; set; }

    [InverseProperty(nameof(PhysicalAssetUsage.Asset))]
    public IEnumerable<PhysicalAssetUsage> Usages { get; init; } = new List<PhysicalAssetUsage>();
}
