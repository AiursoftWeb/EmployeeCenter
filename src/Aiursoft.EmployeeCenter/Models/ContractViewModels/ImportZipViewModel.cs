using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.ContractViewModels;

public class ImportZipViewModel : UiStackLayoutViewModel
{
    public ImportZipViewModel()
    {
        PageTitle = "Import Contracts from Zip";
    }

    [Required(ErrorMessage = "The {0} is required.")]
    [MaxLength(200, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "Zip File")]
    [RegularExpression(@"^contract/.*", ErrorMessage = "Please upload a valid zip file.")]
    public string? ZipFilePath { get; set; }

    [Display(Name = "Is Public")]
    public bool IsPublic { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Status")]
    public ContractStatus Status { get; set; } = ContractStatus.PendingSignature;

    public int? FolderId { get; set; }
}
