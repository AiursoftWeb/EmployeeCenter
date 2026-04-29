using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.LedgerViewModels;

public class TransactionsViewModel : UiStackLayoutViewModel
{
    public TransactionsViewModel()
    {
        PageTitle = "Transactions";
    }

    [Display(Name = "Entity")]
    public required CompanyEntity Entity { get; set; }

    [Display(Name = "Transactions")]
    public List<Transaction> Transactions { get; set; } = new();
}
