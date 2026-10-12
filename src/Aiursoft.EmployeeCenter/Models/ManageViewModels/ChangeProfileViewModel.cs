using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.ManageViewModels;

public class ChangeProfileViewModel : UiStackLayoutViewModel
{
    public ChangeProfileViewModel()
    {
        PageTitle = "Change Profile";
    }

    [NotNull]
    [Display(Name = "Name")]
    [Required(ErrorMessage = "The {0} is required.")]
    [MaxLength(30, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [MinLength(2, ErrorMessage = "The {0} must be at least {1} characters.")]
    public string? Name { get; set; }
}
