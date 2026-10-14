using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.QualificationsViewModels;

public class RecordsViewModel : UiStackLayoutViewModel
{
    public RecordsViewModel() => PageTitle = "Qualification Download Records";
    public List<QualificationDownload> Records { get; set; } = [];
    public int Page { get; set; }
    public bool HasNextPage { get; set; }
    [MaxLength(120)]
    public string? Query { get; set; }
}
