using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.SshKeysViewModels;

public class IndexViewModel : UiStackLayoutViewModel
{
    public IndexViewModel()
    {
        PageTitle = "SSH Keys";
    }

    [Display(Name = "SSH Keys")]
    public required List<SshKey> SshKeys { get; set; }

    [Display(Name = "Target User")]
    public required User TargetUser { get; set; }
}
