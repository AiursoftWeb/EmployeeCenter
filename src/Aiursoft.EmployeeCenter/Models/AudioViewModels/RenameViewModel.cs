using System.ComponentModel.DataAnnotations;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.AudioViewModels;

public class RenameViewModel : UiStackLayoutViewModel
{
    public RenameViewModel()
    {
        PageTitle = "Rename Meeting Recording";
    }

    [Required]
    public int Id { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [MaxLength(200, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "Name")]
    public string Name { get; set; } = string.Empty;
}
