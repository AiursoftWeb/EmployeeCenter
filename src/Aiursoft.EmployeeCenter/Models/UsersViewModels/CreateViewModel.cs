using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using Aiursoft.CSTools.Attributes;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.UsersViewModels;

public class CreateViewModel : UiStackLayoutViewModel
{
    public CreateViewModel()
    {
        PageTitle = "Create User";
    }

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "User name")]
    [ValidDomainName]
    public string? UserName { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Name")]
    [NotNull]
    [MaxLength(30, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [MinLength(2, ErrorMessage = "The {0} must be at least {1} characters.")]
    public string? DisplayName { get; set; }

    [EmailAddress(ErrorMessage = "The {0} is not a valid email address.")]
    [Display(Name = "Email Address")]
    [Required(ErrorMessage = "The {0} is required.")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string? Password { get; set; }

    [Display(Name = "Creation Time")]
    public DateTime CreationTime { get; set; } = DateTime.UtcNow;

    [Display(Name = "Job Level")]
    [MaxLength(100, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    public string? JobLevel { get; set; }

    [Display(Name = "Title")]
    [MaxLength(100, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    public string? Title { get; set; }

    [Display(Name = "Legal Name")]
    [MaxLength(100, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    public string? LegalName { get; set; }

    [Display(Name = "Phone Number")]
    [Phone(ErrorMessage = "The {0} is not a valid phone number.")]
    [MaxLength(30, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    public string? PhoneNumber { get; set; }

    [Display(Name = "Base Salary")]
    public decimal BaseSalary { get; set; }

    [Display(Name = "Bank Account (Card Number)")]
    [MaxLength(30, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    public string? BankAccount { get; set; }

    [Display(Name = "Bank Name (Branch)")]
    [MaxLength(30, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    public string? BankName { get; set; }

    [Display(Name = "Bank Account Name (Holder)")]
    [MaxLength(30, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    public string? BankAccountName { get; set; }

    [Display(Name = "Manager")]
    public string? ManagerId { get; set; }

    public string? ManagerDisplayName { get; set; }

    [Display(Name = "Signing Entity")]
    public int? SigningEntityId { get; set; }

    public IEnumerable<CompanyEntity> AllCompanyEntities { get; set; } = new List<CompanyEntity>();
}
