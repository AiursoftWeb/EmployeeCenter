using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.AssetsViewModels;

public class AssignAssetViewModel : UiStackLayoutViewModel
{
    public AssignAssetViewModel()
    {
        PageTitle = "Assign Asset";
    }

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Asset Id")]
    public Guid AssetId { get; set; }

    [Display(Name = "Asset Tag")]
    public string AssetTag { get; set; } = string.Empty;

    [Display(Name = "Model Name")]
    public string ModelName { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Assign To")]
    public string AssigneeId { get; set; } = string.Empty;

    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    public List<User> AllUsers { get; set; } = new();
}
