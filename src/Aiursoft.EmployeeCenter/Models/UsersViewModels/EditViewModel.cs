using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using Aiursoft.CSTools.Attributes;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;
using Microsoft.AspNetCore.Mvc;

namespace Aiursoft.EmployeeCenter.Models.UsersViewModels;

// Manage if a role is selected or not in the UI.

public class EditViewModel : UiStackLayoutViewModel
{
    public EditViewModel()
    {
        PageTitle = "Edit User";
        AllRoles = [];
    }

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "User name")]
    [ValidDomainName]
    public required string UserName { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Name")]
    [MaxLength(30, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [MinLength(2, ErrorMessage = "The {0} must be at least {1} characters.")]
    public required string DisplayName { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [EmailAddress(ErrorMessage = "The {0} is not a valid email address.")]
    [Display(Name = "Email Address")]
    public required string Email { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "Reset Password (leave empty to keep the same password)")]
    [MaxLength(100, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [MinLength(6, ErrorMessage = "The {0} must be at least {1} characters.")]
    public string? Password { get; set; }

    [NotNull]
    [Display(Name = "Avatar file")]
    [Required(ErrorMessage = "The avatar file is required.")]
    [RegularExpression(@"^avatar.*", ErrorMessage = "The avatar file is invalid. Please upload it again.")]
    [MaxLength(150, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [MinLength(2, ErrorMessage = "The {0} must be at least {1} characters.")]
    public string? AvatarUrl { get; set; }

    [Display(Name = "Creation Time")]
    public DateTime CreationTime { get; set; }

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

    [Display(Name = "Manager Display Name")]
    public string? ManagerDisplayName { get; set; }

    [Display(Name = "Signing Entity")]
    public int? SigningEntityId { get; set; }

    public IEnumerable<CompanyEntity> AllCompanyEntities { get; set; } = new List<CompanyEntity>();

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Id")]
    [FromRoute]
    public required string Id { get; set; }

    public List<UserRoleViewModel> AllRoles { get; set; }
}
