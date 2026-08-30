using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.DnsAuditViewModels;

public enum DnsAuditIssueType
{
    UnknownDns = 1,
    MissingDns = 2,
    AddressFamilyMismatch = 3,
    UnknownServer = 4,
    ProxyStatusMismatch = 5,
    ServerAssignmentMismatch = 6,
    OfflineServicePublished = 7,
    DanglingCname = 8,
    MixedProxyStatus = 9,
    DuplicateServiceRegistration = 10,
    ServiceOutsideAuditedZone = 11,
    UnverifiableDnsTarget = 12,
    MissingServerAssignment = 13,
    WildcardDnsRecord = 14,
    ManagedDnsOutsideRecordApi = 15,
    MissingFrpsServerAssignment = 16,
    PublicDnsLookupFailed = 17,
    DomainAliasRedirectMismatch = 18
}

public enum DnsAuditSeverity
{
    Info = 1,
    Warning = 2,
    Error = 3,
    Critical = 4
}

public sealed class DnsAuditIssue
{
    public required DnsAuditIssueType Type { get; init; }
    public required DnsAuditSeverity Severity { get; init; }
    public required string Domain { get; init; }
    public required string Details { get; init; }
    public int? ServiceId { get; init; }
    public int? DomainAliasId { get; init; }

    public string CheckName => Type switch
    {
        DnsAuditIssueType.UnknownDns => "Unknown DNS record",
        DnsAuditIssueType.MissingDns => "Missing DNS record",
        DnsAuditIssueType.AddressFamilyMismatch => "IPv4/IPv6 mismatch",
        DnsAuditIssueType.UnknownServer => "Unregistered server",
        DnsAuditIssueType.ProxyStatusMismatch => "Cloudflare proxy status mismatch",
        DnsAuditIssueType.ServerAssignmentMismatch => "Server assignment mismatch",
        DnsAuditIssueType.OfflineServicePublished => "Offline service is still published",
        DnsAuditIssueType.DanglingCname => "Dangling or cyclic CNAME",
        DnsAuditIssueType.MixedProxyStatus => "Mixed proxy status",
        DnsAuditIssueType.DuplicateServiceRegistration => "Duplicate service registration",
        DnsAuditIssueType.ServiceOutsideAuditedZone => "DNS audit coverage gap",
        DnsAuditIssueType.UnverifiableDnsTarget => "Unverifiable DNS target",
        DnsAuditIssueType.MissingServerAssignment => "Missing server assignment",
        DnsAuditIssueType.WildcardDnsRecord => "Wildcard DNS record is forbidden",
        DnsAuditIssueType.ManagedDnsOutsideRecordApi => "Cloudflare-managed DNS target",
        DnsAuditIssueType.MissingFrpsServerAssignment => "Missing FRPS server assignment",
        DnsAuditIssueType.PublicDnsLookupFailed => "Public DNS lookup failed",
        DnsAuditIssueType.DomainAliasRedirectMismatch => "Domain alias redirect mismatch",
        _ => Type.ToString()
    };
}

public sealed class DnsAuditReport
{
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
    public int ZoneCount { get; init; }
    public int RecordCount { get; init; }
    public int AuditedHostnameCount { get; init; }
    public required IReadOnlyList<DnsAuditIssue> Issues { get; init; }

    public int CriticalCount => Issues.Count(issue => issue.Severity == DnsAuditSeverity.Critical);
    public int ErrorCount => Issues.Count(issue => issue.Severity == DnsAuditSeverity.Error);
    public int WarningCount => Issues.Count(issue => issue.Severity == DnsAuditSeverity.Warning);
    public int InfoCount => Issues.Count(issue => issue.Severity == DnsAuditSeverity.Info);
}

public sealed class DnsAuditIndexViewModel : UiStackLayoutViewModel
{
    public DnsAuditIndexViewModel()
    {
        PageTitle = "DNS Audit";
    }

    public bool IsInitialized { get; init; }
    public bool IsConfigured { get; init; }
    public string? ErrorMessage { get; init; }
    public DnsAuditReport? Report { get; init; }
    public DateTime? LastAttemptedAt { get; init; }
    public DateTime? LastSuccessfulAt { get; init; }
}

/// <summary>
/// A normalized, provider-neutral DNS observation. Cloudflare-managed records
/// come from its API so orange-cloud origins remain visible; external-provider
/// observations come from effective public A/AAAA resolution.
/// All record types are retained so policy checks (for example, forbidding
/// wildcard records) cannot be bypassed with a non-address record type.
/// Only A, AAAA, and CNAME records participate in address reconciliation.
/// </summary>
public sealed class DnsAuditRecord
{
    public required string Id { get; init; }
    public required string ZoneName { get; init; }
    public required string Type { get; init; }
    public required string Name { get; init; }
    public required string Content { get; init; }
    public bool Proxied { get; init; }
}

public sealed record DnsAuditInput(
    IReadOnlyCollection<string> ZoneNames,
    int TotalRecordCount,
    IReadOnlyCollection<DnsAuditRecord> Records,
    IReadOnlyCollection<Service> Services,
    IReadOnlyCollection<Server> Servers,
    IReadOnlyDictionary<string, IReadOnlyCollection<string>> ResolvedCnameAddresses,
    IReadOnlyCollection<string>? PubliclyResolvableDomains = null,
    IReadOnlyCollection<string>? PubliclyAuditedDomains = null,
    IReadOnlyDictionary<string, string>? PublicDnsLookupFailures = null,
    IReadOnlyCollection<DomainAlias>? DomainAliases = null,
    IReadOnlyDictionary<string, DomainAliasRedirectResult>? DomainAliasRedirectResults = null);

public sealed record DomainAliasRedirectResult(
    bool IsMatch,
    int? StatusCode,
    string? ActualTargetUrl,
    string Details);
