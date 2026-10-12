using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

namespace Aiursoft.EmployeeCenter.Entities;

public class SshKey
{
    [Key]
    public int Id { get; init; }

    [MaxLength(255)]
    public required string OwnerId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(OwnerId))]
    [NotNull]
    public User? Owner { get; set; }

    [MaxLength(100)]
    public required string Name { get; set; }

    [MaxLength(5000)]
    public required string PublicKey { get; set; }

    public DateTime CreationTime { get; init; } = DateTime.UtcNow;
}
