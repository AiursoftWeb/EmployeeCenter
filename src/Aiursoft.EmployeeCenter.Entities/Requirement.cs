using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Aiursoft.EmployeeCenter.Entities;

public class Requirement
{
    [Key]
    public int Id { get; init; }

    [MaxLength(200)]
    public required string Title { get; set; }

    public required string Content { get; set; } // Markdown

    public RequirementStatus Status { get; set; } = RequirementStatus.PendingApproval;

    public DateTime CreationTime { get; init; } = DateTime.UtcNow;

    public DateTime UpdateTime { get; set; } = DateTime.UtcNow;

    public required string SubmitterId { get; set; }

    [ForeignKey(nameof(SubmitterId))]
    public User? Submitter { get; set; }

    [InverseProperty(nameof(RequirementComment.Requirement))]
    public IEnumerable<RequirementComment> Comments { get; init; } = new List<RequirementComment>();
}
