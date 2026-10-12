using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

namespace Aiursoft.EmployeeCenter.Entities;

/// <summary>
/// Represents a reimbursement application submitted by an employee.
/// </summary>
public class Reimbursement
{
    [Key]
    public int Id { get; init; }

    /// <summary>
    /// The ID of the user who submitted this reimbursement request
    /// </summary>
    [MaxLength(255)]
    public required string SubmitterId { get; set; }

    /// <summary>
    /// The user who submitted this reimbursement request
    /// </summary>
    [JsonIgnore]
    [ForeignKey(nameof(SubmitterId))]
    [NotNull]
    public User? Submitter { get; set; }

    /// <summary>
    /// When the expense actually happened
    /// </summary>
    public DateTime ExpenseTime { get; set; }

    /// <summary>
    /// When this application was submitted (initially)
    /// </summary>
    public DateTime SubmittedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Purpose of the consumption
    /// </summary>
    [MaxLength(500)]
    public required string Purpose { get; set; }

    /// <summary>
    /// Supporting email or context for this consumption
    /// </summary>
    [MaxLength(500)]
    public string? SupportingEmail { get; set; }

    /// <summary>
    /// Path to the electronic invoice file
    /// </summary>
    [MaxLength(255)]
    public string? InvoicePath { get; set; }

    /// <summary>
    /// Amount of the reimbursement
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Currency of the reimbursement
    /// </summary>
    [MaxLength(10)]
    public string Currency { get; set; } = "CNY";

    /// <summary>
    /// Category of the reimbursement (e.g., Transport, Meals, Procurement, Travel)
    /// </summary>
    [MaxLength(50)]
    public required string Category { get; set; }

    /// <summary>
    /// The source website of the invoice for cross-checking
    /// </summary>
    [MaxLength(1000)]
    public string? InvoiceSourceUrl { get; set; }

    /// <summary>
    /// Current status of this reimbursement
    /// </summary>
    public ReimbursementStatus Status { get; set; } = ReimbursementStatus.Draft;

    /// <summary>
    /// Reviewer's comment
    /// </summary>
    [MaxLength(500)]
    public string? Comment { get; set; }

    /// <summary>
    /// The ID of the user who reviewed (acknowledged, reimbursed, or rejected) this request
    /// </summary>
    [MaxLength(255)]
    public string? ReviewedById { get; set; }

    /// <summary>
    /// The user who reviewed this request
    /// </summary>
    [JsonIgnore]
    [ForeignKey(nameof(ReviewedById))]
    public User? ReviewedBy { get; set; }

    /// <summary>
    /// When this request was reviewed
    /// </summary>
    public DateTime? ReviewedAt { get; set; }
}
