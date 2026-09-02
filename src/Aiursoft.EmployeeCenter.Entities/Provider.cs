using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace Aiursoft.EmployeeCenter.Entities;

public class Provider
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public required string Name { get; set; }

    [MaxLength(100)]
    public string? NormalizedName { get; set; }

    [JsonIgnore]
    [InverseProperty(nameof(Server.Provider))]
    public IEnumerable<Server> Servers { get; init; } = new List<Server>();
}
