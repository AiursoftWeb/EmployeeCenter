using Aiursoft.EmployeeCenter.Models.DnsAuditViewModels;

namespace Aiursoft.EmployeeCenter.Services.DnsAudit;

public static class ServiceAvailabilityRetryPolicy
{
    public const int MaxAttempts = 3;
    // Each family gets its own budget so both histories fit the persisted 2,000-character evidence field.
    public const int MaxDetailsLength = 940;

    public static async Task<ServiceAvailabilityResult> ExecuteAsync(
        Func<CancellationToken, Task<ServiceAvailabilityResult>> probe,
        Func<int, CancellationToken, Task> delayAfterFailure,
        CancellationToken cancellationToken = default)
    {
        ServiceAvailabilityResult? lastResult = null;
        var history = new List<string>();

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lastResult = await probe(cancellationToken);
            history.Add($"Attempt {attempt}: {ServiceAvailabilityDiagnostics.Limit(lastResult.Details, 270)}");

            if (lastResult.IsHealthy)
            {
                return attempt == 1
                    ? lastResult with { Details = ServiceAvailabilityDiagnostics.Limit(lastResult.Details, MaxDetailsLength) }
                    : lastResult with
                    {
                        Details = $"Succeeded on attempt {attempt} of {MaxAttempts}.\n{string.Join("\n", history)}"
                    };
            }

            if (attempt < MaxAttempts)
            {
                await delayAfterFailure(attempt, cancellationToken);
            }
        }

        return lastResult! with
        {
            Details = $"All {MaxAttempts} availability attempts failed.\n{string.Join("\n", history)}"
        };
    }
}
