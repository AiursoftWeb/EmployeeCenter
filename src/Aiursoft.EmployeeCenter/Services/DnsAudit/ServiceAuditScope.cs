using System.Globalization;
using System.Text.Json;

namespace Aiursoft.EmployeeCenter.Services.DnsAudit;

public sealed class ServiceAuditScope
{
    public bool AuditAllZones { get; init; } = true;
    public string[] IncludedZones { get; init; } = [];
    public string[] ExcludedDomains { get; init; } = [];

    public bool DiscoversZone(string zone) => AuditAllZones ||
        IncludedZones.Contains(DnsAuditAnalyzer.NormalizeDomain(zone), StringComparer.OrdinalIgnoreCase);

    public bool Excludes(string domain) => ExcludedDomains.Any(root => BelongsTo(domain, root));

    public bool DiscoversDomain(string domain) => !Excludes(domain) &&
        (AuditAllZones || IncludedZones.Any(root => BelongsTo(domain, root)));

    private static bool BelongsTo(string domain, string root)
    {
        domain = DnsAuditAnalyzer.NormalizeDomain(domain);
        return domain.Equals(root, StringComparison.OrdinalIgnoreCase) ||
               domain.EndsWith("." + root, StringComparison.OrdinalIgnoreCase);
    }

    public static string[] ParseDomains(string? value) => (value ?? string.Empty)
        .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(Normalize).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray();

    private static string Normalize(string value)
    {
        value = value.Trim().TrimEnd('.');
        try { value = new IdnMapping().GetAscii(value).ToLowerInvariant(); }
        catch (ArgumentException) { throw new ArgumentException("Invalid domain name."); }
        if (value.Length > 253 || Uri.CheckHostName(value) != UriHostNameType.Dns ||
            !value.Contains('.') || value.Split('.').Any(label => label.Length is 0 or > 63 ||
                label.StartsWith('-') || label.EndsWith('-') || label.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-')))
            throw new ArgumentException("Invalid domain name.");
        return value;
    }

    public static ServiceAuditScope Deserialize(string value)
    {
        var scope = JsonSerializer.Deserialize<ServiceAuditScope>(value)
                    ?? throw new JsonException("The audit scope is missing.");
        if (scope.IncludedZones == null || scope.ExcludedDomains == null)
            throw new JsonException("The audit scope lists are missing.");
        var normalized = new ServiceAuditScope
        {
            AuditAllZones = scope.AuditAllZones,
            IncludedZones = ParseDomains(string.Join('\n', scope.IncludedZones)),
            ExcludedDomains = ParseDomains(string.Join('\n', scope.ExcludedDomains))
        };
        if (!normalized.AuditAllZones && normalized.IncludedZones.Length == 0)
            throw new JsonException("At least one included zone is required.");
        return normalized;
    }
}
