using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.ReportLineViewModels;

public class IndexViewModel : UiStackLayoutViewModel
{
    public IndexViewModel()
    {
        PageTitle = "Report Line";
    }

    [Display(Name = "Target User")]
    public required User TargetUser { get; set; }

    [Display(Name = "Manager Chain")]
    public List<User> ManagerChain { get; set; } = new();

    [Display(Name = "Subordinates")]
    public List<User> Subordinates { get; set; } = new();

    [Display(Name = "Peers")]
    public List<User> Peers { get; set; } = new();

    [Display(Name = "Is Viewing Self")]
    public bool IsViewingSelf { get; set; }

    [Display(Name = "Error")]
    public string? Error { get; set; }
}
