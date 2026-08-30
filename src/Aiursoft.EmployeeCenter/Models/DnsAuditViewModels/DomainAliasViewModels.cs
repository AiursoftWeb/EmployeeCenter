using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Services.DnsAudit;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.DnsAuditViewModels;

public class DomainAliasFormViewModel : UiStackLayoutViewModel, IValidatableObject
{
    [Required(ErrorMessage = "The {0} is required.")]
    [MaxLength(255, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "Alias hostname")]
    public string Domain { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Select a target service.")]
    [Display(Name = "Target service")]
    public int TargetServiceId { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [MaxLength(2048, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "Exact target URL")]
    public string TargetUrl { get; set; } = string.Empty;

    public List<Service> AllServices { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var domain = DnsAuditAnalyzer.NormalizeDomain(Domain);
        if (domain.Length == 0 || domain.StartsWith("*.", StringComparison.Ordinal) ||
            Uri.CheckHostName(domain) == UriHostNameType.Unknown)
        {
            yield return new ValidationResult("Enter a valid non-wildcard hostname.", [nameof(Domain)]);
        }

        if (!DomainAliasRedirectEvaluator.TryNormalizeTargetUrl(TargetUrl, out _, out var error))
        {
            yield return new ValidationResult(error, [nameof(TargetUrl)]);
        }
    }
}

public sealed class CreateDomainAliasViewModel : DomainAliasFormViewModel
{
    public CreateDomainAliasViewModel()
    {
        PageTitle = "Register domain alias";
    }
}

public sealed class EditDomainAliasViewModel : DomainAliasFormViewModel
{
    public EditDomainAliasViewModel()
    {
        PageTitle = "Edit domain alias";
    }

    [Required]
    public int Id { get; set; }
}

public sealed class DomainAliasIndexViewModel : UiStackLayoutViewModel
{
    public DomainAliasIndexViewModel()
    {
        PageTitle = "Domain aliases";
    }

    public required IReadOnlyList<DomainAlias> DomainAliases { get; init; }
}
