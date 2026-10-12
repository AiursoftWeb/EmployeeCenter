using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace Aiursoft.EmployeeCenter.Entities;

public class AssetCategory
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public required string Name { get; set; }

    [Required]
    [MaxLength(10)]
    public required string Code { get; set; }

    [JsonIgnore]
    public List<AssetModel> Models { get; set; } = new();
}
