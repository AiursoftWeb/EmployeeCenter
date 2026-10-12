using System.ComponentModel.DataAnnotations;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.CompanyEntityViewModels;

public class CreateViewModel : UiStackLayoutViewModel
{
    public CreateViewModel()
    {
        PageTitle = "Create Company Entity";
    }

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Company Name (Chinese)")]
    [MaxLength(200, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    public required string CompanyName { get; set; }

    [Display(Name = "Company Name (English)")]
    [MaxLength(200, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    public string? CompanyNameEnglish { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Entity Code (Social Credit Code / BR Number)")]
    [MaxLength(50, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    public required string EntityCode { get; set; }

    [Display(Name = "CI Number (HK)")]
    [MaxLength(50, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    public string? CINumber { get; set; }

    [Display(Name = "Registered Address")]
    [MaxLength(500, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    public string? RegisteredAddress { get; set; }

    [Display(Name = "Office Address")]
    [MaxLength(500, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    public string? OfficeAddress { get; set; }

    [Display(Name = "Zip Code")]
    [MaxLength(20, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    public string? ZipCode { get; set; }

    [Display(Name = "Legal Representative / Director")]
    [MaxLength(100, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    public string? LegalRepresentative { get; set; }

    [Display(Name = "Legal Representative Legal Name")]
    [MaxLength(100, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    public string? LegalRepresentativeLegalName { get; set; }

    [Display(Name = "Company Type")]
    [MaxLength(100, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    public string? CompanyType { get; set; }

    [Display(Name = "Establishment Date")]
    [DataType(DataType.Date)]
    public DateTime? EstablishmentDate { get; set; }

    [Display(Name = "Expiry Date (Business License / BR)")]
    [DataType(DataType.Date)]
    public DateTime? ExpiryDate { get; set; }

    [Display(Name = "Bank Name")]
    [MaxLength(100, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    public string? BankName { get; set; }

    [Display(Name = "Bank Account")]
    [MaxLength(50, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    public string? BankAccount { get; set; }

    [Display(Name = "Bank Account Name")]
    [MaxLength(50, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    public string? BankAccountName { get; set; }

    [Display(Name = "SWIFT Code")]
    [MaxLength(50, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    public string? SwiftCode { get; set; }

    [Display(Name = "Bank Code")]
    [MaxLength(50, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    public string? BankCode { get; set; }

    [Display(Name = "Bank Address")]
    [MaxLength(500, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    public string? BankAddress { get; set; }

    [Display(Name = "Company Logo")]
    [RegularExpression(@"^companyfiles.*", ErrorMessage = "Please upload a valid logo file.")]
    [MaxLength(500, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    public string? LogoPath { get; set; }

    [Display(Name = "Company Seal")]
    [RegularExpression(@"^companyfiles.*", ErrorMessage = "Please upload a valid seal file.")]
    [MaxLength(500, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    public string? SealPath { get; set; }

    [Display(Name = "Business Registration (BR / Business License)")]
    [RegularExpression(@"^(companyfiles|company-certs)/.*", ErrorMessage = "Please upload a valid license file.")]
    [MaxLength(500, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    public string? LicensePath { get; set; }

    [Display(Name = "Organization Certificate (CR / Org Code)")]
    [RegularExpression(@"^company-certs/.*", ErrorMessage = "Please upload a valid certificate file.")]
    [MaxLength(500, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    public string? OrganizationCertificatePath { get; set; }

    [Display(Name = "Registered Capital (Mainland)")]
    [MaxLength(50, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    public string? RegisteredCapital { get; set; }

    [Display(Name = "Operation Status")]
    [MaxLength(50, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    public string? OperationStatus { get; set; }

    [Display(Name = "SCR Location (HK)")]
    [MaxLength(500, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    public string? SCRLocation { get; set; }

    [Display(Name = "Company Secretary (HK)")]
    [MaxLength(100, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    public string? CompanySecretary { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Base Currency")]
    [MaxLength(10, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    public string BaseCurrency { get; set; } = "CNY";

    [Display(Name = "Create corresponding ledger")]
    public bool CreateLedger { get; set; }
}
