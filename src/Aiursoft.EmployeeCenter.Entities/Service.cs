using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace Aiursoft.EmployeeCenter.Entities;

public class Service
{
    [Key]
    public int Id { get; set; }

    [MaxLength(255)]
    public string? Name { get; set; }

    [Required]
    [MaxLength(255)]
    [Column("Domain")]
    public required string PrimaryDomain { get; set; }

    [MaxLength(255)]
    public string? NormalizedPrimaryDomain { get; set; }

    [Column("OwnerId")]
    public int? CompanyEntityId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(CompanyEntityId))]
    public CompanyEntity? CompanyEntity { get; set; }

    [Column("CrossEntityLinkId")]
    public int? AlternativeServiceId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(AlternativeServiceId))]
    public Service? AlternativeService { get; set; }

    [MaxLength(100)]
    public string? Protocols { get; set; } // e.g., HTTPS, TCP, UDP

    public int? ServerId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(ServerId))]
    public Server? Server { get; set; }

    public int? FrpsServerId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(FrpsServerId))]
    public Server? FrpsServer { get; set; }

    public int? DnsProviderId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(DnsProviderId))]
    public DnsProvider? DnsProvider { get; set; }

    public bool IsViaFrps { get; set; }

    public bool IsCloudflareProxied { get; set; }

    public bool IsAvailabilityAuditEnabled { get; set; } = true;

    public ServiceStatus Status { get; set; } = ServiceStatus.Running;

    public ServicePurpose Purpose { get; set; } = ServicePurpose.Global;

    public bool AuthentikIntegrated { get; set; }

    public bool IsSelfDeveloped { get; set; }

    [MaxLength(1000)]
    public string? Remark { get; set; }

    [JsonIgnore]
    public List<DomainAlias> DomainAliases { get; set; } = new();

    /// <summary>
    /// False for rows created before the infrastructure registry validation migration.
    /// Such rows remain readable until an administrator reviews and saves them.
    /// </summary>
    public bool IsRegistryValidated { get; set; }

    [MaxLength(36)]
    [ConcurrencyCheck]
    public string? ConcurrencyToken { get; set; }

    public DateTime? RetiredAt { get; set; }

    [MaxLength(255)]
    public string? RetiredByUserId { get; set; }

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
