using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.CollectionChannelsViewModels;

public class CreateViewModel : UiStackLayoutViewModel
{
    public CreateViewModel()
    {
        PageTitle = "Create";
    }

    [Required]
    [Display(Name = "Payer Entity")]
    public int PayerId { get; set; }

    [Required]
    [Display(Name = "Payee Entity")]
    public int PayeeId { get; set; }

    [Display(Name = "Associated Contract")]
    public int? ContractId { get; set; }

    [Required]
    [Display(Name = "Reference Amount")]
    public decimal ReferenceAmount { get; set; }

    [Required]
    [MaxLength(10)]
    [Display(Name = "Currency")]
    public string Currency { get; set; } = "CNY";

    [Required]
    [MaxLength(200)]
    [Display(Name = "Payment Method")]
    public string PaymentMethod { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Start Billing Date")]
    public DateTime StartBillingDate { get; set; } = DateTime.Today;

    [Required]
    [Display(Name = "First Payment Date")]
    public DateTime FirstPaymentDate { get; set; } = DateTime.Today;

    [Display(Name = "Is Recurring")]
    public bool IsRecurring { get; set; }

    [Display(Name = "Recurring Period")]
    public RecurringPeriod RecurringPeriod { get; set; }

    public List<CompanyEntity> AllCompanyEntities { get; set; } = [];
    public List<Contract> AllContracts { get; set; } = [];
}
