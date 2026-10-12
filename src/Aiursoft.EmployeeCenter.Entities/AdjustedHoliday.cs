using System.ComponentModel.DataAnnotations;

namespace Aiursoft.EmployeeCenter.Entities;

/// <summary>
/// Represents a holiday adjustment made by an administrator to override default public holiday rules.
/// For example, marking a weekend as a 'WorkDay' for compensatory work, or a weekday as a 'RestDay'.
/// </summary>
public class AdjustedHoliday
{
    [Key]
    public int Id { get; init; }

    /// <summary>
    /// The date of the adjustment (time portion is ignored).
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Type of adjustment: WorkDay (0) or RestDay (1).
    /// </summary>
    public HolidayType Type { get; set; }

    /// <summary>
    /// Reason or description of the adjustment.
    /// </summary>
    [MaxLength(500)]
    public required string Reason { get; set; }
}
