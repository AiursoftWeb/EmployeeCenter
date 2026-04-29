using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.AssetsViewModels;

public class ManageModelsViewModel : UiStackLayoutViewModel
{
    public ManageModelsViewModel()
    {
        PageTitle = "Manage Models";
    }

    public List<AssetModel> Models { get; set; } = new();
    public List<AssetCategory> AllCategories { get; set; } = new();

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Category")]
    public int NewCategoryId { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [MaxLength(50, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "Brand")]
    public string NewBrand { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} is required.")]
    [MaxLength(100, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "Model Name")]
    public string NewModelName { get; set; } = string.Empty;

    [MaxLength(1000, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "Specs")]
    public string? NewSpecs { get; set; }
}
