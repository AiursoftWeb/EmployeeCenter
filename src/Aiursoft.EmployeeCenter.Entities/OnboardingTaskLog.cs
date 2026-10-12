using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Aiursoft.EmployeeCenter.Entities;

public class OnboardingTaskLog
{
    [Key]
    public int Id { get; set; }

    public required string UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    public int TaskId { get; set; }

    [ForeignKey(nameof(TaskId))]
    public OnboardingTask? Task { get; set; }

    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    public DateTime? CompletionTime { get; set; }
}
