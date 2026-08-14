using Aiursoft.Scanner.Abstractions;

namespace Aiursoft.EmployeeCenter.Services;

public class AsrProcessingCancellationRegistry : ISingletonDependency, IDisposable
{
    private readonly Lock _lock = new();
    private readonly Dictionary<int, CancellationTokenSource> _sources = [];

    public Registration Register(int audioId)
    {
        var source = new CancellationTokenSource();
        lock (_lock)
        {
            if (_sources.TryGetValue(audioId, out var existing))
            {
                existing.Cancel();
            }
            _sources[audioId] = source;
        }
        return new Registration(this, audioId, source);
    }

    public void Cancel(int audioId)
    {
        lock (_lock)
        {
            if (_sources.TryGetValue(audioId, out var source))
            {
                source.Cancel();
            }
        }
    }

    public void Dispose()
    {
        List<CancellationTokenSource> sources;
        lock (_lock)
        {
            sources = [.. _sources.Values];
            _sources.Clear();
        }
        foreach (var source in sources)
        {
            source.Cancel();
            source.Dispose();
        }
        GC.SuppressFinalize(this);
    }

    private void Unregister(int audioId, CancellationTokenSource source)
    {
        lock (_lock)
        {
            if (_sources.TryGetValue(audioId, out var current) && ReferenceEquals(current, source))
            {
                _sources.Remove(audioId);
            }
        }
        source.Dispose();
    }

    public sealed class Registration(
        AsrProcessingCancellationRegistry registry,
        int audioId,
        CancellationTokenSource source) : IDisposable
    {
        public CancellationToken CancellationToken => source.Token;

        public void Cancel() => source.Cancel();

        public void Dispose() => registry.Unregister(audioId, source);
    }
}
