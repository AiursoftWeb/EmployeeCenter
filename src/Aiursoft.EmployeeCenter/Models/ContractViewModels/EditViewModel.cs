using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.ContractViewModels;

public class EditViewModel : UiStackLayoutViewModel
{
    public EditViewModel()
    {
        PageTitle = "Edit Contract";
    }

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Id")]
    public int Id { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [MaxLength(200, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "Name")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(150, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "New Contract File (Optional)")]
    [RegularExpression(@"^contract.*", ErrorMessage = "Please upload a valid contract file.")]
    public string? FilePath { get; set; }

    [Display(Name = "Is Public")]
    public bool IsPublic { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Status")]
    public ContractStatus Status { get; set; }

    public int? FolderId { get; set; }
}
