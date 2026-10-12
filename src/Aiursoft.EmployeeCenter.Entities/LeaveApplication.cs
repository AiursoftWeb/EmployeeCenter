using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

namespace Aiursoft.EmployeeCenter.Entities;

/// <summary>
/// Represents a leave application submitted by an employee.
/// </summary>
public class LeaveApplication
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
    /// Type of leave: Annual Leave or Sick Leave
    /// </summary>
    public LeaveType LeaveType { get; set; }

    /// <summary>
    /// Start date of leave (inclusive)
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// End date of leave (inclusive)
    /// </summary>
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Total working days requested (excluding weekends and public holidays)
    /// </summary>
    public decimal TotalDays { get; set; }

    /// <summary>
    /// Reason for leave
    /// </summary>
    [MaxLength(500)]
    public required string Reason { get; set; }

    /// <summary>
    /// Whether this application is pending approval
    /// </summary>
    public bool IsPending { get; set; } = true;

    /// <summary>
    /// Whether this application was approved (relevant only when IsPending = false)
    /// </summary>
    public bool IsApproved { get; set; }

    /// <summary>
    /// When the application was submitted
    /// </summary>
    public DateTime SubmittedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// When the application was reviewed (nullable if not yet reviewed)
    /// </summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>
    /// Whether this application has been withdrawn by the applicant
    /// </summary>
    public bool IsWithdrawn { get; set; }

    /// <summary>
    /// When the application was withdrawn
    /// </summary>
    public DateTime? WithdrawnAt { get; set; }
    /// <summary>
    /// The ID of the user who reviewed (approved or rejected) this application
    /// </summary>
    [MaxLength(255)]
    public string? ReviewedById { get; set; }

    /// <summary>
    /// The user who reviewed (approved or rejected) this application
    /// </summary>
    [ForeignKey(nameof(ReviewedById))]
    public User? ReviewedBy { get; set; }
}
