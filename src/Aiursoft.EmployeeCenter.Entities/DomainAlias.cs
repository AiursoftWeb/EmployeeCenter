using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace Aiursoft.EmployeeCenter.Entities;

public class DomainAlias
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(255)]
    public required string Domain { get; set; }

    public int TargetServiceId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(TargetServiceId))]
    public Service? TargetService { get; set; }

    [Required]
    [MaxLength(2048)]
    public required string TargetUrl { get; set; }

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
