using System.ComponentModel.DataAnnotations;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.SshKeysViewModels;

public class CreateViewModel : UiStackLayoutViewModel
{
    public CreateViewModel()
    {
        PageTitle = "Add SSH Key";
    }

    [Display(Name = "Target User Id")]
    public string? TargetUserId { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [MaxLength(255, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "Key Name")]
    public string? Name { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [MaxLength(5000, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "Public Key")]
    public string? PublicKey { get; set; }
}
