using System.ComponentModel.DataAnnotations;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.ServicesViewModels;

public class CreateServiceViewModel : UiStackLayoutViewModel, IValidatableObject
{
    public CreateServiceViewModel()
    {
        PageTitle = "Create Service";
    }

    [Required(ErrorMessage = "The {0} is required.")]
    [MaxLength(255, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "Domain")]
    public string Domain { get; set; } = string.Empty;

    [Display(Name = "Owner")]
    public int? OwnerId { get; set; }

    [Display(Name = "Cross-Entity Link")]
    public int? CrossEntityLinkId { get; set; }

    [MaxLength(100, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "Protocols")]
    public string? Protocols { get; set; }

    [Display(Name = "Server")]
    public int? ServerId { get; set; }

    [Display(Name = "FRPS Server")]
    public int? FrpsServerId { get; set; }

    [Display(Name = "DNS Provider")]
    public int? DnsProviderId { get; set; }

    [Display(Name = "Via FRPS")]
    public bool IsViaFrps { get; set; }

    [Display(Name = "Cloudflare Proxied")]
    public bool IsCloudflareProxied { get; set; }

    [Display(Name = "Status")]
    public ServiceStatus Status { get; set; }

    [Display(Name = "Purpose")]
    public ServicePurpose Purpose { get; set; }

    [Display(Name = "Authentik Integrated")]
    public bool AuthentikIntegrated { get; set; }

    [Display(Name = "Is Self Developed")]
    public bool IsSelfDeveloped { get; set; }

    [MaxLength(1000, ErrorMessage = "The {0} cannot exceed {1} characters.")]
    [Display(Name = "Remark")]
    public string? Remark { get; set; }

    public List<CompanyEntity> AllOwners { get; set; } = new();
    public List<DnsProvider> AllDnsProviders { get; set; } = new();
    public List<Service> AllServices { get; set; } = new();
    public List<Server> AllServers { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!IsViaFrps)
        {
            yield break;
        }

        if (!ServerId.HasValue)
        {
            yield return new ValidationResult(
                "The running server is required when the service uses FRPS.",
                [nameof(ServerId)]);
        }

        if (!FrpsServerId.HasValue)
        {
            yield return new ValidationResult(
                "The FRPS server is required when the service uses FRPS.",
                [nameof(FrpsServerId)]);
        }
    }
}
