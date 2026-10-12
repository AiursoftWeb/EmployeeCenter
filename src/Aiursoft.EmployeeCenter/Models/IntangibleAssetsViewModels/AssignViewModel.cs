using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.IntangibleAssetsViewModels;

public class AssignViewModel : UiStackLayoutViewModel
{
    public AssignViewModel()
    {
        PageTitle = "Assign";
    }

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Asset Id")]
    public Guid AssetId { get; set; }

    [Display(Name = "Name")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Assign to")]
    public string AssigneeId { get; set; } = string.Empty;

    public IEnumerable<User> AllUsers { get; set; } = new List<User>();
}
