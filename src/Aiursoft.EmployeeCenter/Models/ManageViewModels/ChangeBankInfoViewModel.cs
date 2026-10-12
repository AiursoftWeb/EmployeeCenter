using System.ComponentModel.DataAnnotations;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.ManageViewModels;

public class ChangeBankInfoViewModel : UiStackLayoutViewModel
{
    public ChangeBankInfoViewModel()
    {
        PageTitle = "Change Bank Information";
    }

    [Display(Name = "Bank Card Number")]
    [MaxLength(30, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    public string? BankCardNumber { get; set; }

    [Display(Name = "Bank Name")]
    [MaxLength(30, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    public string? BankName { get; set; }

    [Display(Name = "Bank Account Name")]
    [MaxLength(30, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    public string? BankAccountName { get; set; }
}
