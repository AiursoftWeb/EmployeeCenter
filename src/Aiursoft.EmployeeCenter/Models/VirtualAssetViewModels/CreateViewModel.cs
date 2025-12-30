using System.ComponentModel.DataAnnotations;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.VirtualAssetViewModels;

public class CreateViewModel : UiStackLayoutViewModel
{
    public CreateViewModel()
    {
        PageTitle = "Create Virtual Asset";
    }

    [Required]
    [MaxLength(100)]
    [Display(Name = "Account Name")]
    public string? AccountName { get; set; }

    [MaxLength(200)]
    [Display(Name = "Login URL")]
    public string? LoginUrl { get; set; }

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string? Password { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "TOTP Secret")]
    public string? TotpSecret { get; set; }

    [Display(Name = "High Risk")]
    public bool IsHighRisk { get; set; }
}
