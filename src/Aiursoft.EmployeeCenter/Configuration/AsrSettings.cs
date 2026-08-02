namespace Aiursoft.EmployeeCenter.Configuration;

public class AsrSettings
{
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
}
