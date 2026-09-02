using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.ServersViewModels;

public class CreateServerViewModel : UiStackLayoutViewModel, IValidatableObject
{
    public CreateServerViewModel()
    {
        PageTitle = "Create Server";
    }

    [MaxLength(100, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "IPv4 Address")]
    public string? ServerIp { get; set; }

    [MaxLength(100, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "IPv6 Address")]
    public string? Ipv6Address { get; set; }

    [MaxLength(500, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "Detail Link")]
    public string? DetailLink { get; set; }

    [Display(Name = "Location")]
    public int? LocationId { get; set; }

    [MaxLength(100, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "Hostname")]
    public string? Hostname { get; set; }

    [Display(Name = "Technical owner")]
    public string? TechnicalOwnerId { get; set; }

    [Display(Name = "Provider")]
    public int? ProviderId { get; set; }

    [Display(Name = "Company Entity")]
    public int? CompanyEntityId { get; set; }

    public IEnumerable<Location> AllLocations { get; set; } = new List<Location>();
    public IEnumerable<User> AllOwners { get; set; } = new List<User>();
    public IEnumerable<Provider> AllProviders { get; set; } = new List<Provider>();
    public IEnumerable<CompanyEntity> AllCompanyEntities { get; set; } = new List<CompanyEntity>();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Hostname) &&
            string.IsNullOrWhiteSpace(ServerIp) &&
            string.IsNullOrWhiteSpace(Ipv6Address))
        {
            yield return new ValidationResult(
                "At least one hostname or IP address is required.",
                [nameof(Hostname), nameof(ServerIp), nameof(Ipv6Address)]);
        }

        if (!string.IsNullOrWhiteSpace(Hostname))
        {
            ValidationResult? hostnameError = null;
            try
            {
                InfrastructureValueNormalizer.NormalizeDomain(Hostname);
            }
            catch (FormatException exception)
            {
                hostnameError = new ValidationResult(exception.Message, [nameof(Hostname)]);
            }

            if (hostnameError != null)
            {
                yield return hostnameError;
            }
        }

        var ipv4Error = ValidateIp(ServerIp, System.Net.Sockets.AddressFamily.InterNetwork, nameof(ServerIp));
        if (ipv4Error != null)
        {
            yield return ipv4Error;
        }

        var ipv6Error = ValidateIp(Ipv6Address, System.Net.Sockets.AddressFamily.InterNetworkV6, nameof(Ipv6Address));
        if (ipv6Error != null)
        {
            yield return ipv6Error;
        }
    }

    private static ValidationResult? ValidateIp(
        string? value,
        System.Net.Sockets.AddressFamily family,
        string propertyName)
    {
        try
        {
            InfrastructureValueNormalizer.NormalizeOptionalIp(value, family);
        }
        catch (FormatException exception)
        {
            return new ValidationResult(exception.Message, [propertyName]);
        }

        return null;
    }
}
