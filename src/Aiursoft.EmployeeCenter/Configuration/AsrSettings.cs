namespace Aiursoft.EmployeeCenter.Configuration;

public class AsrSettings
{
    private const int TimeoutBufferSeconds = 30;

    public bool Enabled { get; init; } = true;
    public string? Endpoint { get; init; }
    public string? BearerToken { get; init; }
    public string Model { get; init; } = "whisperx";
    public string? Level { get; init; } = "large-v3";
    public string? Language { get; init; }
    public int SegmentDurationSeconds { get; init; } = 30 * 60;
    public int SegmentOverlapSeconds { get; init; } = 2;
    public int TimeoutSeconds { get; init; } = 7200;
    public int AsrMaxRetryCount { get; init; } = 30;
    public int AsrMaxEmptyRetryCount { get; init; } = 10;

    public TimeSpan GetProcessingTimeout()
    {
        return TimeSpan.FromSeconds(TimeoutSeconds + TimeoutBufferSeconds);
    }

    public void ValidateSegmentation()
    {
        if (SegmentDurationSeconds <= 0)
        {
            throw new InvalidOperationException("ASR segment duration must be greater than zero.");
        }
        if (SegmentOverlapSeconds < 0 || SegmentOverlapSeconds >= SegmentDurationSeconds)
        {
            throw new InvalidOperationException("ASR segment overlap must be non-negative and less than the segment duration.");
        }
    }
}
