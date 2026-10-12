using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace Aiursoft.EmployeeCenter.Entities;

public class AssetModel
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int CategoryId { get; set; }

    [ForeignKey(nameof(CategoryId))]
    public AssetCategory Category { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    public required string Brand { get; set; }

    [Required]
    [MaxLength(100)]
    public required string ModelName { get; set; }

    [MaxLength(1000)]
    public string? Specs { get; set; }

    [JsonIgnore]
    public List<Asset> Assets { get; set; } = new();
}
