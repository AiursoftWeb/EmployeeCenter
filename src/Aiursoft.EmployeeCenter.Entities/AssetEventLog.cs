using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace Aiursoft.EmployeeCenter.Entities;

// 实体资产流转/审计日志
public class AssetEventLog
{
    [Key]
    public Guid Id { get; init; } = Guid.NewGuid();

    public required Guid AssetId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(AssetId))]
    public PhysicalAsset? Asset { get; set; }

    // 操作人
    public required string OperatorId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(OperatorId))]
    public User? Operator { get; set; }

    public AssetStatus FromStatus { get; set; }
    public AssetStatus ToStatus { get; set; }

    [MaxLength(1000)]
    public string? Remark { get; set; } // 审批意见、维修原因等

    public DateTime EventTime { get; init; } = DateTime.UtcNow;
}
