using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

namespace Aiursoft.EmployeeCenter.Entities;

public class WeeklyReport
{
    [Key]
    public int Id { get; init; }

    public required string UserId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(UserId))]
    [NotNull]
    public User? User { get; set; }

    public required string Content { get; set; }

    public DateTime WeekStartDate { get; set; }

    public DateTime CreateTime { get; init; } = DateTime.UtcNow;

    [InverseProperty(nameof(WeeklyReportRequirement.WeeklyReport))]
    public IEnumerable<WeeklyReportRequirement> WeeklyReportRequirements { get; set; } = new List<WeeklyReportRequirement>();
}
