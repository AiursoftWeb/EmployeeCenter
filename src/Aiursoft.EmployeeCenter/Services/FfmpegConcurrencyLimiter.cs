using Aiursoft.EmployeeCenter.Configuration;
using Aiursoft.Scanner.Abstractions;
using Microsoft.Extensions.Options;

namespace Aiursoft.EmployeeCenter.Services;

public class FfmpegConcurrencyLimiter : ISingletonDependency, IDisposable
{
    private readonly SemaphoreSlim _semaphore;

    public FfmpegConcurrencyLimiter(IOptions<AsrSettings> settings)
    {
        _semaphore = new SemaphoreSlim(settings.Value.MaxConcurrentFfmpegProcesses);
    }

    public async Task<IDisposable> EnterAsync(CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        return new Releaser(_semaphore);
    }

    public void Dispose()
    {
        _semaphore.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed class Releaser(SemaphoreSlim semaphore) : IDisposable
    {
        public void Dispose() => semaphore.Release();
    }
}
