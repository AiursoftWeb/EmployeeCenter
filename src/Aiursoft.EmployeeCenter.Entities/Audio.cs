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

    [MaxLength(200)]
    public string? PendingFilePath { get; set; }

    public AudioMediaStatus MediaStatus { get; set; } = AudioMediaStatus.Ready;

    [MaxLength(1000)]
    public string? MediaProcessingError { get; set; }

    [MaxLength(32)]
    public string? MediaProcessingToken { get; set; }

    public DateTime? MediaProcessingStartedTime { get; set; }

    public int AsrAttemptCount { get; set; }

    public int EmptyResultCount { get; set; }

    public DateTime? LastAsrAttemptTime { get; set; }

    [MaxLength(32)]
    public string? AsrProcessingToken { get; set; }

    [MaxLength(128)]
    public string? AsrActiveTaskId { get; set; }

    [MaxLength(1000)]
    public string? AsrTerminalError { get; set; }

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

    public List<AudioAsrSegment> AsrSegments { get; set; } = [];
}
