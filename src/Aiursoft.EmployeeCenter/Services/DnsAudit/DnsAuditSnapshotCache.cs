using Aiursoft.EmployeeCenter.Models.DnsAuditViewModels;

namespace Aiursoft.EmployeeCenter.Services.DnsAudit;

public sealed record DnsAuditSnapshot
{
    public bool IsInitialized { get; init; }
    public bool IsConfigured { get; init; }
    public DnsAuditReport? Report { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime? LastAttemptedAt { get; init; }
    public DateTime? LastSuccessfulAt { get; init; }
}

public sealed class DnsAuditSnapshotCache
{
    private readonly Lock _lock = new();
    private DnsAuditSnapshot _current = new();
    private int _refreshInProgress;

    public DnsAuditSnapshot Current
    {
        get
        {
            lock (_lock)
            {
                return _current;
            }
        }
    }

    public bool TryBeginRefresh()
    {
        return Interlocked.CompareExchange(ref _refreshInProgress, 1, 0) == 0;
    }

    public void EndRefresh()
    {
        Volatile.Write(ref _refreshInProgress, 0);
    }

    public void SetNotConfigured(DateTime attemptedAt)
    {
        Set(new DnsAuditSnapshot
        {
            IsInitialized = true,
            IsConfigured = false,
            LastAttemptedAt = attemptedAt
        });
    }

    public void SetSuccess(DnsAuditReport report, DateTime attemptedAt)
    {
        Set(new DnsAuditSnapshot
        {
            IsInitialized = true,
            IsConfigured = true,
            Report = report,
            LastAttemptedAt = attemptedAt,
            LastSuccessfulAt = report.GeneratedAt
        });
    }

    public void SetFailure(string errorMessage, DateTime attemptedAt)
    {
        lock (_lock)
        {
            _current = _current with
            {
                IsInitialized = true,
                IsConfigured = true,
                ErrorMessage = errorMessage,
                LastAttemptedAt = attemptedAt
            };
        }
    }

    private void Set(DnsAuditSnapshot snapshot)
    {
        lock (_lock)
        {
            _current = snapshot;
        }
    }
}
