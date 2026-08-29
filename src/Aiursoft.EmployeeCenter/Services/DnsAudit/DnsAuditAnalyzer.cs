using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Aiursoft.EmployeeCenter.Models.DnsAuditViewModels;

namespace Aiursoft.EmployeeCenter.Services.DnsAudit;

public static partial class DnsAuditAnalyzer
{
    private static readonly HashSet<string> AuditableTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "A", "AAAA", "CNAME"
    };

    public static DnsAuditReport Analyze(DnsAuditInput input)
    {
        var zones = input.ZoneNames
            .Select(NormalizeDomain)
            .Where(zone => zone.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var records = input.Records
            .Where(record => AuditableTypes.Contains(record.Type))
            .Select(record => new NormalizedRecord(
                record.Type.ToUpperInvariant(),
                NormalizeDomain(record.Name),
                record.Content.Trim(),
                record.Proxied))
            .Where(record => record.Name.Length > 0)
            .ToList();

        var recordsByName = records
            .GroupBy(record => record.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var servicesByDomain = input.Services
            .Select(service => (Service: service, Domain: NormalizeDomain(service.Domain)))
            .Where(item => item.Domain.Length > 0)
            .GroupBy(item => item.Domain, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Service).ToList(), StringComparer.OrdinalIgnoreCase);

        var resolvedCnameAddresses = input.ResolvedCnameAddresses
            .ToDictionary(
                pair => NormalizeDomain(pair.Key),
                pair => pair.Value.Select(NormalizeIpAddress).Where(address => address != null).Select(address => address!).ToHashSet(StringComparer.OrdinalIgnoreCase) as IReadOnlyCollection<string>,
                StringComparer.OrdinalIgnoreCase);
        var publiclyResolvableDomains = (input.PubliclyResolvableDomains ?? [])
            .Select(NormalizeDomain)
            .Where(domain => domain.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var issues = new List<DnsAuditIssue>();

        // Wildcards make it possible to publish hostnames without registering
        // each service explicitly, defeating the purpose of the service registry.
        foreach (var wildcardGroup in input.Records
                     .Select(record => new
                     {
                         Name = NormalizeDomain(record.Name),
                         Type = record.Type.ToUpperInvariant()
                     })
                     .Where(record => record.Name.StartsWith("*.", StringComparison.Ordinal))
                     .GroupBy(record => record.Name, StringComparer.OrdinalIgnoreCase))
        {
            issues.Add(new DnsAuditIssue
            {
                Type = DnsAuditIssueType.WildcardDnsRecord,
                Severity = DnsAuditSeverity.Critical,
                Domain = wildcardGroup.Key,
                Details = $"Wildcard DNS is forbidden because it bypasses explicit service registration. Record type(s): {string.Join(", ", wildcardGroup.Select(record => record.Type).Distinct().Order())}."
            });
        }

        // Registry integrity: a hostname is the stable identity used for reconciliation.
        foreach (var (domain, registeredServices) in servicesByDomain.Where(pair => pair.Value.Count > 1))
        {
            issues.Add(new DnsAuditIssue
            {
                Type = DnsAuditIssueType.DuplicateServiceRegistration,
                Severity = DnsAuditSeverity.Warning,
                Domain = domain,
                ServiceId = registeredServices[0].Id,
                Details = $"EmployeeCenter contains {registeredServices.Count} service registrations for the same normalized hostname."
            });
        }

        // 1. Cloudflare exposes a service hostname that is absent from the company registry.
        foreach (var domain in recordsByName.Keys.Where(domain => !servicesByDomain.ContainsKey(domain)))
        {
            issues.Add(new DnsAuditIssue
            {
                Type = DnsAuditIssueType.UnknownDns,
                Severity = DnsAuditSeverity.Warning,
                Domain = domain,
                Details = "Cloudflare has an A, AAAA, or CNAME record for this hostname, but EmployeeCenter has no matching service registration."
            });
        }

        // 2. A service inside an audited Cloudflare zone has no service DNS record.
        foreach (var (domain, registeredServices) in servicesByDomain)
        {
            if (!BelongsToAnyZone(domain, zones))
            {
                foreach (var service in registeredServices)
                {
                    issues.Add(new DnsAuditIssue
                    {
                        Type = DnsAuditIssueType.ServiceOutsideAuditedZone,
                        Severity = DnsAuditSeverity.Warning,
                        Domain = domain,
                        ServiceId = service.Id,
                        Details = service.DnsProvider == null
                            ? "This service is outside every Cloudflare zone visible to the audit token. Its DNS state cannot be audited."
                            : $"This service uses DNS provider '{service.DnsProvider.Name}' and is outside every Cloudflare zone visible to the audit token. Its DNS state cannot be audited."
                    });
                }
                continue;
            }

            if (recordsByName.ContainsKey(domain))
            {
                continue;
            }

            foreach (var service in registeredServices)
            {
                if (publiclyResolvableDomains.Contains(domain))
                {
                    issues.Add(new DnsAuditIssue
                    {
                        Type = DnsAuditIssueType.ManagedDnsOutsideRecordApi,
                        Severity = DnsAuditSeverity.Info,
                        Domain = domain,
                        ServiceId = service.Id,
                        Details = "The hostname resolves publicly but is absent from Cloudflare's standard DNS record API. It may be managed by Load Balancing, R2, Workers, or another Cloudflare product; its origin cannot be reconciled from ordinary DNS records."
                    });
                    continue;
                }

                issues.Add(new DnsAuditIssue
                {
                    Type = DnsAuditIssueType.MissingDns,
                    Severity = service.Status == Entities.ServiceStatus.Running
                        ? DnsAuditSeverity.Critical
                        : DnsAuditSeverity.Warning,
                    Domain = domain,
                    ServiceId = service.Id,
                    Details = $"The registered service is {service.Status}, but no A, AAAA, or CNAME record exists in its Cloudflare zone."
                });
            }
        }

        var effectiveAddressesByDomain = recordsByName.Keys.ToDictionary(
            domain => domain,
            domain => ResolveEffectiveAddresses(
                domain,
                recordsByName,
                resolvedCnameAddresses,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase)),
            StringComparer.OrdinalIgnoreCase);

        // Never silently accept a record whose final destination could not be established.
        foreach (var domain in effectiveAddressesByDomain
                     .Where(pair => pair.Value.Count == 0)
                     .Select(pair => pair.Key))
        {
            issues.Add(new DnsAuditIssue
            {
                Type = DnsAuditIssueType.UnverifiableDnsTarget,
                Severity = DnsAuditSeverity.Info,
                Domain = domain,
                ServiceId = servicesByDomain.GetValueOrDefault(domain)?.FirstOrDefault()?.Id,
                Details = "The A/AAAA/CNAME chain did not yield a verifiable origin IP address, so server ownership cannot be checked."
            });
        }

        // A hostname cannot reliably be both orange-cloud and DNS-only at the same time.
        foreach (var (domain, domainRecords) in recordsByName)
        {
            if (domainRecords.Any(record => record.Proxied) && domainRecords.Any(record => !record.Proxied))
            {
                issues.Add(new DnsAuditIssue
                {
                    Type = DnsAuditIssueType.MixedProxyStatus,
                    Severity = DnsAuditSeverity.Warning,
                    Domain = domain,
                    ServiceId = servicesByDomain.GetValueOrDefault(domain)?.FirstOrDefault()?.Id,
                    Details = "Records for this hostname mix Cloudflare-proxied and DNS-only modes."
                });
            }
        }

        // 3. DNS-only hostnames must be reachable by both IPv4-only and IPv6-only clients.
        foreach (var (domain, domainRecords) in recordsByName)
        {
            if (domainRecords.Any(record => record.Proxied))
            {
                continue;
            }

            var addresses = effectiveAddressesByDomain[domain];
            var hasIpv4 = addresses.Any(address => IPAddress.Parse(address).AddressFamily == AddressFamily.InterNetwork);
            var hasIpv6 = addresses.Any(address => IPAddress.Parse(address).AddressFamily == AddressFamily.InterNetworkV6);
            if (hasIpv4 == hasIpv6)
            {
                continue;
            }

            issues.Add(new DnsAuditIssue
            {
                Type = DnsAuditIssueType.AddressFamilyMismatch,
                Severity = DnsAuditSeverity.Warning,
                Domain = domain,
                ServiceId = servicesByDomain.GetValueOrDefault(domain)?.FirstOrDefault()?.Id,
                Details = hasIpv4
                    ? "This DNS-only hostname resolves to IPv4 but not IPv6."
                    : "This DNS-only hostname resolves to IPv6 but not IPv4."
            });
        }

        // A DNS-only CNAME that cannot reach an address is dangling or cyclic.
        foreach (var (domain, domainRecords) in recordsByName)
        {
            if (domainRecords.All(record => !record.Proxied) &&
                domainRecords.Any(record => record.Type == "CNAME") &&
                effectiveAddressesByDomain[domain].Count == 0)
            {
                issues.Add(new DnsAuditIssue
                {
                    Type = DnsAuditIssueType.DanglingCname,
                    Severity = DnsAuditSeverity.Warning,
                    Domain = domain,
                    ServiceId = servicesByDomain.GetValueOrDefault(domain)?.FirstOrDefault()?.Id,
                    Details = "This DNS-only CNAME did not resolve to any IPv4 or IPv6 address. Its target may be missing or cyclic."
                });
            }
        }

        // 4. Every origin address published through Cloudflare must belong to a registered server.
        var knownServerAddresses = ExtractKnownServerAddresses(input.Servers);
        foreach (var (domain, addresses) in effectiveAddressesByDomain)
        {
            var unknownAddresses = addresses
                .Where(address => !knownServerAddresses.ContainsKey(address))
                .OrderBy(address => address, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (unknownAddresses.Count == 0)
            {
                continue;
            }

            issues.Add(new DnsAuditIssue
            {
                Type = DnsAuditIssueType.UnknownServer,
                Severity = DnsAuditSeverity.Critical,
                Domain = domain,
                ServiceId = servicesByDomain.GetValueOrDefault(domain)?.FirstOrDefault()?.Id,
                Details = $"DNS resolves to unregistered server address(es): {string.Join(", ", unknownAddresses)}."
            });
        }

        // Compare fields that EmployeeCenter explicitly declares with Cloudflare's observed state.
        foreach (var (domain, registeredServices) in servicesByDomain)
        {
            if (!recordsByName.TryGetValue(domain, out var domainRecords))
            {
                continue;
            }

            var isActuallyProxied = domainRecords.Any(record => record.Proxied);
            var actualServerIds = effectiveAddressesByDomain[domain]
                .SelectMany(address => knownServerAddresses.GetValueOrDefault(address) ?? [])
                .ToHashSet();

            foreach (var service in registeredServices)
            {
                if (service.Status == Entities.ServiceStatus.Running &&
                    !service.IsViaFrps &&
                    !service.ServerId.HasValue)
                {
                    issues.Add(new DnsAuditIssue
                    {
                        Type = DnsAuditIssueType.MissingServerAssignment,
                        Severity = DnsAuditSeverity.Warning,
                        Domain = domain,
                        ServiceId = service.Id,
                        Details = "The running service has DNS but no server assignment in EmployeeCenter."
                    });
                }

                if (service.IsCloudflareProxied != isActuallyProxied)
                {
                    issues.Add(new DnsAuditIssue
                    {
                        Type = DnsAuditIssueType.ProxyStatusMismatch,
                        Severity = DnsAuditSeverity.Warning,
                        Domain = domain,
                        ServiceId = service.Id,
                        Details = $"EmployeeCenter expects Cloudflare proxied = {service.IsCloudflareProxied}, but Cloudflare reports {isActuallyProxied}."
                    });
                }

                if (service.Status == Entities.ServiceStatus.Offline)
                {
                    issues.Add(new DnsAuditIssue
                    {
                        Type = DnsAuditIssueType.OfflineServicePublished,
                        Severity = DnsAuditSeverity.Warning,
                        Domain = domain,
                        ServiceId = service.Id,
                        Details = "The service is registered as Offline, but Cloudflare still publishes a service DNS record."
                    });
                }

                if (!service.IsViaFrps &&
                    service.ServerId.HasValue &&
                    actualServerIds.Count > 0 &&
                    !actualServerIds.Contains(service.ServerId.Value))
                {
                    issues.Add(new DnsAuditIssue
                    {
                        Type = DnsAuditIssueType.ServerAssignmentMismatch,
                        Severity = DnsAuditSeverity.Critical,
                        Domain = domain,
                        ServiceId = service.Id,
                        Details = $"DNS points to registered server ID(s) {string.Join(", ", actualServerIds.Order())}, but the service is assigned to server ID {service.ServerId.Value}."
                    });
                }
            }
        }

        return new DnsAuditReport
        {
            ZoneCount = zones.Count,
            RecordCount = input.TotalRecordCount,
            AuditedHostnameCount = recordsByName.Count,
            Issues = issues
                .OrderByDescending(issue => issue.Severity)
                .ThenBy(issue => issue.Type)
                .ThenBy(issue => issue.Domain, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    public static string NormalizeDomain(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var domain = value.Trim();
        if (domain.Contains("://", StringComparison.Ordinal) && Uri.TryCreate(domain, UriKind.Absolute, out var uri))
        {
            domain = uri.Host;
        }

        domain = domain.Trim().TrimEnd('.').ToLowerInvariant();
        var wildcardPrefix = domain.StartsWith("*.", StringComparison.Ordinal) ? "*." : string.Empty;
        if (wildcardPrefix.Length > 0)
        {
            domain = domain[2..];
        }

        try
        {
            domain = new IdnMapping().GetAscii(domain);
        }
        catch (ArgumentException)
        {
            // Keep the normalized source value. The audit remains read-only and will
            // show the malformed registration instead of failing the whole report.
        }

        return wildcardPrefix + domain;
    }

    private static bool BelongsToAnyZone(string domain, IReadOnlySet<string> zones)
    {
        return zones.Any(zone =>
            domain.Equals(zone, StringComparison.OrdinalIgnoreCase) ||
            domain.EndsWith($".{zone}", StringComparison.OrdinalIgnoreCase));
    }

    private static HashSet<string> ResolveEffectiveAddresses(
        string domain,
        IReadOnlyDictionary<string, List<NormalizedRecord>> recordsByName,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> resolvedCnameAddresses,
        HashSet<string> visited)
    {
        if (!visited.Add(domain))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var addresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!recordsByName.TryGetValue(domain, out var domainRecords))
        {
            return addresses;
        }

        foreach (var record in domainRecords)
        {
            if (record.Type is "A" or "AAAA")
            {
                var normalizedAddress = NormalizeIpAddress(record.Content);
                if (normalizedAddress != null)
                {
                    addresses.Add(normalizedAddress);
                }
                continue;
            }

            if (record.Type != "CNAME")
            {
                continue;
            }

            var target = NormalizeDomain(record.Content);
            if (recordsByName.ContainsKey(target))
            {
                addresses.UnionWith(ResolveEffectiveAddresses(target, recordsByName, resolvedCnameAddresses, visited));
            }
            else if (resolvedCnameAddresses.TryGetValue(domain, out var resolvedAddresses))
            {
                addresses.UnionWith(resolvedAddresses);
            }
        }

        return addresses;
    }

    private static Dictionary<string, HashSet<int>> ExtractKnownServerAddresses(IEnumerable<Entities.Server> servers)
    {
        var addresses = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
        foreach (var server in servers)
        {
            if (string.IsNullOrWhiteSpace(server.ServerIp))
            {
                continue;
            }

            foreach (var candidate in AddressSeparatorRegex().Split(server.ServerIp))
            {
                var normalizedAddress = NormalizeIpAddress(candidate.Trim('[', ']'));
                if (normalizedAddress != null)
                {
                    if (!addresses.TryGetValue(normalizedAddress, out var serverIds))
                    {
                        serverIds = [];
                        addresses[normalizedAddress] = serverIds;
                    }
                    serverIds.Add(server.Id);
                }
            }
        }
        return addresses;
    }

    private static string? NormalizeIpAddress(string? value)
    {
        return IPAddress.TryParse(value, out var address) ? address.ToString() : null;
    }

    [GeneratedRegex(@"[\s,;]+")]
    private static partial Regex AddressSeparatorRegex();

    private sealed record NormalizedRecord(string Type, string Name, string Content, bool Proxied);
}
