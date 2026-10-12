using System.ComponentModel.DataAnnotations;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.LedgerViewModels;

public class CreateTransactionViewModel : UiStackLayoutViewModel
{
    public CreateTransactionViewModel()
    {
        PageTitle = "Create Transaction";
    }

    [Display(Name = "Entity Id")]
    public int EntityId { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [MaxLength(500, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "Description")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Source Account")]
    public int SourceAccountId { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Destination Account")]
    public int DestinationAccountId { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [Range(0.0001, double.MaxValue, ErrorMessage = "The {0} must be between {1} and {2}.")]
    [Display(Name = "Amount")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [Range(0.0000001, double.MaxValue, ErrorMessage = "The {0} must be between {1} and {2}.")]
    [Display(Name = "Exchange Rate")]
    public decimal ExchangeRate { get; set; } = 1;

    [Display(Name = "Invoice Path")]
    public string? InvoicePath { get; set; }

    [Display(Name = "MT103 Path")]
    public string? MT103Path { get; set; }

    [Display(Name = "Payment Voucher Path")]
    public string? PaymentVoucherPath { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Transaction Time")]
    public DateTime TransactionTime { get; set; } = DateTime.UtcNow;
}
