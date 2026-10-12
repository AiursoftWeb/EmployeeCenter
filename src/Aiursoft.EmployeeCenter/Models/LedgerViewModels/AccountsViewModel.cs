using System.ComponentModel.DataAnnotations;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.LedgerViewModels;

public class AccountsViewModel : UiStackLayoutViewModel
{
    public AccountsViewModel()
    {
        PageTitle = "Accounts";
    }

    [Display(Name = "Entity Id")]
    public int EntityId { get; set; }

    [Display(Name = "Entity Name")]
    public string EntityName { get; set; } = string.Empty;

    [Display(Name = "Accounts")]
    public List<AccountWithBalance> Accounts { get; set; } = new();
}
