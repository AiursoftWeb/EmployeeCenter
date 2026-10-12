using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

namespace Aiursoft.EmployeeCenter.Entities;

public class SignalQuestionResponse
{
    [Key]
    public int Id { get; init; }

    public required int SignalResponseId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(SignalResponseId))]
    [NotNull]
    public SignalResponse? SignalResponse { get; set; }

    public required int QuestionId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(QuestionId))]
    [NotNull]
    public SignalQuestion? Question { get; set; }

    public string? Answer { get; set; }
}
