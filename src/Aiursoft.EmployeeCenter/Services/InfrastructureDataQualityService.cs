using System.Net.Sockets;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.InfrastructureViewModels;
using Aiursoft.Scanner.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.EmployeeCenter.Services;

public sealed class InfrastructureDataQualityService(EmployeeCenterDbContext context) : IScopedDependency
{
    public async Task<IReadOnlyList<InfrastructureDataQualityIssue>> ScanAsync(
        CancellationToken cancellationToken = default)
    {
        var services = await context.Services.AsNoTracking().ToListAsync(cancellationToken);
        var servers = await context.Servers.AsNoTracking().ToListAsync(cancellationToken);
        var providers = await context.Providers.AsNoTracking().ToListAsync(cancellationToken);
        var dnsProviders = await context.DnsProviders.AsNoTracking().ToListAsync(cancellationToken);
        var issues = new List<InfrastructureDataQualityIssue>();

        foreach (var service in services)
        {
            if (!service.IsRegistryValidated)
            {
                Add(issues, "Info", "Service", service.Id, "LegacyRow",
                    "This row predates registry validation and should be reviewed and saved.");
            }
            if (string.IsNullOrWhiteSpace(service.Name))
            {
                Add(issues, "Warning", "Service", service.Id, "MissingName", "The service has no human-readable name.");
            }
            if (TryNormalizeDomain(service.PrimaryDomain) == null)
            {
                Add(issues, "Error", "Service", service.Id, "InvalidPrimaryDomain",
                    $"'{service.PrimaryDomain}' is not a valid primary domain.");
            }
            if (service.IsViaFrps && (!service.ServerId.HasValue || !service.FrpsServerId.HasValue))
            {
                Add(issues, "Error", "Service", service.Id, "InvalidFrpsAssignment",
                    "A service using FRPS must have both a running server and an FRPS server.");
            }
            if (service.IsViaFrps && service.ServerId == service.FrpsServerId && service.ServerId.HasValue)
            {
                Add(issues, "Error", "Service", service.Id, "SameFrpsServer",
                    "The running server and FRPS server must be different.");
            }
            if (service.AlternativeServiceId == service.Id)
            {
                Add(issues, "Error", "Service", service.Id, "SelfAlternative", "The service references itself as its alternative.");
            }
        }

        AddDuplicateIssues(
            services.Select(service => (service.Id, TryNormalizeDomain(service.PrimaryDomain))),
            "Service", "DuplicatePrimaryDomain", issues);
        AddAlternativeCycleIssues(services, issues);

        var retiredServerIds = servers.Where(server => server.RetiredAt.HasValue).Select(server => server.Id).ToHashSet();
        foreach (var service in services.Where(service => service.RetiredAt == null))
        {
            if ((service.ServerId.HasValue && retiredServerIds.Contains(service.ServerId.Value)) ||
                (service.FrpsServerId.HasValue && retiredServerIds.Contains(service.FrpsServerId.Value)))
            {
                Add(issues, "Error", "Service", service.Id, "RetiredServerReference",
                    "An active service references a retired running or FRPS server.");
            }
        }

        foreach (var server in servers)
        {
            if (!server.IsRegistryValidated)
            {
                Add(issues, "Info", "Server", server.Id, "LegacyRow",
                    "This row predates registry validation and should be reviewed and saved.");
            }
            if (string.IsNullOrWhiteSpace(server.Hostname) &&
                string.IsNullOrWhiteSpace(server.ServerIp) &&
                string.IsNullOrWhiteSpace(server.Ipv6Address))
            {
                Add(issues, "Error", "Server", server.Id, "MissingIdentifier",
                    "At least one hostname or IP address is required.");
            }
            ValidateServerValue(server, server.Hostname, "InvalidHostname", value =>
                InfrastructureValueNormalizer.NormalizeOptionalHostname(value), issues);
            ValidateServerValue(server, server.ServerIp, "InvalidIpv4", value =>
                InfrastructureValueNormalizer.NormalizeOptionalIp(value, AddressFamily.InterNetwork), issues);
            ValidateServerValue(server, server.Ipv6Address, "InvalidIpv6", value =>
                InfrastructureValueNormalizer.NormalizeOptionalIp(value, AddressFamily.InterNetworkV6), issues);
        }

        AddDuplicateIssues(
            servers.Select(server => (server.Id, TryNormalizeDomain(server.Hostname))),
            "Server", "DuplicateHostname", issues);
        AddDuplicateIssues(
            providers.Select(provider => (provider.Id, NormalizeOptionalName(provider.Name))),
            "Provider", "DuplicateProviderName", issues);
        AddDuplicateIssues(
            dnsProviders.Select(provider => (provider.Id, NormalizeOptionalName(provider.Name))),
            "DnsProvider", "DuplicateDnsProviderName", issues);

        return issues
            .OrderByDescending(issue => SeverityOrder(issue.Severity))
            .ThenBy(issue => issue.ResourceType)
            .ThenBy(issue => issue.ResourceId)
            .ThenBy(issue => issue.Code)
            .ToList();
    }

    private static void AddAlternativeCycleIssues(
        IReadOnlyCollection<Service> services,
        ICollection<InfrastructureDataQualityIssue> issues)
    {
        var links = services.ToDictionary(service => service.Id, service => service.AlternativeServiceId);
        foreach (var service in services)
        {
            var visited = new HashSet<int>();
            int? current = service.Id;
            while (current.HasValue && visited.Add(current.Value))
            {
                current = links.GetValueOrDefault(current.Value);
            }
            if (current.HasValue)
            {
                Add(issues, "Error", "Service", service.Id, "AlternativeCycle",
                    "The alternative-service chain contains a cycle.");
            }
        }
    }

    private static void AddDuplicateIssues(
        IEnumerable<(int Id, string? Value)> values,
        string resourceType,
        string code,
        ICollection<InfrastructureDataQualityIssue> issues)
    {
        foreach (var group in values
                     .Where(item => item.Value != null)
                     .GroupBy(item => item.Value!, StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() > 1))
        {
            var ids = group.Select(item => item.Id).Order().ToArray();
            foreach (var id in ids)
            {
                Add(issues, "Error", resourceType, id, code,
                    $"Normalized value '{group.Key}' is shared by IDs {string.Join(", ", ids)}.");
            }
        }
    }

    private static void ValidateServerValue(
        Server server,
        string? value,
        string code,
        Func<string?, string?> normalize,
        ICollection<InfrastructureDataQualityIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        try
        {
            normalize(value);
        }
        catch (FormatException exception)
        {
            Add(issues, "Error", "Server", server.Id, code, exception.Message);
        }
    }

    private static string? TryNormalizeDomain(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try
        {
            return InfrastructureValueNormalizer.NormalizeDomain(value);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string? NormalizeOptionalName(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : InfrastructureValueNormalizer.NormalizeName(value);

    private static void Add(
        ICollection<InfrastructureDataQualityIssue> issues,
        string severity,
        string resourceType,
        int resourceId,
        string code,
        string details) => issues.Add(new InfrastructureDataQualityIssue(
        severity, resourceType, resourceId, code, details));

    private static int SeverityOrder(string severity) => severity switch
    {
        "Error" => 3,
        "Warning" => 2,
        _ => 1
    };
}
