using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Aiursoft.EmployeeCenter.Entities;

public class BankCardChangeLog
{
    public int Id { get; set; }

    [MaxLength(255)]
    public required string UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    [MaxLength(255)]
    public string? OldBankCardNumber { get; set; }
    [MaxLength(255)]
    public string? NewBankCardNumber { get; set; }

    [MaxLength(255)]
    public string? OldBankName { get; set; }
    [MaxLength(255)]
    public string? NewBankName { get; set; }

    [MaxLength(255)]
    public string? OldBankAccountName { get; set; }
    [MaxLength(255)]
    public string? NewBankAccountName { get; set; }

    public DateTime ChangeTime { get; set; } = DateTime.UtcNow;

    [MaxLength(255)]
    public string? ChangedByUserId { get; set; }

    [ForeignKey(nameof(ChangedByUserId))]
    public User? ChangedByUser { get; set; }
}
