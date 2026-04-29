using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.AssetsViewModels;

public class ManageCategoriesViewModel : UiStackLayoutViewModel
{
    public ManageCategoriesViewModel()
    {
        PageTitle = "Manage Categories";
    }

    public List<AssetCategory> Categories { get; set; } = new();

    [Required(ErrorMessage = "The {0} is required.")]
    [MaxLength(50, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "New Name")]
    public string NewName { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} is required.")]
    [MaxLength(10, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "New Code")]
    public string NewCode { get; set; } = string.Empty;
}
