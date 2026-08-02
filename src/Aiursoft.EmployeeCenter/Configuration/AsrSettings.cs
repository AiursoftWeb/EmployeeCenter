namespace Aiursoft.EmployeeCenter.Configuration;

public class AsrSettings
{
    private const string TranscriptionEndpointSuffix = "/audio/transcriptions";

    private const int TimeoutBufferSeconds = 30;

    public bool Enabled { get; init; } = true;
    public string? Endpoint { get; init; }
    public string? SystemEndpoint { get; init; }
    public string? BearerToken { get; init; }
    public string Model { get; init; } = "whisperx";
    public string? Level { get; init; } = "large-v3";
    public string? Language { get; init; }
    public int TimeoutSeconds { get; init; } = 7200;
    public int AsrMaxRetryCount { get; init; } = 30;
    public int AsrMaxEmptyRetryCount { get; init; } = 10;

    public TimeSpan GetProcessingTimeout()
    {
        return TimeSpan.FromSeconds(TimeoutSeconds + TimeoutBufferSeconds);
    }

    public string? ResolveSystemEndpoint()
    {
        if (!string.IsNullOrWhiteSpace(SystemEndpoint))
        {
            return SystemEndpoint;
        }
        if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out var transcriptionEndpoint) ||
            !transcriptionEndpoint.AbsolutePath.EndsWith(
                TranscriptionEndpointSuffix,
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var systemEndpoint = new UriBuilder(transcriptionEndpoint)
        {
            Path = transcriptionEndpoint.AbsolutePath[..^TranscriptionEndpointSuffix.Length] + "/system",
            Query = string.Empty,
            Fragment = string.Empty
        };
        return systemEndpoint.Uri.AbsoluteUri;
    }
}
