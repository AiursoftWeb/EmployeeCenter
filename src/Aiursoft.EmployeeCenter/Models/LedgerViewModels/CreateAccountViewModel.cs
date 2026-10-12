using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.LedgerViewModels;

public class CreateAccountViewModel : UiStackLayoutViewModel
{
    public CreateAccountViewModel()
    {
        PageTitle = "Create Account";
    }

    [Display(Name = "Entity Id")]
    public int EntityId { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [MaxLength(200, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "Account Name")]
    public string AccountName { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Account Type")]
    public FinanceAccountType AccountType { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [MaxLength(10, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "Currency")]
    public string Currency { get; set; } = "CNY";

    [Display(Name = "Show In Dashboard")]
    public bool ShowInDashboard { get; set; } = true;
}
