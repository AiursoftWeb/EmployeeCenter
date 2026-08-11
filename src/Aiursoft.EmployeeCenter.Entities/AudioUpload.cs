using System.ComponentModel.DataAnnotations;

namespace Aiursoft.EmployeeCenter.Entities;

public class AudioUpload
{
    [Key]
    public Guid Id { get; set; }

    [MaxLength(255)]
    public required string OwnerId { get; set; }

    [MaxLength(200)]
    public required string FilePath { get; set; }

    public AudioUploadPurpose Purpose { get; set; }

    public int? TargetAudioId { get; set; }

    public DateTime CreatedTime { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresTime { get; set; }

    public DateTime? ConsumedTime { get; set; }

    [MaxLength(32)]
    public string ConcurrencyToken { get; set; } = Guid.NewGuid().ToString("N");
}

public enum AudioUploadPurpose
{
    Create,
    Replace
}
