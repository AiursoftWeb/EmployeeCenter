using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.ReimbursementViewModels;

public class IndexViewModel : UiStackLayoutViewModel
{
    public IndexViewModel()
    {
        PageTitle = "My Reimbursements";
    }

    public List<Reimbursement> MyReimbursements { get; set; } = new();
}

public class CreateViewModel : UiStackLayoutViewModel
{
    public CreateViewModel()
    {
        PageTitle = "New Reimbursement Request";
    }

    [Required]
    [Display(Name = "Expense Time")]
    [DataType(DataType.Date)]
    public DateTime ExpenseTime { get; set; } = DateTime.UtcNow.Date;

    [Required]
    [MaxLength(500)]
    [Display(Name = "Purpose")]
    public string Purpose { get; set; } = string.Empty;

    [MaxLength(500)]
    [Display(Name = "Supporting Email")]
    public string? SupportingEmail { get; set; }

    [Display(Name = "Electronic Invoice")]
    [MaxLength(200)]
    [RegularExpression(@"^reimbursement/.*", ErrorMessage = "Invalid file path.")]
    public string? InvoicePath { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
    [Display(Name = "Amount")]
    public decimal Amount { get; set; }

    [Required]
    [MaxLength(10)]
    [Display(Name = "Currency")]
    public string Currency { get; set; } = "CNY";

    public Dictionary<string, string>? CurrencyOptions { get; set; }

    [Required]
    [MaxLength(50)]
    [Display(Name = "Category")]
    public string Category { get; set; } = string.Empty;

    [MaxLength(1000)]
    [Display(Name = "Invoice Source Website")]
    [Url(ErrorMessage = "Please enter a valid URL.")]
    public string? InvoiceSourceUrl { get; set; }

    public bool SaveAsDraft { get; set; }
}

public class EditViewModel : CreateViewModel
{
    public EditViewModel()
    {
        PageTitle = "Edit Reimbursement Request";
    }

    public int Id { get; set; }
    public string? ExistingInvoicePath { get; set; }
}

public class DetailsViewModel : UiStackLayoutViewModel
{
    public DetailsViewModel()
    {
        PageTitle = "Reimbursement Details";
    }

    public required Reimbursement Reimbursement { get; set; }
    public bool CanApprove { get; set; }
    public string? InvoiceUrl { get; set; }
}

public class ManageIndexViewModel : UiStackLayoutViewModel
{
    public ManageIndexViewModel()
    {
        PageTitle = "Reimbursement Approval Center";
    }

    public List<Reimbursement> PendingRequests { get; set; } = new();
    public List<Reimbursement> AcknowledgedRequests { get; set; } = new();
    public List<Reimbursement> HistoryRequests { get; set; } = new();
}

public class ActionViewModel
{

    public int Id { get; set; }

    [Required]
    [MaxLength(500)]
    [Display(Name = "Comment")]
    public string Comment { get; set; } = string.Empty;
}
