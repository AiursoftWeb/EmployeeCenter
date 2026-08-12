using System.ComponentModel.DataAnnotations;

namespace Aiursoft.EmployeeCenter.Entities;

public class AudioFileDeletion
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(200)]
    public required string FilePath { get; set; }

    public DateTime CreatedTime { get; set; } = DateTime.UtcNow;
}
