using System.Net;
using Aiursoft.EmployeeCenter.Models.DnsAuditViewModels;

namespace Aiursoft.EmployeeCenter.Services.DnsAudit;

public static class ServiceAvailabilityEvaluator
{
    public static ServiceAvailabilityResult Evaluate(HttpStatusCode statusCode)
    {
        var numericStatusCode = (int)statusCode;
        return new ServiceAvailabilityResult(
            numericStatusCode < 500,
            numericStatusCode,
            $"The public endpoint responded with HTTP {numericStatusCode} ({statusCode}).");
    }
}
