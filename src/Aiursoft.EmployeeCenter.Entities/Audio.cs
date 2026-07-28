using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace Aiursoft.EmployeeCenter.Entities;

public class Audio
{
    [Key]
    public int Id { get; set; }

    [MaxLength(200)]
    public required string Name { get; set; }

    [MaxLength(200)]
    public required string FilePath { get; set; }

    public int AsrAttemptCount { get; set; }

    public int EmptyResultCount { get; set; }

    public DateTime? LastAsrAttemptTime { get; set; }

    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The user who uploaded this recording.
    /// </summary>
    [MaxLength(255)]
    public string? OwnerId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(OwnerId))]
    public User? Owner { get; set; }

    [JsonIgnore]
    [InverseProperty(nameof(AudioShare.Audio))]
    public List<AudioShare> AudioShares { get; set; } = [];

    public AudioAsrResult? AsrResult { get; set; }
}
