using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace Aiursoft.EmployeeCenter.Entities;

public class DnsProvider
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public required string Name { get; set; }

    [MaxLength(100)]
    public string? NormalizedName { get; set; }

    [MaxLength(200)]
    public string? Description { get; set; }

    [JsonIgnore]
    public List<Service> Services { get; set; } = new();
}
