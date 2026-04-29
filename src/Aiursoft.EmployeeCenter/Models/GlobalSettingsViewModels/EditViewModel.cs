using System.ComponentModel.DataAnnotations;

namespace Aiursoft.EmployeeCenter.Models.GlobalSettingsViewModels;

public class EditViewModel
{

    [Required(ErrorMessage = "The {0} is required.")]
    [MaxLength(100, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "Key")]
    public string Key { get; set; } = string.Empty;

    [MaxLength(4000, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "Value")]
    public string? Value { get; set; }
}
