using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Aiursoft.EmployeeCenter.Entities;

public class SignalQuestion
{
    [Key]
    public int Id { get; init; }

    [MaxLength(500)]
    public required string Title { get; set; }

    public SignalQuestionType Type { get; set; }

    [MaxLength(200)]
    public string? Tags { get; set; }

    public string? Meta { get; set; }

    public DateTime CreationTime { get; init; } = DateTime.UtcNow;

    [InverseProperty(nameof(SignalQuestionnaireQuestion.Question))]
    public IEnumerable<SignalQuestionnaireQuestion> QuestionnaireQuestions { get; init; } = new List<SignalQuestionnaireQuestion>();
}
