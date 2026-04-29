using System.ComponentModel.DataAnnotations;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.InvoiceViewModels;

public enum InvoiceType
{
    [Display(Name = "Receipt / Paid Invoice")]
    Receipt,

    [Display(Name = "Proforma Invoice / Billing Statement")]
    Proforma
}

public class CreateViewModel : UiStackLayoutViewModel
{
    public CreateViewModel()
    {
        PageTitle = "Create";
    }

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Invoice Type")]
    public InvoiceType Type { get; set; } = InvoiceType.Receipt;

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Seller")]
    public int SellerId { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Seller Name")]
    public string SellerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Seller Address")]
    public string SellerAddress { get; set; } = string.Empty;

    [Display(Name = "Seller Code")]
    public string? SellerCode { get; set; }

    [Display(Name = "Seller Contact")]
    public string? SellerContact { get; set; }

    [Display(Name = "Buyer")]
    public int? BuyerId { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Buyer Name")]
    public string BuyerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Buyer Address")]
    public string BuyerAddress { get; set; } = string.Empty;

    [Display(Name = "Buyer CR No")]
    public string? BuyerCRNo { get; set; }

    [Display(Name = "Buyer Attention")]
    public string? BuyerAttn { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Invoice No")]
    public string InvoiceNo { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} is required.")]
    [DataType(DataType.Date)]
    [Display(Name = "Date")]
    public DateTime Date { get; set; } = DateTime.Today;

    [Display(Name = "Contract No")]
    public string? ContractNo { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Currency")]
    public string Currency { get; set; } = "HKD";

    public List<InvoiceItemViewModel> Items { get; set; } = new();

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Bank Name")]
    public string BankName { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Bank Account")]
    public string BankAccount { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Beneficiary Name")]
    public string BeneficiaryName { get; set; } = string.Empty;

    [Display(Name = "SWIFT Code")]
    public string? SwiftCode { get; set; }

    [Display(Name = "Bank Address")]
    public string? BankAddress { get; set; }

    public decimal Subtotal => Items.Sum(i => i.Quantity * i.UnitPrice);
    public decimal Tax { get; set; }
    public decimal TotalDue => Subtotal + Tax;
}
