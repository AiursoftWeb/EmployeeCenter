using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.InfrastructureViewModels;

public sealed record InfrastructureDataQualityIssue(
    string Severity,
    string ResourceType,
    int ResourceId,
    string Code,
    string Details);

public sealed class InfrastructureDataQualityViewModel : UiStackLayoutViewModel
{
    public InfrastructureDataQualityViewModel()
    {
        PageTitle = "Infrastructure data quality";
    }

    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;

    public required IReadOnlyList<InfrastructureDataQualityIssue> Issues { get; init; }
}
