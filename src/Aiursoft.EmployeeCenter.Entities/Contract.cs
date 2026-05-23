using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Aiursoft.EmployeeCenter.Entities;

public class Contract
{
    [Key]
    public int Id { get; set; }

    [MaxLength(200)]
    public required string Name { get; set; }

    [MaxLength(200)]
    public required string FilePath { get; set; }

    public ContractStatus Status { get; set; }

    public bool IsPublic { get; set; }

    public int OcrAttemptCount { get; set; }

    public int EmptyResultCount { get; set; }

    public DateTime? LastOcrAttemptTime { get; set; }

    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    public int? FolderId { get; set; }
    [ForeignKey(nameof(FolderId))]
    public ContractFolder? Folder { get; set; }

    public List<CollectionChannel> CollectionChannels { get; set; } = [];
}
