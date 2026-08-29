using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace Aiursoft.EmployeeCenter.Entities;

public class Service
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(255)]
    public required string Domain { get; set; }

    public int? OwnerId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(OwnerId))]
    public CompanyEntity? Owner { get; set; }

    public int? CrossEntityLinkId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(CrossEntityLinkId))]
    public Service? CrossEntityLink { get; set; }

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

    public ServiceStatus Status { get; set; } = ServiceStatus.Running;

    public ServicePurpose Purpose { get; set; } = ServicePurpose.Global;

    public bool AuthentikIntegrated { get; set; }

    public bool IsSelfDeveloped { get; set; }

    [MaxLength(1000)]
    public string? Remark { get; set; }

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
