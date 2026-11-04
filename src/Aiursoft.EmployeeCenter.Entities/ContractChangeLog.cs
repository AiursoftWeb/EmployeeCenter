using System.ComponentModel.DataAnnotations;

namespace Aiursoft.EmployeeCenter.Entities;

public class ContractChangeLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    // An independent key keeps history after the contract is permanently deleted.
    [MaxLength(32)] public string? ContractKey { get; set; }
    [MaxLength(200)] public required string ContractName { get; set; }
    [MaxLength(40)] public required string Action { get; set; }
    [MaxLength(255)] public string? UserId { get; set; }
    [MaxLength(255)] public required string UserName { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
