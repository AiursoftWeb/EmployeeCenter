using System.ComponentModel.DataAnnotations;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.PhysicalAssetViewModels;

public class CreateViewModel : UiStackLayoutViewModel
{
    public CreateViewModel()
    {
        PageTitle = "Create Physical Asset";
    }

    [Required]
    [MaxLength(100)]
    public string? Name { get; set; }

    [Display(Name = "Total Stock")]
    [Range(0, int.MaxValue)]
    public int TotalStock { get; set; }
}
