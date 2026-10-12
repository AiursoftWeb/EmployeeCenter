using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Aiursoft.EmployeeCenter.Entities;

public class RequirementComment
{
    [Key]
    public int Id { get; init; }

    public required string Content { get; set; }

    public DateTime CreateTime { get; init; } = DateTime.UtcNow;

    public int RequirementId { get; set; }

    [ForeignKey(nameof(RequirementId))]
    public Requirement? Requirement { get; set; }

    public required string AuthorId { get; set; }

    [ForeignKey(nameof(AuthorId))]
    public User? Author { get; set; }

    public int? ParentCommentId { get; set; }

    [ForeignKey(nameof(ParentCommentId))]
    public RequirementComment? ParentComment { get; set; }

    [InverseProperty(nameof(ParentComment))]
    public IEnumerable<RequirementComment> Replies { get; init; } = new List<RequirementComment>();
}
