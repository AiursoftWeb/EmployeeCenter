using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

namespace Aiursoft.EmployeeCenter.Entities;

public class SignalResponse
{
    [Key]
    public int Id { get; init; }

    public required int QuestionnaireId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(QuestionnaireId))]
    [NotNull]
    public SignalQuestionnaire? Questionnaire { get; set; }

    public required string UserId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(UserId))]
    [NotNull]
    public User? User { get; set; }

    public DateTime SubmitTime { get; init; } = DateTime.UtcNow;

    [InverseProperty(nameof(SignalQuestionResponse.SignalResponse))]
    public IEnumerable<SignalQuestionResponse> QuestionResponses { get; init; } = new List<SignalQuestionResponse>();
}
