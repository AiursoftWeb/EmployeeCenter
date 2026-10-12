using System.ComponentModel.DataAnnotations;

namespace Aiursoft.EmployeeCenter.Models.WeeklyReportViewModels;

public class CreateViewModel
{

    [Required]
    public string Content { get; set; } = string.Empty;

    public string? OnBehalfOf { get; set; }

    public DateTime? WeekStartDate { get; set; }

    public List<WeeklyReportRequirementViewModel>? Requirements { get; set; }
}
