using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace Aiursoft.EmployeeCenter.Entities;

public class Server
{
    [Key]
    public int Id { get; set; }

    [MaxLength(100)]
    public string? ServerIp { get; set; }

    [MaxLength(100)]
    public string? Ipv6Address { get; set; }

    [MaxLength(500)]
    public string? DetailLink { get; set; }

    public int? LocationId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(LocationId))]
    public Location? Location { get; set; }

    [MaxLength(100)]
    public string? Hostname { get; set; }

    [MaxLength(255)]
    public string? OwnerId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(OwnerId))]
    public User? Owner { get; set; }

    public int? ProviderId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(ProviderId))]
    public Provider? Provider { get; set; }

    public int? CompanyEntityId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(CompanyEntityId))]
    public CompanyEntity? CompanyEntity { get; set; }

    [JsonIgnore]
    [InverseProperty(nameof(Service.Server))]
    public IEnumerable<Service> Services { get; init; } = new List<Service>();

    [JsonIgnore]
    [InverseProperty(nameof(Service.FrpsServer))]
    public IEnumerable<Service> FrpsServices { get; init; } = new List<Service>();

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
