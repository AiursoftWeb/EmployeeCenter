using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Aiursoft.EmployeeCenter.Entities;

public class Transaction
{
    [Key]
    public int Id { get; init; }

    [MaxLength(500)]
    public required string Description { get; set; }

    public int SourceAccountId { get; set; }

    [ForeignKey(nameof(SourceAccountId))]
    public FinanceAccount? SourceAccount { get; set; }

    public int DestinationAccountId { get; set; }

    [ForeignKey(nameof(DestinationAccountId))]
    public FinanceAccount? DestinationAccount { get; set; }

    /// <summary>
    /// Amount in Source Account Currency.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Source Currency / Destination Currency.
    /// Default is 1 if same currency.
    /// </summary>
    public decimal ExchangeRate { get; set; } = 1;

    [MaxLength(500)]
    public string? InvoicePath { get; set; }

    [MaxLength(500)]
    public string? MT103Path { get; set; }

    [MaxLength(500)]
    public string? PaymentVoucherPath { get; set; }

    public DateTime TransactionTime { get; set; } = DateTime.UtcNow;

    public int OcrAttemptCount { get; set; }

    public int EmptyResultCount { get; set; }

    public DateTime? LastOcrAttemptTime { get; set; }

    public DateTime CreateTime { get; init; } = DateTime.UtcNow;
}
