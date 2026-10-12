using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Aiursoft.EmployeeCenter.Entities;

public class ContractOcrResult
{
    [Key]
    public int Id { get; set; }

    public int ContractId { get; set; }

    [ForeignKey(nameof(ContractId))]
    public Contract? Contract { get; set; }

    public required string JsonResult { get; set; }

    public string? PlainText { get; set; }

    public DateTime CreateTime { get; set; } = DateTime.UtcNow;
}
