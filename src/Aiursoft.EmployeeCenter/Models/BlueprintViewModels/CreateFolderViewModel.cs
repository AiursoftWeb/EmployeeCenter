using System.ComponentModel.DataAnnotations;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.BlueprintViewModels;

public class CreateFolderViewModel : UiStackLayoutViewModel
{
    public CreateFolderViewModel()
    {
        PageTitle = "Create New Blueprint Folder";
    }

    public int? ParentFolderId { get; set; }

    [Required]
    [MaxLength(200)]
    [Display(Name = "Folder Name")]
    public string Name { get; set; } = string.Empty;
}
