using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Aiursoft.EmployeeCenter.Entities;

public class SignalQuestionnaire
{
    [Key]
    public int Id { get; init; }

    [MaxLength(200)]
    public required string Title { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreationTime { get; init; } = DateTime.UtcNow;

    [InverseProperty(nameof(SignalQuestionnaireQuestion.Questionnaire))]
    public IEnumerable<SignalQuestionnaireQuestion> Questions { get; init; } = new List<SignalQuestionnaireQuestion>();

    [InverseProperty(nameof(SignalResponse.Questionnaire))]
    public IEnumerable<SignalResponse> Responses { get; init; } = new List<SignalResponse>();
}
