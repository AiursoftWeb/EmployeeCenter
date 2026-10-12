using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.CollectionRecordsViewModels;

public class CreateViewModel : UiStackLayoutViewModel
{
    public CreateViewModel()
    {
        PageTitle = "Create";
    }

    [Required]
    public int ChannelId { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Expected Amount")]
    public decimal ExpectedAmount { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Actual Amount")]
    public decimal ActualAmount { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Due Date")]
    public DateTime DueDate { get; set; } = DateTime.Today;

    [Display(Name = "Paid Date")]
    public DateTime? PaidDate { get; set; }

    [MaxLength(500, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [RegularExpression(@"^collection-record/.*", ErrorMessage = "Invalid file path.")]
    [Display(Name = "Receipt File")]
    public string? ReceiptPath { get; set; }

    [MaxLength(500, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [RegularExpression(@"^collection-record/.*", ErrorMessage = "Invalid file path.")]
    [Display(Name = "Invoice File")]
    public string? InvoicePath { get; set; }

    [MaxLength(200, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "Transaction ID")]
    public string? TransactionId { get; set; }

    [MaxLength(500, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [RegularExpression(@"^collection-record/.*", ErrorMessage = "Invalid file path.")]
    [Display(Name = "SWIFT / Bank Receipt File")]
    public string? SwiftReceiptPath { get; set; }

    [MaxLength(1000, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "Remark")]
    public string? Remark { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Status")]
    public CollectionRecordStatus Status { get; set; }

    public CollectionChannel? Channel { get; set; }
}
