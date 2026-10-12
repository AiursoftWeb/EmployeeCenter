using System.ComponentModel.DataAnnotations;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.BlueprintViewModels;

public class EditFolderViewModel : UiStackLayoutViewModel
{
    public EditFolderViewModel()
    {
        PageTitle = "Edit Blueprint Folder";
    }

    [Required]
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    [Display(Name = "Folder Name")]
    public string Name { get; set; } = string.Empty;

    public int? ParentFolderId { get; set; }
}
