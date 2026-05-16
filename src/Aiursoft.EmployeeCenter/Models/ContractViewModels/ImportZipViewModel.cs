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
    [Display(Name = "Zip File")]
    public IFormFile? ZipFile { get; set; }

    [Display(Name = "Is Public")]
    public bool IsPublic { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Status")]
    public ContractStatus Status { get; set; } = ContractStatus.PendingSignature;

    public int? FolderId { get; set; }
}
