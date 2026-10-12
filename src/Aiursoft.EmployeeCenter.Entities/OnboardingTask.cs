using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Aiursoft.EmployeeCenter.Entities;

public class OnboardingTask
{
    [Key]
    public int Id { get; set; }

    public int Order { get; set; }

    public int ExpectedDurationSeconds { get; set; }

    [MaxLength(100)]
    public required string Title { get; set; }

    public required string Description { get; set; }

    [MaxLength(500)]
    public required string StartLink { get; set; }

    [Newtonsoft.Json.JsonIgnore]
    [InverseProperty(nameof(OnboardingTaskLog.Task))]
    public IEnumerable<OnboardingTaskLog> Logs { get; init; } = new List<OnboardingTaskLog>();
}
