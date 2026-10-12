using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.ContractViewModels;

public class ManageViewModel : UiStackLayoutViewModel
{
    public ManageViewModel()
    {
        PageTitle = "Manage Contracts";
    }

    public int? FolderId { get; set; }

    public ContractFolder? CurrentFolder { get; set; }

    [Display(Name = "Sub Folders")]
    public IEnumerable<ContractFolder> SubFolders { get; set; } = new List<ContractFolder>();

    [Display(Name = "Contracts")]
    public IEnumerable<Contract> Contracts { get; set; } = new List<Contract>();
}
