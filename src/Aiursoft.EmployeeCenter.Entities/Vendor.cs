using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace Aiursoft.EmployeeCenter.Entities;

public class Vendor
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public required string Name { get; set; }

    [MaxLength(200)]
    public string? ContactInfo { get; set; }

    [JsonIgnore]
    public List<Asset> Assets { get; set; } = new();
}
