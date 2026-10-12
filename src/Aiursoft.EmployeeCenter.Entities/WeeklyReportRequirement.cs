using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace Aiursoft.EmployeeCenter.Entities;

public class WeeklyReportRequirement
{
    [Key]
    public int Id { get; init; }

    public int WeeklyReportId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(WeeklyReportId))]
    public WeeklyReport? WeeklyReport { get; set; }

    public int RequirementId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(RequirementId))]
    public Requirement? Requirement { get; set; }

    public int Hours { get; set; }
}
