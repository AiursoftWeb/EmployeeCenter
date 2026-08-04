using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.IntangibleAssetsViewModels;

public class CreateViewModel : UiStackLayoutViewModel
{
    public CreateViewModel()
    {
        PageTitle = "Create";
    }

    [Required(ErrorMessage = "The {0} is required.")]
    [MaxLength(100, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "Name")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Type")]
    public IntangibleAssetType Type { get; set; }

    [MaxLength(200, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "Supplier")]
    public string? Supplier { get; set; }

    [MaxLength(100, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "Account")]
    public string? Account { get; set; }

    [MaxLength(100, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "Password")]
    public string? Password { get; set; }

    [MaxLength(500, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Url(ErrorMessage = "The {0} is not a valid URL.")]
    [Display(Name = "Management URL")]
    public string? ManagementUrl { get; set; }

    [MaxLength(100, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "Filing Number")]
    public string? FilingNumber { get; set; }

    [MaxLength(200, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "Filing Subject")]
    public string? FilingSubject { get; set; }

    [MaxLength(1000, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "Filing Query Method")]
    public string? FilingQueryMethod { get; set; }

    [Display(Name = "Registration Date")]
    public DateTime? RegistrationDate { get; set; }

    [Display(Name = "Expiration Date")]
    public DateTime? ExpirationDate { get; set; }

    [Display(Name = "Purchase Price")]
    public decimal? PurchasePrice { get; set; }

    [Display(Name = "Currency")]
    [MaxLength(10, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    public string Currency { get; set; } = "CNY";

    [MaxLength(500, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "Trademark Image")]
    public string? TrademarkImageUrl { get; set; }

    [MaxLength(500, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "Invoice File")]
    public string? InvoiceFileUrl { get; set; }

    [MaxLength(500, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "Registration Certificate")]
    public string? RegistrationCertificateUrl { get; set; }

    [Display(Name = "Public")]
    public bool IsPublic { get; set; }

    [Display(Name = "Belongs to Entity")]
    public int? CompanyEntityId { get; set; }

    [MaxLength(255, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "Assignee")]
    public string? AssigneeId { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Status")]
    public IntangibleAssetStatus Status { get; set; }

    [MaxLength(2000, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    public IEnumerable<User> AllUsers { get; set; } = new List<User>();

    public IEnumerable<CompanyEntity> AllCompanyEntities { get; set; } = new List<CompanyEntity>();

    public Dictionary<string, string> CurrencyOptions { get; set; } = new();
}
