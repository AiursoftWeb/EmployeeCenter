using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.ContractViewModels;

public class CreateViewModel : UiStackLayoutViewModel
{
    public CreateViewModel()
    {
        PageTitle = "Create New Contract";
    }

    [Required(ErrorMessage = "The {0} is required.")]
    [MaxLength(200, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "Name")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} is required.")]
    [MaxLength(150, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [MinLength(2, ErrorMessage = "The {0} must be at least {1} characters.")]
    [Display(Name = "Contract File")]
    [RegularExpression(@"^contract.*", ErrorMessage = "Please upload a valid contract file.")]
    public string? FilePath { get; set; }

    [Display(Name = "Is Public")]
    public bool IsPublic { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Status")]
    public ContractStatus Status { get; set; } = ContractStatus.PendingSignature;

    public int? FolderId { get; set; }
}
