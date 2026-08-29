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
            .Include(service => service.Server)
            .Include(service => service.FrpsServer)
            .Include(service => service.DnsProvider)
            .ToListAsync(cancellationToken);
        var servers = await dbContext.Servers
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var resolvedCnameAddresses = await ResolveDnsOnlyCnamesAsync(auditRecords, cancellationToken);
        var recordNames = auditRecords
            .Select(record => DnsAuditAnalyzer.NormalizeDomain(record.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var zoneNames = zones
            .Select(zone => DnsAuditAnalyzer.NormalizeDomain(zone.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var publiclyResolvableManagedDomains = await ResolveDomainsAsync(
            services
                .Select(service => DnsAuditAnalyzer.NormalizeDomain(service.Domain))
                .Where(domain => domain.Length > 0 &&
                                 BelongsToAnyZone(domain, zoneNames) &&
                                 !recordNames.Contains(domain)),
            cancellationToken);

        return DnsAuditAnalyzer.Analyze(new DnsAuditInput(
            zones.Select(zone => zone.Name).ToList(),
            totalRecordCount,
            auditRecords,
            services,
            servers,
            resolvedCnameAddresses,
            publiclyResolvableManagedDomains.Keys.ToList()));
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
        var domains = sourceDomains
            .Select(DnsAuditAnalyzer.NormalizeDomain)
            .Where(domain => domain.Length > 0 && !domain.StartsWith("*.", StringComparison.Ordinal))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var result = new ConcurrentDictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase);

        await Parallel.ForEachAsync(
            domains,
            new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = cancellationToken },
            async (domain, token) =>
            {
                try
                {
                    var addresses = await Dns.GetHostAddressesAsync(domain, token);
                    result[domain] = addresses.Select(address => address.ToString()).Distinct().ToList();
                }
                catch (Exception ex) when (ex is SocketException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
                {
                    logger.LogWarning(ex, "Could not resolve hostname '{Domain}' during DNS audit", domain);
                    result[domain] = [];
                }
            });

        return result
            .Where(pair => pair.Value.Count > 0)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
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
