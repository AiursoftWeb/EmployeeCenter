using System.ComponentModel.DataAnnotations;

namespace Aiursoft.EmployeeCenter.Entities;

// 虚拟资产
public class VirtualAsset
{
    [Key]
    public Guid Id { get; init; } = Guid.NewGuid();

    [MaxLength(100)]
    public required string AccountName { get; set; } // e.g. "Aliyun Root"

    [MaxLength(200)]
    public string? LoginUrl { get; set; }

    // AES-256 加密存储
    [MaxLength(1000)]
    public required string EncryptedPassword { get; set; }

    // TOTP Secret (Encrypted)
    [MaxLength(1000)]
    public string? EncryptedTotpSecret { get; set; }

    // 是否为高危资产 (触发 IM 告警)
    public bool IsHighRisk { get; set; }
}
