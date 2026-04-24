using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Aiursoft.EmployeeCenter.Entities;

public class TransactionOcrResult
{
    [Key]
    public int Id { get; set; }

    public int TransactionId { get; set; }

    [ForeignKey(nameof(TransactionId))]
    public Transaction? Transaction { get; set; }

    public TransactionAttachmentType AttachmentType { get; set; }

    public required string JsonResult { get; set; }

    public string? PlainText { get; set; }

    public DateTime CreateTime { get; set; } = DateTime.UtcNow;
}
