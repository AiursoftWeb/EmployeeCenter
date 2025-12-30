using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace Aiursoft.EmployeeCenter.Entities;

// 虚拟资产访问日志 (查看密码/密钥)
public class VirtualAssetAccessLog
{
    [Key]
    public Guid Id { get; init; } = Guid.NewGuid();

    public required Guid AssetId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(AssetId))]
    public VirtualAsset? Asset { get; set; }

    // 访问者
    public required string UserId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    public DateTime AccessTime { get; init; } = DateTime.UtcNow;

    // 是否校验了 MFA (强制 check)
    public bool MfaVerified { get; set; }

    // 记录访问IP等
    [MaxLength(100)]
    public string? IpAddress { get; set; }
}
