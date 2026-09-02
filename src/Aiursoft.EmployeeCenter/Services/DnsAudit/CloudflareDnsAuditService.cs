using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aiursoft.EmployeeCenter.Configuration;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.DnsAuditViewModels;
using Aiursoft.Scanner.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.EmployeeCenter.Services.DnsAudit;

public sealed class CloudflareDnsAuditService(
    EmployeeCenterDbContext dbContext,
    GlobalSettingsService settingsService,
    IHttpClientFactory httpClientFactory,
    ILogger<CloudflareDnsAuditService> logger) : IScopedDependency
{
    private const string ApiBaseUrl = "https://api.cloudflare.com/client/v4";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<bool> IsConfiguredAsync()
    {
        var token = await settingsService.GetSettingValueAsync(SettingsMap.CloudflareApiToken);
        return !string.IsNullOrWhiteSpace(token);
    }

    public async Task<DnsAuditReport> AuditAsync(CancellationToken cancellationToken = default)
    {
        var token = await settingsService.GetSettingValueAsync(SettingsMap.CloudflareApiToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new CloudflareDnsAuditException("Cloudflare API token is not configured.");
        }

        using var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Aiursoft-EmployeeCenter-DnsAudit/1.0");

        var zones = await GetAllPagesAsync<CloudflareZone>(client, "/zones", cancellationToken);
        var auditRecords = new List<DnsAuditRecord>();
        var totalRecordCount = 0;

        foreach (var zone in zones)
        {
            var records = await GetAllPagesAsync<CloudflareDnsRecord>(
                client,
                $"/zones/{Uri.EscapeDataString(zone.Id)}/dns_records",
                cancellationToken);
            totalRecordCount += records.Count;

            auditRecords.AddRange(records
                .Select(record => new DnsAuditRecord
                {
                    Id = record.Id,
                    ZoneName = zone.Name,
                    Type = record.Type,
                    Name = record.Name,
                    Content = record.Content,
                    Proxied = record.Proxied == true
                }));
        }

        var services = await dbContext.Services
            .AsNoTracking()
            .Where(service => service.RetiredAt == null)
            .Include(service => service.Server)
            .Include(service => service.FrpsServer)
            .Include(service => service.DnsProvider)
            .ToListAsync(cancellationToken);
        var domainAliases = await dbContext.DomainAliases
            .AsNoTracking()
            .Include(alias => alias.TargetService)
            .ToListAsync(cancellationToken);
        var servers = await dbContext.Servers
            .AsNoTracking()
            .Where(server => server.RetiredAt == null)
            .ToListAsync(cancellationToken);

        // Cloudflare's API remains the source of truth for zones managed by
        // Cloudflare because public DNS deliberately hides orange-cloud origins.
        // For every registered service outside those zones, query public DNS and
        // feed the observed effective A/AAAA addresses into the same analyzer.
        var zoneNames = zones
            .Select(zone => DnsAuditAnalyzer.NormalizeDomain(zone.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var publicDnsResolutions = await ResolveDomainOutcomesAsync(
            services
                .Select(service => DnsAuditAnalyzer.NormalizeDomain(service.PrimaryDomain))
                .Concat(domainAliases.Select(alias => DnsAuditAnalyzer.NormalizeDomain(alias.Domain)))
                .Where(domain => domain.Length > 0 && !BelongsToAnyZone(domain, zoneNames)),
            cancellationToken);
        foreach (var (domain, resolution) in publicDnsResolutions.Where(pair => pair.Value.Completed))
        {
            totalRecordCount += resolution.Addresses.Count;
            auditRecords.AddRange(resolution.Addresses.Select(address => new DnsAuditRecord
            {
                Id = $"public-dns:{domain}:{address}",
                ZoneName = "Public DNS",
                Type = IPAddress.Parse(address).AddressFamily == AddressFamily.InterNetwork ? "A" : "AAAA",
                Name = domain,
                Content = address,
                Proxied = false
            }));
        }

        var resolvedCnameAddresses = await ResolveDnsOnlyCnamesAsync(auditRecords, cancellationToken);
        var recordNames = auditRecords
            .Select(record => DnsAuditAnalyzer.NormalizeDomain(record.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var publiclyResolvableManagedDomains = await ResolveDomainsAsync(
            services
                .Select(service => DnsAuditAnalyzer.NormalizeDomain(service.PrimaryDomain))
                .Concat(domainAliases.Select(alias => DnsAuditAnalyzer.NormalizeDomain(alias.Domain)))
                .Where(domain => domain.Length > 0 &&
                                 BelongsToAnyZone(domain, zoneNames) &&
                                 !recordNames.Contains(domain)),
            cancellationToken);
        var aliasRedirectResults = await AuditDomainAliasRedirectsAsync(domainAliases, cancellationToken);
        var serviceAvailabilityResults = await AuditServiceAvailabilityAsync(services, cancellationToken);

        return DnsAuditAnalyzer.Analyze(new DnsAuditInput(
            zones.Select(zone => zone.Name).ToList(),
            totalRecordCount,
            auditRecords,
            services,
            servers,
            resolvedCnameAddresses,
            publiclyResolvableManagedDomains.Keys.ToList(),
            publicDnsResolutions
                .Where(pair => pair.Value.Completed)
                .Select(pair => pair.Key)
                .ToList(),
            publicDnsResolutions
                .Where(pair => !pair.Value.Completed)
                .ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value.Failure ?? "The public DNS query failed.",
                    StringComparer.OrdinalIgnoreCase),
            domainAliases,
            aliasRedirectResults,
            serviceAvailabilityResults));
    }

    private async Task<IReadOnlyDictionary<int, ServiceAvailabilityResult>> AuditServiceAvailabilityAsync(
        IReadOnlyCollection<Service> services,
        CancellationToken cancellationToken)
    {
        var candidates = services
            .Where(ServiceAvailabilityEvaluator.ShouldAudit)
            .Select(service => (
                Service: service,
                Scheme: ServiceAvailabilityEvaluator.GetHttpScheme(service.Protocols)!))
            .ToList();
        var results = new ConcurrentDictionary<int, ServiceAvailabilityResult>();
        var client = httpClientFactory.CreateClient("ServiceAvailabilityAudit");

        await Parallel.ForEachAsync(
            candidates,
            new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = cancellationToken },
            async (candidate, token) =>
            {
                var service = candidate.Service;
                var domain = DnsAuditAnalyzer.NormalizeDomain(service.PrimaryDomain);
                if (domain.Length == 0 || domain.StartsWith("*.", StringComparison.Ordinal))
                {
                    results[service.Id] = new ServiceAvailabilityResult(
                        false,
                        null,
                        "The registered service hostname is invalid.");
                    return;
                }

                var uri = new UriBuilder(candidate.Scheme!, domain) { Path = "/" }.Uri;
                var result = await ServiceAvailabilityRetryPolicy.ExecuteAsync(
                    async attemptToken =>
                    {
                        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                        request.Headers.UserAgent.ParseAdd("Aiursoft-EmployeeCenter-ServiceAudit/1.0");
                        request.Headers.Accept.ParseAdd("text/html,application/json;q=0.9,*/*;q=0.1");
                        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(attemptToken);
                        timeout.CancelAfter(TimeSpan.FromSeconds(10));
                        try
                        {
                            using var response = await client.SendAsync(
                                request,
                                HttpCompletionOption.ResponseHeadersRead,
                                timeout.Token);
                            return ServiceAvailabilityEvaluator.Evaluate(response.StatusCode);
                        }
                        catch (OperationCanceledException) when (!attemptToken.IsCancellationRequested)
                        {
                            return new ServiceAvailabilityResult(
                                false,
                                null,
                                "The public endpoint timed out after 10 seconds.");
                        }
                        catch (HttpRequestException ex)
                        {
                            return new ServiceAvailabilityResult(
                                false,
                                null,
                                $"The public endpoint request failed: {ex.Message}");
                        }
                    },
                    async (failedAttempt, delayToken) =>
                    {
                        // Stagger retries so a shared network flap does not make every probe retry in lockstep.
                        var jitter = TimeSpan.FromMilliseconds(Math.Abs(service.Id % 5) * 150);
                        var backoff = failedAttempt == 1 ? TimeSpan.FromSeconds(1) : TimeSpan.FromSeconds(3);
                        await Task.Delay(backoff + jitter, delayToken);
                    },
                    token);

                results[service.Id] = result;
                if (!result.IsHealthy)
                {
                    logger.LogWarning(
                        "Service availability audit for '{Domain}' failed after {AttemptCount} attempts: {Details}",
                        domain,
                        ServiceAvailabilityRetryPolicy.MaxAttempts,
                        result.Details);
                }
            });

        return results.ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    private async Task<IReadOnlyDictionary<string, DomainAliasRedirectResult>> AuditDomainAliasRedirectsAsync(
        IReadOnlyCollection<DomainAlias> aliases,
        CancellationToken cancellationToken)
    {
        var results = new ConcurrentDictionary<string, DomainAliasRedirectResult>(StringComparer.OrdinalIgnoreCase);
        var client = httpClientFactory.CreateClient("DnsAliasAudit");

        await Parallel.ForEachAsync(
            aliases.Where(alias => alias.Type == DomainAliasType.HttpRedirect),
            new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = cancellationToken },
            async (alias, token) =>
            {
                var domain = DnsAuditAnalyzer.NormalizeDomain(alias.Domain);
                if (domain.Length == 0 || domain.StartsWith("*.", StringComparison.Ordinal))
                {
                    results[domain] = new DomainAliasRedirectResult(
                        false,
                        null,
                        null,
                        "The registered alias hostname is invalid.");
                    return;
                }

                var sourceUri = new UriBuilder(Uri.UriSchemeHttps, domain) { Path = "/" }.Uri;
                using var request = new HttpRequestMessage(HttpMethod.Get, sourceUri);
                request.Headers.UserAgent.ParseAdd("Aiursoft-EmployeeCenter-DnsAliasAudit/1.0");
                request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml;q=0.9,*/*;q=0.1");
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeout.CancelAfter(TimeSpan.FromSeconds(10));
                try
                {
                    using var response = await client.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        timeout.Token);
                    results[domain] = DomainAliasRedirectEvaluator.Evaluate(
                        sourceUri,
                        alias.TargetUrl ?? string.Empty,
                        response.StatusCode,
                        response.Headers.Location);
                }
                catch (OperationCanceledException ex) when (!token.IsCancellationRequested)
                {
                    logger.LogWarning(ex, "Domain alias HTTP audit for '{Domain}' timed out", domain);
                    results[domain] = new DomainAliasRedirectResult(
                        false,
                        null,
                        null,
                        "The alias could not be verified because its HTTPS request timed out after 10 seconds.");
                }
                catch (HttpRequestException ex)
                {
                    logger.LogWarning(ex, "Domain alias HTTP audit for '{Domain}' failed", domain);
                    results[domain] = new DomainAliasRedirectResult(
                        false,
                        null,
                        null,
                        $"The alias HTTPS request failed: {ex.Message}");
                }
            });

        return results.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<IReadOnlyDictionary<string, IReadOnlyCollection<string>>> ResolveDnsOnlyCnamesAsync(
        IReadOnlyCollection<DnsAuditRecord> records,
        CancellationToken cancellationToken)
    {
        var domains = records
            .GroupBy(record => DnsAuditAnalyzer.NormalizeDomain(record.Name), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Any(record => record.Type == "CNAME") && group.All(record => !record.Proxied))
            .Select(group => group.Key)
            .ToList();
        return await ResolveDomainsAsync(domains, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<string, IReadOnlyCollection<string>>> ResolveDomainsAsync(
        IEnumerable<string> sourceDomains,
        CancellationToken cancellationToken)
    {
        var outcomes = await ResolveDomainOutcomesAsync(sourceDomains, cancellationToken);
        return outcomes
            .Where(pair => pair.Value.Completed && pair.Value.Addresses.Count > 0)
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value.Addresses,
                StringComparer.OrdinalIgnoreCase);
    }

    private async Task<IReadOnlyDictionary<string, DnsResolutionResult>> ResolveDomainOutcomesAsync(
        IEnumerable<string> sourceDomains,
        CancellationToken cancellationToken)
    {
        var domains = sourceDomains
            .Select(DnsAuditAnalyzer.NormalizeDomain)
            .Where(domain => domain.Length > 0 && !domain.StartsWith("*.", StringComparison.Ordinal))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var result = new ConcurrentDictionary<string, DnsResolutionResult>(StringComparer.OrdinalIgnoreCase);

        await Parallel.ForEachAsync(
            domains,
            new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = cancellationToken },
            async (domain, token) =>
            {
                using var lookupTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                lookupTimeout.CancelAfter(TimeSpan.FromSeconds(10));
                try
                {
                    var addresses = await Dns.GetHostAddressesAsync(domain, lookupTimeout.Token);
                    result[domain] = new DnsResolutionResult(
                        Completed: true,
                        Addresses: addresses.Select(address => address.ToString()).Distinct().ToList(),
                        Failure: null);
                }
                catch (SocketException ex) when (ex.SocketErrorCode is SocketError.HostNotFound or SocketError.NoData)
                {
                    // An authoritative negative answer is a successful audit result:
                    // the registered hostname currently has no public address record.
                    result[domain] = new DnsResolutionResult(true, [], null);
                }
                catch (OperationCanceledException ex) when (!token.IsCancellationRequested)
                {
                    logger.LogWarning(ex, "Public DNS lookup for '{Domain}' timed out during service audit", domain);
                    result[domain] = new DnsResolutionResult(false, [], "The public DNS lookup timed out after 10 seconds.");
                }
                catch (SocketException ex)
                {
                    logger.LogWarning(ex, "Could not resolve hostname '{Domain}' during service audit", domain);
                    result[domain] = new DnsResolutionResult(
                        false,
                        [],
                        $"The public DNS resolver returned {ex.SocketErrorCode}: {ex.Message}");
                }
            });

        return result.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static bool BelongsToAnyZone(string domain, IReadOnlySet<string> zones)
    {
        return zones.Any(zone =>
            domain.Equals(zone, StringComparison.OrdinalIgnoreCase) ||
            domain.EndsWith($".{zone}", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<List<T>> GetAllPagesAsync<T>(
        HttpClient client,
        string path,
        CancellationToken cancellationToken)
    {
        var allResults = new List<T>();
        for (var page = 1; ; page++)
        {
            var separator = path.Contains('?', StringComparison.Ordinal) ? '&' : '?';
            using var response = await client.GetAsync(
                $"{ApiBaseUrl}{path}{separator}page={page}&per_page=100",
                cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
            CloudflareEnvelope<T>? envelope;
            try
            {
                envelope = JsonSerializer.Deserialize<CloudflareEnvelope<T>>(responseText, JsonOptions);
            }
            catch (JsonException ex)
            {
                throw new CloudflareDnsAuditException("Cloudflare returned an invalid API response.", ex);
            }

            if (!response.IsSuccessStatusCode || envelope is not { Success: true })
            {
                var error = envelope?.Errors.Count > 0
                    ? string.Join("; ", envelope.Errors.Select(item => item.Message))
                    : $"HTTP {(int)response.StatusCode} ({response.ReasonPhrase})";
                throw new CloudflareDnsAuditException($"Cloudflare API request failed: {error}");
            }

            allResults.AddRange(envelope.Result);
            var totalPages = Math.Max(1, envelope.ResultInfo?.TotalPages ?? 1);
            if (page >= totalPages)
            {
                return allResults;
            }
        }
    }

    private sealed class CloudflareEnvelope<T>
    {
        public bool Success { get; init; }
        public List<T> Result { get; init; } = [];
        public List<CloudflareApiError> Errors { get; init; } = [];

        [JsonPropertyName("result_info")]
        public CloudflareResultInfo? ResultInfo { get; init; }
    }

    private sealed class CloudflareApiError
    {
        public string Message { get; init; } = "Unknown Cloudflare API error";
    }

    private sealed class CloudflareResultInfo
    {
        [JsonPropertyName("total_pages")]
        public int TotalPages { get; init; }
    }

    private sealed class CloudflareZone
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
    }

    private sealed class CloudflareDnsRecord
    {
        public required string Id { get; init; }
        public required string Type { get; init; }
        public required string Name { get; init; }
        public required string Content { get; init; }
        public bool? Proxied { get; init; }
    }

    private sealed record DnsResolutionResult(
        bool Completed,
        IReadOnlyCollection<string> Addresses,
        string? Failure);
}

public sealed class CloudflareDnsAuditException : Exception
{
    public CloudflareDnsAuditException(string message) : base(message)
    {
    }

    public CloudflareDnsAuditException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
