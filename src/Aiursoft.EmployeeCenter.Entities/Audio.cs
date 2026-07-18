using System.ComponentModel.DataAnnotations;

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

    public List<AudioAsrResult> AsrResults { get; set; } = [];
}
