using Aiursoft.EmployeeCenter.Models.DnsAuditViewModels;

namespace Aiursoft.EmployeeCenter.Services.DnsAudit;

public static class ServiceAvailabilityRetryPolicy
{
    public const int MaxAttempts = 3;

    public static async Task<ServiceAvailabilityResult> ExecuteAsync(
        Func<CancellationToken, Task<ServiceAvailabilityResult>> probe,
        Func<int, CancellationToken, Task> delayAfterFailure,
        CancellationToken cancellationToken = default)
    {
        ServiceAvailabilityResult? lastResult = null;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lastResult = await probe(cancellationToken);

            if (lastResult.IsHealthy)
            {
                return attempt == 1
                    ? lastResult
                    : lastResult with
                    {
                        Details = $"{lastResult.Details} Succeeded on attempt {attempt} of {MaxAttempts}."
                    };
            }

            if (attempt < MaxAttempts)
            {
                await delayAfterFailure(attempt, cancellationToken);
            }
        }

        return lastResult! with
        {
            Details = $"All {MaxAttempts} availability attempts failed. Last attempt: {lastResult.Details}"
        };
    }
}
