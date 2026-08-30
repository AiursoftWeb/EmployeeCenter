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

        var aliasesByDomain = (input.DomainAliases ?? [])
            .Select(alias => (Alias: alias, Domain: NormalizeDomain(alias.Domain)))
            .Where(item => item.Domain.Length > 0)
            .GroupBy(item => item.Domain, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Alias).ToList(), StringComparer.OrdinalIgnoreCase);
        var aliasRedirectResults = (input.DomainAliasRedirectResults ??
                                    new Dictionary<string, DomainAliasRedirectResult>())
            .ToDictionary(
                pair => NormalizeDomain(pair.Key),
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);

        var resolvedCnameAddresses = input.ResolvedCnameAddresses
            .ToDictionary(
                pair => NormalizeDomain(pair.Key),
                pair => pair.Value.Select(NormalizeIpAddress).Where(address => address != null).Select(address => address!).ToHashSet(StringComparer.OrdinalIgnoreCase) as IReadOnlyCollection<string>,
                StringComparer.OrdinalIgnoreCase);
        var publiclyResolvableDomains = (input.PubliclyResolvableDomains ?? [])
            .Select(NormalizeDomain)
            .Where(domain => domain.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var publiclyAuditedDomains = (input.PubliclyAuditedDomains ?? [])
            .Select(NormalizeDomain)
            .Where(domain => domain.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var publicDnsLookupFailures = (input.PublicDnsLookupFailures ?? new Dictionary<string, string>())
            .ToDictionary(
                pair => NormalizeDomain(pair.Key),
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);

        var issues = new List<DnsAuditIssue>();

        var availabilityResults = input.ServiceAvailabilityResults ??
                                  new Dictionary<int, ServiceAvailabilityResult>();
        var servicesById = input.Services.ToDictionary(service => service.Id);
        foreach (var (serviceId, result) in availabilityResults.Where(pair => !pair.Value.IsHealthy))
        {
            if (!servicesById.TryGetValue(serviceId, out var service))
            {
                continue;
            }

            issues.Add(new DnsAuditIssue
            {
                Type = DnsAuditIssueType.ServiceUnavailable,
                Severity = DnsAuditSeverity.Critical,
                Domain = NormalizeDomain(service.Domain),
                ServiceId = service.Id,
                Details = result.Details
            });
        }

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
        foreach (var domain in recordsByName.Keys.Where(domain =>
                     !servicesByDomain.ContainsKey(domain) && !aliasesByDomain.ContainsKey(domain)))
        {
            issues.Add(new DnsAuditIssue
            {
                Type = DnsAuditIssueType.UnknownDns,
                Severity = DnsAuditSeverity.Error,
                Domain = domain,
                Details = "Cloudflare has an A, AAAA, or CNAME record for this hostname, but EmployeeCenter has no matching service registration."
            });
        }

        // A registered alias is a managed DNS entry point, not another service.
        // Its first HTTP response must redirect to the exact registered target.
        foreach (var (domain, aliases) in aliasesByDomain)
        {
            if (servicesByDomain.ContainsKey(domain))
            {
                foreach (var alias in aliases)
                {
                    issues.Add(new DnsAuditIssue
                    {
                        Type = DnsAuditIssueType.DomainAliasRedirectMismatch,
                        Severity = DnsAuditSeverity.Critical,
                        Domain = domain,
                        DomainAliasId = alias.Id,
                        Details = "This hostname is registered as both a service and a domain alias. Keep exactly one source of truth."
                    });
                }
                continue;
            }

            foreach (var alias in aliases)
            {
                if (!aliasRedirectResults.TryGetValue(domain, out var redirectResult))
                {
                    issues.Add(new DnsAuditIssue
                    {
                        Type = DnsAuditIssueType.DomainAliasRedirectMismatch,
                        Severity = DnsAuditSeverity.Critical,
                        Domain = domain,
                        DomainAliasId = alias.Id,
                        Details = $"The redirect could not be verified. Expected exactly '{alias.TargetUrl}'."
                    });
                    continue;
                }

                if (!redirectResult.IsMatch)
                {
                    issues.Add(new DnsAuditIssue
                    {
                        Type = DnsAuditIssueType.DomainAliasRedirectMismatch,
                        Severity = DnsAuditSeverity.Critical,
                        Domain = domain,
                        DomainAliasId = alias.Id,
                        Details = redirectResult.Details
                    });
                }
            }
        }

        // 2. Every registered service must have observable DNS. Cloudflare zones
        // are reconciled from the API; all other providers are audited through
        // public recursive DNS results supplied by the collector.
        foreach (var (domain, registeredServices) in servicesByDomain)
        {
            var belongsToCloudflareZone = BelongsToAnyZone(domain, zones);
            if (recordsByName.ContainsKey(domain))
            {
                continue;
            }

            if (!belongsToCloudflareZone)
            {
                foreach (var service in registeredServices)
                {
                    if (publicDnsLookupFailures.TryGetValue(domain, out var failure))
                    {
                        issues.Add(new DnsAuditIssue
                        {
                            Type = DnsAuditIssueType.PublicDnsLookupFailed,
                            Severity = DnsAuditSeverity.Warning,
                            Domain = domain,
                            ServiceId = service.Id,
                            Details = $"The service uses DNS provider '{service.DnsProvider?.Name ?? "an external provider"}', but its public DNS could not be queried reliably. {failure}"
                        });
                    }
                    else if (publiclyAuditedDomains.Contains(domain))
                    {
                        issues.Add(new DnsAuditIssue
                        {
                            Type = DnsAuditIssueType.MissingDns,
                            Severity = service.Status == Entities.ServiceStatus.Running
                                ? DnsAuditSeverity.Critical
                                : DnsAuditSeverity.Warning,
                            Domain = domain,
                            ServiceId = service.Id,
                            Details = $"The registered service is {service.Status}, but public DNS returned no IPv4 or IPv6 address through provider '{service.DnsProvider?.Name ?? "external DNS"}'."
                        });
                    }
                    else
                    {
                        // Compatibility fallback for analyzer callers that have not
                        // supplied a public-DNS collection result.
                        issues.Add(new DnsAuditIssue
                        {
                            Type = DnsAuditIssueType.ServiceOutsideAuditedZone,
                            Severity = DnsAuditSeverity.Warning,
                            Domain = domain,
                            ServiceId = service.Id,
                            Details = service.DnsProvider == null
                                ? "This service is outside every Cloudflare zone and no public DNS observation was supplied."
                                : $"This service uses DNS provider '{service.DnsProvider.Name}', but no public DNS observation was supplied."
                        });
                    }
                }
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

        // 4. Every observed origin address must belong to a registered server.
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

        // Compare fields that EmployeeCenter explicitly declares with observed DNS state.
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
                    !service.ServerId.HasValue)
                {
                    issues.Add(new DnsAuditIssue
                    {
                        Type = DnsAuditIssueType.MissingServerAssignment,
                        Severity = DnsAuditSeverity.Warning,
                        Domain = domain,
                        ServiceId = service.Id,
                        Details = "The running service has DNS but no running server assignment in EmployeeCenter."
                    });
                }

                if (service.IsViaFrps && !service.FrpsServerId.HasValue)
                {
                    issues.Add(new DnsAuditIssue
                    {
                        Type = DnsAuditIssueType.MissingFrpsServerAssignment,
                        Severity = DnsAuditSeverity.Warning,
                        Domain = domain,
                        ServiceId = service.Id,
                        Details = "The service uses FRPS but has no FRPS server assignment in EmployeeCenter."
                    });
                }

                if (BelongsToAnyZone(domain, zones) &&
                    service.IsCloudflareProxied != isActuallyProxied)
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
                        Details = "The service is registered as Offline, but DNS still publishes an address for it."
                    });
                }

                var expectedServerIds = new HashSet<int>();
                if (service.ServerId.HasValue)
                {
                    expectedServerIds.Add(service.ServerId.Value);
                }
                if (service.IsViaFrps && service.FrpsServerId.HasValue)
                {
                    expectedServerIds.Add(service.FrpsServerId.Value);
                }
                if (expectedServerIds.Count > 0 &&
                    actualServerIds.Count > 0 &&
                    !actualServerIds.Overlaps(expectedServerIds))
                {
                    issues.Add(new DnsAuditIssue
                    {
                        Type = DnsAuditIssueType.ServerAssignmentMismatch,
                        Severity = DnsAuditSeverity.Critical,
                        Domain = domain,
                        ServiceId = service.Id,
                        Details = $"DNS points to registered server ID(s) {string.Join(", ", actualServerIds.Order())}, but the service expects running/FRPS server ID(s) {string.Join(", ", expectedServerIds.Order())}."
                    });
                }
            }
        }

        return new DnsAuditReport
        {
            ZoneCount = zones.Count,
            RecordCount = input.TotalRecordCount,
            AuditedHostnameCount = recordsByName.Count,
            AvailabilityCheckedCount = availabilityResults.Count,
            AvailabilityHealthyCount = availabilityResults.Count(pair => pair.Value.IsHealthy),
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
            foreach (var addressField in new[] { server.ServerIp, server.Ipv6Address })
            {
                if (string.IsNullOrWhiteSpace(addressField))
                {
                    continue;
                }

                foreach (var candidate in AddressSeparatorRegex().Split(addressField))
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
