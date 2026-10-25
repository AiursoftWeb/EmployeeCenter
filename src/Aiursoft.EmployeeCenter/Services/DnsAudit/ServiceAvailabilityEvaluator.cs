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
            DescribeFamily(ipv4, "IPv4", "A") + " " + DescribeFamily(ipv6, "IPv6", "AAAA"));
    }

    private static string DescribeFamily(ServiceAvailabilityResult? result, string family, string recordType) =>
        result == null
            ? $"{family}: Unavailable. Missing public {family} endpoint: public DNS returned no public {recordType} address."
            : $"{family}: {(result.IsHealthy ? "Reachable" : "Unavailable")}. {result.Details}";

    public static ServiceAvailabilityResult Evaluate(HttpStatusCode statusCode)
    {
        var numericStatusCode = (int)statusCode;
        return new ServiceAvailabilityResult(
            numericStatusCode < 500,
            numericStatusCode,
            $"The public endpoint responded with HTTP {numericStatusCode} ({statusCode}).");
    }
}
