using System.ComponentModel.DataAnnotations;

namespace Aiursoft.EmployeeCenter.Entities;

public class AudioFileDeletion
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(200)]
    public required string FilePath { get; set; }

    public DateTime CreatedTime { get; set; } = DateTime.UtcNow;

    public int AttemptCount { get; set; }

    public DateTime NextAttemptTime { get; set; } = DateTime.UtcNow;

    [MaxLength(1000)]
    public string? LastError { get; set; }

    public bool IsDeadLetter { get; set; }
}
