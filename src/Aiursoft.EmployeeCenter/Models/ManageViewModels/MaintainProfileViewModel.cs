using System.ComponentModel.DataAnnotations;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.ManageViewModels;

public class MaintainProfileViewModel : UiStackLayoutViewModel
{
    public MaintainProfileViewModel()
    {
        PageTitle = "Maintain Profile";
    }

    [Display(Name = "Legal Name")]
    [MaxLength(100, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    public string? LegalName { get; set; }

    [Display(Name = "Phone Number")]
    [Phone(ErrorMessage = "The {0} is not a valid phone number.")]
    [MaxLength(30, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    public string? PhoneNumber { get; set; }

    [Display(Name = "Bank Account (Card Number)")]
    [MaxLength(30, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    public string? BankAccount { get; set; }

    [Display(Name = "Bank Name (Branch)")]
    [MaxLength(30, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    public string? BankName { get; set; }

    [Display(Name = "Bank Account Name (Holder)")]
    [MaxLength(30, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    public string? BankAccountName { get; set; }
}
