using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Aiursoft.EmployeeCenter.Entities;

public class AudioAsrResult
{
    [Key]
    public int AudioId { get; set; }

    [ForeignKey(nameof(AudioId))]
    public Audio? Audio { get; set; }

    public required string PlainText { get; set; }

    public DateTime CreateTime { get; set; } = DateTime.UtcNow;
}
