using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

namespace Aiursoft.EmployeeCenter.Entities;

/// <summary>
/// Stores the leave allocation for a specific user in a specific year.
/// This entity stores only the allocation amounts, not the remaining balance.
/// Remaining balance is calculated dynamically from allocations minus leave applications.
/// </summary>
public class LeaveBalance
{
    [Key]
    public int Id { get; init; }

    [MaxLength(255)]
    public required string UserId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(UserId))]
    [NotNull]
    public User? User { get; set; }

    /// <summary>
    /// The year this allocation applies to (e.g., 2026)
    /// </summary>
    public int Year { get; set; }

    /// <summary>
    /// Total annual leave days allocated for this year (typically 12 days)
    /// </summary>
    public decimal AnnualLeaveAllocation { get; set; }

    /// <summary>
    /// Total sick leave days allocated for this year (typically 7 days)
    /// </summary>
    public decimal SickLeaveAllocation { get; set; }

    /// <summary>
    /// Annual leave carried over from previous year (max 12 days, expires after 1 year)
    /// This represents unused leave from the previous year's AnnualLeaveAllocation only.
    /// Deducted first when taking leave (FIFO).
    /// </summary>
    public decimal CarriedOverAnnualLeave { get; set; }

    public DateTime CreationTime { get; init; } = DateTime.UtcNow;
}
