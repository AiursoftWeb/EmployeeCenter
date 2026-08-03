using System.Diagnostics;
using System.Globalization;
using Aiursoft.EmployeeCenter.Configuration;
using Aiursoft.Scanner.Abstractions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Aiursoft.EmployeeCenter.Services;

public sealed record AsrSegmentWindow(
    int Index,
    long CoreStartMilliseconds,
    long CoreEndMilliseconds,
    long InputStartMilliseconds,
    long InputEndMilliseconds);

public sealed record AsrMediaProbe(TimeSpan Duration);

public class AsrMediaProcessor(
    IOptions<AsrSettings> asrSettings,
    ILogger<AsrMediaProcessor> logger) : ITransientDependency
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(30);
    private readonly TimeSpan _segmentProcessingTimeout = asrSettings.Value.GetProcessingTimeout();

    public static IReadOnlyList<AsrSegmentWindow> CreateSegmentWindows(
        TimeSpan mediaDuration,
        int segmentDurationSeconds,
        int overlapSeconds)
    {
        if (mediaDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(mediaDuration));
        }
        if (segmentDurationSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(segmentDurationSeconds));
        }
        if (overlapSeconds < 0 || overlapSeconds >= segmentDurationSeconds)
        {
            throw new ArgumentOutOfRangeException(nameof(overlapSeconds));
        }

        var totalMilliseconds = checked((long)Math.Ceiling(mediaDuration.TotalMilliseconds));
        var segmentMilliseconds = checked((long)segmentDurationSeconds * 1000);
        var overlapMilliseconds = checked((long)overlapSeconds * 1000);
        var segmentCount = checked((int)((totalMilliseconds + segmentMilliseconds - 1) / segmentMilliseconds));
        var windows = new List<AsrSegmentWindow>(segmentCount);

        for (var index = 0; index < segmentCount; index++)
        {
            var coreStart = checked(index * segmentMilliseconds);
            var coreEnd = Math.Min(coreStart + segmentMilliseconds, totalMilliseconds);
            windows.Add(new AsrSegmentWindow(
                Index: index,
                CoreStartMilliseconds: coreStart,
                CoreEndMilliseconds: coreEnd,
                InputStartMilliseconds: Math.Max(0, coreStart - overlapMilliseconds),
                InputEndMilliseconds: Math.Min(totalMilliseconds, coreEnd + overlapMilliseconds)));
        }

        return windows;
    }

    public async Task<AsrMediaProbe> ProbeAsync(string mediaPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "ffprobe",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-v");
        startInfo.ArgumentList.Add("error");
        startInfo.ArgumentList.Add("-show_entries");
        startInfo.ArgumentList.Add("stream=codec_type:format=duration");
        startInfo.ArgumentList.Add("-of");
        startInfo.ArgumentList.Add("json");
        startInfo.ArgumentList.Add(mediaPath);

        var result = await RunProcessAsync(startInfo, ProbeTimeout);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"ffprobe failed: {result.Error}");
        }

        var payload = JsonConvert.DeserializeObject<FfprobePayload>(result.Output);
        if (payload == null || !payload.Streams.Any(stream => stream.CodecType == "audio"))
        {
            throw new InvalidOperationException("Media does not contain a decodable audio stream.");
        }
        if (!double.TryParse(payload.Format?.Duration, NumberStyles.Float, CultureInfo.InvariantCulture, out var durationSeconds) ||
            !double.IsFinite(durationSeconds) ||
            durationSeconds <= 0)
        {
            throw new InvalidOperationException("Media duration is unavailable.");
        }

        return new AsrMediaProbe(TimeSpan.FromSeconds(durationSeconds));
    }

    public async Task<IReadOnlyDictionary<int, string>> CreateSegmentFilesAsync(
        string mediaPath,
        IReadOnlyList<AsrSegmentWindow> windows,
        string outputDirectory)
    {
        if (windows.Count == 0)
        {
            return new Dictionary<int, string>();
        }

        Directory.CreateDirectory(outputDirectory);
        var outputPaths = windows.ToDictionary(
            window => window.Index,
            window => Path.Combine(outputDirectory, $"segment-{window.Index}.flac"));
        foreach (var window in windows)
        {
            await CreateSegmentFileAsync(mediaPath, window, outputPaths[window.Index]);
        }

        return outputPaths;
    }

    private async Task CreateSegmentFileAsync(
        string mediaPath,
        AsrSegmentWindow window,
        string outputPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-nostdin");
        startInfo.ArgumentList.Add("-hide_banner");
        startInfo.ArgumentList.Add("-loglevel");
        startInfo.ArgumentList.Add("error");
        startInfo.ArgumentList.Add("-y");
        startInfo.ArgumentList.Add("-ss");
        startInfo.ArgumentList.Add(FormatMilliseconds(window.InputStartMilliseconds));
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(mediaPath);
        startInfo.ArgumentList.Add("-t");
        startInfo.ArgumentList.Add(FormatMilliseconds(window.InputEndMilliseconds - window.InputStartMilliseconds));
        startInfo.ArgumentList.Add("-map");
        startInfo.ArgumentList.Add("0:a:0");
        startInfo.ArgumentList.Add("-ar");
        startInfo.ArgumentList.Add("16000");
        startInfo.ArgumentList.Add("-ac");
        startInfo.ArgumentList.Add("1");
        startInfo.ArgumentList.Add("-sample_fmt");
        startInfo.ArgumentList.Add("s16");
        startInfo.ArgumentList.Add("-c:a");
        startInfo.ArgumentList.Add("flac");
        startInfo.ArgumentList.Add(outputPath);

        var result = await RunProcessAsync(startInfo, _segmentProcessingTimeout);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"ffmpeg failed: {result.Error}");
        }
        if (!File.Exists(outputPath))
        {
            throw new InvalidOperationException($"ffmpeg did not create expected output {outputPath}.");
        }
    }

    private static string FormatMilliseconds(long milliseconds)
    {
        return (milliseconds / 1000d).ToString("0.###", CultureInfo.InvariantCulture);
    }

    private async Task<ProcessResult> RunProcessAsync(ProcessStartInfo startInfo, TimeSpan timeout)
    {
        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start {startInfo.FileName}.");
        }

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        using var timeoutSource = new CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
        }
        catch (OperationCanceledException ex) when (timeoutSource.IsCancellationRequested)
        {
            logger.LogError(
                ex,
                "{ProcessName} timed out after {TimeoutSeconds} seconds.",
                startInfo.FileName,
                timeout.TotalSeconds);
            await TerminateProcessAsync(process, startInfo.FileName);
            await Task.WhenAll(outputTask, errorTask);
            throw new TimeoutException(
                $"{startInfo.FileName} timed out after {timeout.TotalSeconds} seconds.",
                ex);
        }
        var output = await outputTask;
        var error = await errorTask;
        if (process.ExitCode != 0)
        {
            logger.LogError("{ProcessName} exited with code {ExitCode}: {Error}", startInfo.FileName, process.ExitCode, error);
        }
        return new ProcessResult(process.ExitCode, output, error);
    }

    private async Task TerminateProcessAsync(Process process, string processName)
    {
        if (process.HasExited)
        {
            return;
        }

        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex)
        {
            if (process.HasExited)
            {
                return;
            }
            logger.LogError(ex, "Failed to terminate timed out process {ProcessName}.", processName);
            throw new TimeoutException($"Failed to terminate timed out process {processName}.", ex);
        }
        await process.WaitForExitAsync();
    }

    private sealed record ProcessResult(int ExitCode, string Output, string Error);

    private sealed class FfprobePayload
    {
        public List<FfprobeStream> Streams { get; set; } = [];
        public FfprobeFormat? Format { get; set; }
    }

    private sealed class FfprobeStream
    {
        [JsonProperty("codec_type")]
        public string? CodecType { get; set; }
    }

    private sealed class FfprobeFormat
    {
        public string? Duration { get; set; }
    }
}
