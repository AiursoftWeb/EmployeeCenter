using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.DnsAuditViewModels;
using Aiursoft.Scanner.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.EmployeeCenter.Services.DnsAudit;

public sealed class ServiceAuditStore(EmployeeCenterDbContext context) : IScopedDependency
{
    public async Task<long> QueueAsync(string? requestedByUserId, CancellationToken cancellationToken = default)
    {
        var recentRunningThreshold = DateTime.UtcNow.AddHours(-6);
        var activeRunId = await context.ServiceAuditRuns
            .AsNoTracking()
            .Where(run => run.Status == ServiceAuditRunStatus.Pending ||
                          (run.Status == ServiceAuditRunStatus.Running &&
                           run.StartedAt >= recentRunningThreshold))
            .OrderBy(run => run.RequestedAt)
            .Select(run => (long?)run.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (activeRunId.HasValue)
        {
            return activeRunId.Value;
        }

        var run = new ServiceAuditRun
        {
            Status = ServiceAuditRunStatus.Pending,
            RequestedAt = DateTime.UtcNow,
            RequestedByUserId = requestedByUserId
        };
        context.ServiceAuditRuns.Add(run);
        await context.SaveChangesAsync(cancellationToken);
        return run.Id;
    }

    public async Task<long> BeginRunAsync(CancellationToken cancellationToken = default)
    {
        var run = await context.ServiceAuditRuns
            .Where(item => item.Status == ServiceAuditRunStatus.Pending)
            .OrderBy(item => item.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (run == null)
        {
            run = new ServiceAuditRun
            {
                Status = ServiceAuditRunStatus.Running,
                RequestedAt = DateTime.UtcNow,
                StartedAt = DateTime.UtcNow
            };
            context.ServiceAuditRuns.Add(run);
        }
        else
        {
            run.Status = ServiceAuditRunStatus.Running;
            run.StartedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken);
        return run.Id;
    }

    public async Task CompleteSuccessAsync(
        long runId,
        DnsAuditReport report,
        CancellationToken cancellationToken = default)
    {
        var run = await context.ServiceAuditRuns.FindAsync([runId], cancellationToken)
                  ?? throw new InvalidOperationException($"Service audit run {runId} was not found.");
        var completedAt = report.GeneratedAt;
        run.Status = ServiceAuditRunStatus.Succeeded;
        run.CompletedAt = completedAt;
        run.ErrorMessage = null;
        run.ZoneCount = report.ZoneCount;
        run.RecordCount = report.RecordCount;
        run.AuditedHostnameCount = report.AuditedHostnameCount;
        run.AvailabilityCheckedCount = report.AvailabilityCheckedCount;
        run.AvailabilityHealthyCount = report.AvailabilityHealthyCount;
        run.CriticalCount = report.CriticalCount;
        run.ErrorCount = report.ErrorCount;
        run.WarningCount = report.WarningCount;
        run.InfoCount = report.InfoCount;

        context.ServiceAuditIssues.AddRange(report.Issues.Select(issue => new ServiceAuditIssue
        {
            ServiceAuditRunId = run.Id,
            ServiceId = issue.ServiceId,
            DomainAliasId = issue.DomainAliasId,
            Type = issue.Type.ToString(),
            Severity = issue.Severity.ToString(),
            Domain = issue.Domain,
            Details = issue.Details,
            ObservedAt = completedAt
        }));
        context.ServiceAuditObservations.AddRange(report.Observations.Select(observation =>
            new ServiceAuditObservation
            {
                ServiceAuditRunId = run.Id,
                ServiceId = observation.ServiceId,
                Domain = observation.Domain,
                Health = observation.Health,
                StatusCode = observation.StatusCode,
                Details = observation.Details,
                ObservedAt = observation.ObservedAt
            }));
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task CompleteNotConfiguredAsync(long runId, CancellationToken cancellationToken = default) =>
        CompleteWithoutReportAsync(runId, ServiceAuditRunStatus.NotConfigured, null, cancellationToken);

    public Task CompleteFailureAsync(
        long runId,
        string errorMessage,
        CancellationToken cancellationToken = default) =>
        CompleteWithoutReportAsync(runId, ServiceAuditRunStatus.Failed, errorMessage, cancellationToken);

    public async Task<DnsAuditSnapshot> LoadSnapshotAsync(
        DnsAuditSnapshot? fallback = null,
        CancellationToken cancellationToken = default)
    {
        var latest = await context.ServiceAuditRuns
            .AsNoTracking()
            .OrderByDescending(run => run.RequestedAt)
            .ThenByDescending(run => run.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (latest == null)
        {
            return fallback ?? new DnsAuditSnapshot();
        }

        var latestSuccess = await context.ServiceAuditRuns
            .AsNoTracking()
            .Include(run => run.Issues)
            .Include(run => run.Observations)
            .Where(run => run.Status == ServiceAuditRunStatus.Succeeded)
            .OrderByDescending(run => run.CompletedAt)
            .ThenByDescending(run => run.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return new DnsAuditSnapshot
        {
            IsInitialized = true,
            IsConfigured = latest.Status != ServiceAuditRunStatus.NotConfigured,
            ErrorMessage = latest.Status == ServiceAuditRunStatus.Failed ? latest.ErrorMessage : null,
            Report = latestSuccess == null ? null : MapReport(latestSuccess),
            LastAttemptedAt = latest.StartedAt ?? latest.RequestedAt,
            LastSuccessfulAt = latestSuccess?.CompletedAt
        };
    }

    public Task<List<ServiceAuditRun>> LoadHistoryAsync(
        int count = 20,
        CancellationToken cancellationToken = default) => context.ServiceAuditRuns
        .AsNoTracking()
        .OrderByDescending(run => run.RequestedAt)
        .ThenByDescending(run => run.Id)
        .Take(Math.Clamp(count, 1, 100))
        .ToListAsync(cancellationToken);

    private async Task CompleteWithoutReportAsync(
        long runId,
        ServiceAuditRunStatus status,
        string? errorMessage,
        CancellationToken cancellationToken)
    {
        var run = await context.ServiceAuditRuns.FindAsync([runId], cancellationToken)
                  ?? throw new InvalidOperationException($"Service audit run {runId} was not found.");
        run.Status = status;
        run.ErrorMessage = errorMessage;
        run.CompletedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }

    private static DnsAuditReport MapReport(ServiceAuditRun run) => new()
    {
        GeneratedAt = run.CompletedAt ?? run.RequestedAt,
        ZoneCount = run.ZoneCount,
        RecordCount = run.RecordCount,
        AuditedHostnameCount = run.AuditedHostnameCount,
        AvailabilityCheckedCount = run.AvailabilityCheckedCount,
        AvailabilityHealthyCount = run.AvailabilityHealthyCount,
        Issues = run.Issues.Select(issue => new DnsAuditIssue
        {
            Type = Enum.TryParse<DnsAuditIssueType>(issue.Type, out var type) ? type : DnsAuditIssueType.UnknownDns,
            Severity = Enum.TryParse<DnsAuditSeverity>(issue.Severity, out var severity)
                ? severity
                : DnsAuditSeverity.Warning,
            Domain = issue.Domain,
            Details = issue.Details,
            ServiceId = issue.ServiceId,
            DomainAliasId = issue.DomainAliasId
        }).ToList(),
        Observations = run.Observations.Select(observation => new ServiceAuditObservationResult(
            observation.ServiceId ?? 0,
            observation.Domain,
            observation.Health,
            observation.StatusCode,
            observation.Details ?? string.Empty,
            observation.ObservedAt)).ToList()
    };
}
