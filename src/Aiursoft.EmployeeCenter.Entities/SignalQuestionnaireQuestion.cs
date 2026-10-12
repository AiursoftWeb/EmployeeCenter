using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

namespace Aiursoft.EmployeeCenter.Entities;

public class SignalQuestionnaireQuestion
{
    [Key]
    public int Id { get; init; }

    public required int QuestionnaireId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(QuestionnaireId))]
    [NotNull]
    public SignalQuestionnaire? Questionnaire { get; set; }

    public required int QuestionId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(QuestionId))]
    [NotNull]
    public SignalQuestion? Question { get; set; }

    public int Order { get; set; }
}
