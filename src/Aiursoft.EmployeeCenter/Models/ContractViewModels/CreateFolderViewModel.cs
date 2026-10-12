using System.ComponentModel.DataAnnotations;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.ContractViewModels;

public class CreateFolderViewModel : UiStackLayoutViewModel
{
    public CreateFolderViewModel()
    {
        PageTitle = "Create New Folder";
    }

    public int? ParentFolderId { get; set; }

    [Required]
    [MaxLength(200)]
    [Display(Name = "Folder Name")]
    public string Name { get; set; } = string.Empty;
}
