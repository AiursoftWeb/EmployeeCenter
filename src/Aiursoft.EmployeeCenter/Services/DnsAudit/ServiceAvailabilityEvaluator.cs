using System.Net;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.DnsAuditViewModels;

namespace Aiursoft.EmployeeCenter.Services.DnsAudit;

public static class ServiceAvailabilityEvaluator
{
    public static bool ShouldAudit(Service service)
    {
        return service.IsAvailabilityAuditEnabled &&
               service.Status == ServiceStatus.Running &&
               GetHttpScheme(service.Protocols) != null;
    }

    public static string? GetHttpScheme(string? protocols)
    {
        if (string.IsNullOrWhiteSpace(protocols))
        {
            return null;
        }

        if (protocols.Contains("HTTPS", StringComparison.OrdinalIgnoreCase))
        {
            return Uri.UriSchemeHttps;
        }

        return protocols.Contains("HTTP", StringComparison.OrdinalIgnoreCase)
            ? Uri.UriSchemeHttp
            : null;
    }

    public static ServiceAvailabilityResult EvaluateDualStack(
        ServiceAvailabilityResult? ipv4,
        ServiceAvailabilityResult? ipv6)
    {
        var healthy = ipv4?.IsHealthy == true && ipv6?.IsHealthy == true;
        return new ServiceAvailabilityResult(
            healthy,
            ipv4?.IsHealthy != true ? ipv4?.StatusCode : ipv6?.IsHealthy != true ? ipv6?.StatusCode : ipv4.StatusCode,
            $"IPv4: {ipv4?.Details ?? "Missing public IPv4 endpoint: public DNS returned no public A address."} " +
            $"IPv6: {ipv6?.Details ?? "Missing public IPv6 endpoint: public DNS returned no public AAAA address."}");
    }

    public static ServiceAvailabilityResult Evaluate(HttpStatusCode statusCode)
    {
        var numericStatusCode = (int)statusCode;
        return new ServiceAvailabilityResult(
            numericStatusCode < 500,
            numericStatusCode,
            $"The public endpoint responded with HTTP {numericStatusCode} ({statusCode}).");
    }
}
