using System.ComponentModel.DataAnnotations;
using Aiursoft.UiStack.Layout;
using Aiursoft.EmployeeCenter.Services.DnsAudit;

namespace Aiursoft.EmployeeCenter.Models.DnsAuditViewModels;

public sealed class AuditSettingsViewModel : UiStackLayoutViewModel, IValidatableObject
{
    public AuditSettingsViewModel() { PageTitle = "Audit settings"; }

    [Display(Name = "Discover unregistered services in all Cloudflare zones")]
    public bool AuditAllZones { get; set; } = true;

    [Display(Name = "Included Cloudflare zones")]
    [StringLength(4000)]
    public string? IncludedZones { get; set; }

    [Display(Name = "Excluded domains")]
    [StringLength(4000)]
    public string? ExcludedDomains { get; set; }

    public bool IsOverriddenByConfig { get; set; }
    public bool Saved { get; set; }

    public ServiceAuditScope ToScope() => new()
    {
        AuditAllZones = AuditAllZones,
        IncludedZones = ServiceAuditScope.ParseDomains(IncludedZones),
        ExcludedDomains = ServiceAuditScope.ParseDomains(ExcludedDomains)
    };

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        ServiceAuditScope? scope = null;
        try { scope = ToScope(); }
        catch (ArgumentException)
        {
            // The validation result is returned outside the catch block.
        }
        if (scope == null)
            yield return new ValidationResult("Enter domain names only, one per line. URLs, IP addresses and wildcard patterns are not supported.");
        else if (!scope.AuditAllZones && scope.IncludedZones.Length == 0)
            yield return new ValidationResult("Select all Cloudflare zones or enter at least one included zone.", [nameof(IncludedZones)]);
    }
}
