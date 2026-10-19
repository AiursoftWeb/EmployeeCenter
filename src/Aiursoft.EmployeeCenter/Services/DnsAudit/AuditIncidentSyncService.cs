using System.Security.Cryptography;
using System.Text;
using Aiursoft.AlertCenter.SDK.Models;
using Aiursoft.EmployeeCenter.Configuration;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.DnsAuditViewModels;
using Newtonsoft.Json.Linq;

namespace Aiursoft.EmployeeCenter.Services.DnsAudit;

public sealed class AuditIncidentSyncService(
    IAuditIncidentClient client, IAlertCenterSettingsProvider settingsProvider, ILogger<AuditIncidentSyncService> logger)
{
    private const string Source = "EmployeeCenter.ServiceAudit";

    public async Task<bool> SynchronizeAsync(long runId, DnsAuditReport report)
    {
        try
        {
            var settings = await settingsProvider.GetAsync();
            if (!settings.Enabled) return true;
            settings.Validate();

            // Finish all reads before mutations: active-only pagination shrinks when incidents close.
            var active = await LoadActiveAsync();
            var owned = active.Select(incident => (Incident: incident, Identity: ReadIdentity(incident.Context)))
                .Where(item => item.Identity != null)
                .GroupBy(item => item.Identity!)
                .ToDictionary(group => group.Key, group => group.Select(item => item.Incident)
                    .OrderBy(item => item.ReceivedAtUtc).ThenBy(item => item.IncidentId).ToList());
            var current = report.Issues.Where(issue => issue.Severity >= DnsAuditSeverity.Warning)
                .GroupBy(Identity.From).ToDictionary(group => group.Key,
                    group => group.OrderByDescending(issue => issue.Severity).First());
            var success = true;
            foreach (var (identity, issue) in current)
            {
                if (owned.ContainsKey(identity)) continue;
                try
                {
                    // The same persisted audit run and issue always reuse the same reporting receipt.
                    var reportId = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(FormattableString.Invariant($"{Source}:{runId}:{identity.Type}:{identity.Domain}:{identity.ServiceId}:{identity.DomainAliasId}")))[..16]);
                    var title = $"{issue.CheckName}: {identity.Domain}";
                    var content = Describe(issue, runId, report.GeneratedAt, settings);
                    await client.CreateAsync(new CreateIncidentRequest
                    {
                        ReportId = reportId, Title = title.Length > 200 ? title[..200] : title,
                        Content = content.Length > 4000 ? content[..4000] : content,
                        Level = issue.Severity switch
                        {
                            DnsAuditSeverity.Critical => "sev2", DnsAuditSeverity.Error => "sev3", _ => "sev4"
                        },
                        OccurredAtUtc = new DateTimeOffset(DateTime.SpecifyKind(report.GeneratedAt, DateTimeKind.Utc)),
                        Context = new JObject
                        {
                            ["source"] = Source, ["checkType"] = identity.Type.ToString(),
                            ["domain"] = identity.Domain, ["serviceId"] = identity.ServiceId,
                            ["domainAliasId"] = identity.DomainAliasId, ["auditRunId"] = runId,
                            ["evidence"] = new JObject { ["details"] = issue.Details, ["severity"] = issue.Severity.ToString() }
                        }
                    });
                }
                catch (Exception ex)
                {
                    success = false;
                    logger.LogError(ex, "Could not report audit issue {Issue} from run {RunId}", identity, runId);
                }
            }
            foreach (var (identity, incidents) in owned)
            {
                var stillPresent = current.ContainsKey(identity);
                if (!stillPresent && !CanResolve(identity, report)) continue;
                foreach (var incident in stillPresent ? incidents.Skip(1) : incidents)
                {
                    try
                    {
                        await client.ResolveAsync(incident.IncidentId, new ResolveIncidentRequest
                        {
                            Version = incident.Version,
                            Reason = stillPresent
                                ? $"Audit run {runId}: duplicate of active incident {incidents[0].IncidentId}. The issue remains active there."
                                : $"Audit run {runId}: this check is now healthy or the registered target no longer applies."
                        });
                    }
                    catch (Exception ex)
                    {
                        // In particular, a version conflict is left for fresh audit evidence and a fresh query.
                        success = false;
                        logger.LogError(ex, "Could not resolve audit incident {IncidentId} in run {RunId}", incident.IncidentId, runId);
                    }
                }
            }
            logger.LogInformation("AlertCenter synchronization for audit run {RunId} completed: {FindingCount} findings, {IncidentCount} managed active incidents, success {Success}",
                runId, current.Count, owned.Sum(item => item.Value.Count), success);
            return success;
        }
        catch (Exception ex)
        {
            // A transport/query failure must never reclassify the saved audit as failed or authorize closures.
            logger.LogError(ex, "AlertCenter synchronization failed for completed audit run {RunId}", runId);
            return false;
        }
    }

    private static string Describe(DnsAuditIssue issue, long runId, DateTime observedAt, AlertCenterSettings settings)
    {
        var nextSteps = issue.Type switch
        {
            DnsAuditIssueType.ServiceUnavailable => "Check the reported HTTP/DNS error, the service process, reverse proxy and origin connectivity. Verify the public endpoint before declaring recovery.",
            DnsAuditIssueType.MissingDns => "Compare the running service registration with its authoritative DNS zone. Restore the intended DNS record, or correct the registration if the service has been retired.",
            DnsAuditIssueType.ProxyStatusMismatch or DnsAuditIssueType.MixedProxyStatus => "Compare Cloudflare proxy settings with the intended service registration. Confirm whether this hostname should be proxied, then correct the mismatched record or registration.",
            DnsAuditIssueType.DanglingCname => "Follow the CNAME chain and fix its missing target or cycle. Check that the final hostname resolves to the intended service.",
            DnsAuditIssueType.AddressFamilyMismatch or DnsAuditIssueType.ServerAssignmentMismatch => "Compare the observed DNS addresses with the registered origin or FRP server. Correct the mismatched address or server assignment.",
            DnsAuditIssueType.MissingServerAssignment or DnsAuditIssueType.MissingFrpsServerAssignment or DnsAuditIssueType.UnknownServer => "Identify the actual hosting and FRP servers, then correct their entries in the service registry.",
            DnsAuditIssueType.OfflineServicePublished => "Confirm whether the service is retired or should still run. Reconcile its lifecycle registration and published DNS accordingly.",
            DnsAuditIssueType.DomainAliasRedirectMismatch => "Check the alias configuration, HTTP response and redirect destination against the registered target.",
            DnsAuditIssueType.DuplicateServiceRegistration => "Review the matching service records and reconcile duplicate registrations with the service owner.",
            DnsAuditIssueType.UnknownDns or DnsAuditIssueType.WildcardDnsRecord => "Identify the DNS record owner and intended service. Reconcile the record with the service registry and DNS policy before changing it.",
            _ => "Review the audit evidence and collection coverage, then compare DNS, the service registry and the actual endpoint. An incomplete lookup is not proof of recovery."
        };
        var header = $"Employee Center found {issue.CheckName} for {issue.Domain}.\n" +
            $"Audit classification: {issue.Severity}. Critical maps to sev2, Error to sev3, and Warning to sev4.\n" +
            $"Observed (UTC): {observedAt:O}; audit run: {runId}.\n\nEvidence: ";
        var footer = $"\n\nNext steps: {nextSteps}\n" +
            "After correcting the issue, run Audit now in Employee Center. A successful audit with sufficient recovery evidence " +
            "automatically resolves this incident when the check is healthy or the registered target no longer applies. " +
            "Closing it manually while the issue persists causes a new incident at the next audit.\n" +
            $"Audit and service details: {settings.PublicBaseUrl.TrimEnd('/')}/ServiceAudit";
        var budget = Math.Max(0, 4000 - header.Length - footer.Length);
        var evidence = issue.Details.Length <= budget ? issue.Details : issue.Details[..Math.Max(0, budget - 3)] + "...";
        return header + evidence + footer;
    }

    private async Task<List<IncidentDetails>> LoadActiveAsync()
    {
        var results = new List<IncidentDetails>();
        int? expectedTotal = null;
        for (var page = 1; ; page++)
        {
            var result = await client.QueryAsync(new QueryIncidentsRequest { ActiveOnly = true, Page = page, PageSize = 100 });
            expectedTotal ??= result.TotalCount;
            if (expectedTotal != result.TotalCount || result.Page != page || result.PageSize != 100)
                throw new InvalidOperationException("The active incident listing changed; synchronization will be retried after the next audit.");
            results.AddRange(result.Items);
            if (results.Count >= expectedTotal)
            {
                if (results.Count != expectedTotal || results.Select(item => item.IncidentId).Distinct().Count() != results.Count)
                    throw new InvalidOperationException("The active incident pages overlap or have inconsistent totals.");
                return results;
            }
            if (result.Items.Count == 0)
                throw new InvalidOperationException("The active incident listing ended before its reported total.");
        }
    }

    private static Identity? ReadIdentity(JObject? context)
    {
        if (context?["source"] is not JValue { Type: JTokenType.String } source || source.Value<string>() != Source ||
            context["checkType"] is not JValue { Type: JTokenType.String } checkType ||
            !Enum.TryParse<DnsAuditIssueType>(checkType.Value<string>(), out var type) || !Enum.IsDefined(type) ||
            context["domain"] is not JValue { Type: JTokenType.String } domain) return null;
        if (context["serviceId"]?.Type is not (JTokenType.Integer or JTokenType.Null) ||
            context["domainAliasId"]?.Type is not (JTokenType.Integer or JTokenType.Null)) return null;
        try
        {
            return new Identity(type, DnsAuditAnalyzer.NormalizeDomain(domain.Value<string>() ?? string.Empty),
                context.Value<int?>("serviceId"), context.Value<int?>("domainAliasId"));
        }
        catch (OverflowException) { return null; }
    }

    private static bool CanResolve(Identity identity, DnsAuditReport report)
    {
        var input = report.RecoveryEvidence;
        if (input == null) return false;
        if (identity.ServiceId.HasValue && !input.Services.Any(service => service.Id == identity.ServiceId &&
                DnsAuditAnalyzer.NormalizeDomain(service.PrimaryDomain) == identity.Domain)) return true;
        if (identity.DomainAliasId.HasValue && !(input.DomainAliases ?? []).Any(alias => alias.Id == identity.DomainAliasId &&
                DnsAuditAnalyzer.NormalizeDomain(alias.Domain) == identity.Domain)) return true;
        if (identity.Type == DnsAuditIssueType.ServiceUnavailable)
            return identity.ServiceId.HasValue && input.ServiceAvailabilityResults?.GetValueOrDefault(identity.ServiceId.Value)?.IsHealthy == true;
        if (identity.Type == DnsAuditIssueType.DomainAliasRedirectMismatch &&
            input.DomainAliases?.Any(alias => alias.Id == identity.DomainAliasId && alias.Type == DomainAliasType.HttpRedirect) == true)
            return input.DomainAliasRedirectResults?.GetValueOrDefault(identity.Domain)?.IsMatch == true;
        if (report.Issues.Any(issue => DnsAuditAnalyzer.NormalizeDomain(issue.Domain) == identity.Domain &&
                issue.Type is DnsAuditIssueType.PublicDnsLookupFailed or DnsAuditIssueType.ServiceOutsideAuditedZone or
                    DnsAuditIssueType.UnverifiableDnsTarget or DnsAuditIssueType.ManagedDnsOutsideRecordApi)) return false;
        return (input.PubliclyAuditedDomains ?? []).Any(domain => DnsAuditAnalyzer.NormalizeDomain(domain) == identity.Domain) ||
               input.ZoneNames.Any(zone => identity.Domain == DnsAuditAnalyzer.NormalizeDomain(zone) ||
                   identity.Domain.EndsWith("." + DnsAuditAnalyzer.NormalizeDomain(zone), StringComparison.Ordinal));
    }

    private sealed record Identity(DnsAuditIssueType Type, string Domain, int? ServiceId, int? DomainAliasId)
    {
        public static Identity From(DnsAuditIssue issue) => new(issue.Type,
            DnsAuditAnalyzer.NormalizeDomain(issue.Domain), issue.ServiceId, issue.DomainAliasId);
    }
}
