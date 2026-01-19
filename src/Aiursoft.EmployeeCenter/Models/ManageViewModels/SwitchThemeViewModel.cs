using System.ComponentModel.DataAnnotations;

namespace Aiursoft.EmployeeCenter.Models.ManageViewModels;

public class SwitchThemeViewModel
{
    [Required]
    public required string Theme { get; set; }
}
