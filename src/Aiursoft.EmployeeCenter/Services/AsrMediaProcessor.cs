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

public sealed record AsrMediaProbe(TimeSpan Duration, bool HasVideoStream = false);

public class AsrMediaProcessor(
    IOptions<AsrSettings> asrSettings,
    ILogger<AsrMediaProcessor> logger,
    FfmpegConcurrencyLimiter? concurrencyLimiter = null) : ITransientDependency
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(30);
    private readonly bool _preferOriginalSegmentCodec = asrSettings.Value.PreferOriginalSegmentCodec;
    private readonly TimeSpan _segmentProcessingTimeout = asrSettings.Value.GetProcessingTimeout();
    private readonly TimeSpan _mediaProcessingTimeout =
        TimeSpan.FromSeconds(asrSettings.Value.MediaProcessingTimeoutSeconds);

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

    public virtual async Task<AsrMediaProbe> ProbeAsync(
        string mediaPath,
        CancellationToken cancellationToken = default)
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

        var result = await RunProcessAsync(startInfo, ProbeTimeout, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"ffprobe failed: {result.Error}");
        }

        var payload = JsonConvert.DeserializeObject<FfprobePayload>(result.Output);
        if (payload == null || !payload.Streams.Any(stream => stream.CodecType == "audio"))
        {
            throw new InvalidOperationException("Media does not contain a decodable audio stream.");
        }
        var duration = ParseMetadataDuration(payload.Format?.Duration);
        if (duration == null)
        {
            logger.LogWarning(
                "Media duration metadata is unavailable for {MediaPath}. Falling back to decoded duration.",
                mediaPath);
            duration = await ProbeDecodedDurationAsync(mediaPath, cancellationToken);
        }
        if (duration == null)
        {
            throw new InvalidOperationException("Media duration is unavailable.");
        }

        return new AsrMediaProbe(
            duration.Value,
            payload.Streams.Any(stream => stream.CodecType == "video"));
    }

    public static TimeSpan? ParseDecodedDuration(string progressOutput)
    {
        long maxMicroseconds = 0;
        foreach (var line in progressOutput.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!line.StartsWith("out_time_us=", StringComparison.Ordinal))
            {
                continue;
            }
            if (long.TryParse(
                    line.AsSpan("out_time_us=".Length),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var microseconds) &&
                microseconds > maxMicroseconds)
            {
                maxMicroseconds = microseconds;
            }
        }

        return maxMicroseconds > 0 ? TimeSpan.FromMicroseconds(maxMicroseconds) : null;
    }

    public virtual async Task<IReadOnlyDictionary<int, string>> CreateSegmentFilesAsync(
        string mediaPath,
        IReadOnlyList<AsrSegmentWindow> windows,
        string outputDirectory,
        long uploadLimitBytes = long.MaxValue,
        CancellationToken cancellationToken = default)
    {
        if (windows.Count == 0)
        {
            return new Dictionary<int, string>();
        }

        Directory.CreateDirectory(outputDirectory);
        var outputPaths = new Dictionary<int, string>();
        foreach (var window in windows)
        {
            outputPaths[window.Index] = await CreateSegmentFileAsync(
                mediaPath,
                window,
                outputDirectory,
                uploadLimitBytes,
                cancellationToken);
        }

        return outputPaths;
    }

    public async Task<string> ExtractAudioTrackAsync(
        string mediaPath,
        string outputDirectory,
        string outputFileNamePrefix,
        CancellationToken cancellationToken = default)
    {
        using var processingSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        processingSource.CancelAfter(_mediaProcessingTimeout);
        Directory.CreateDirectory(outputDirectory);
        if (_preferOriginalSegmentCodec)
        {
            var copiedPath = Path.Combine(outputDirectory, $"{outputFileNamePrefix}.mka");
            if (await TryCopyAudioTrackAsync(mediaPath, copiedPath, processingSource.Token))
            {
                return copiedPath;
            }
        }

        var transcodedPath = Path.Combine(outputDirectory, $"{outputFileNamePrefix}.flac");
        await TranscodeAudioTrackAsync(mediaPath, transcodedPath, processingSource.Token);
        return transcodedPath;
    }

    private async Task<bool> TryCopyAudioTrackAsync(
        string mediaPath,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        AddCommonAudioOutputArguments(startInfo, mediaPath);
        startInfo.ArgumentList.Add("-c:a");
        startInfo.ArgumentList.Add("copy");
        startInfo.ArgumentList.Add(outputPath);

        var succeeded = false;
        try
        {
            var result = await RunProcessAsync(startInfo, _mediaProcessingTimeout, cancellationToken);
            if (result.ExitCode == 0 && File.Exists(outputPath))
            {
                succeeded = true;
                return true;
            }

            logger.LogWarning(
                "ffmpeg stream-copy audio extraction failed for {MediaPath}. Falling back to FLAC transcoding. Error: {Error}",
                mediaPath,
                result.Error);
            return false;
        }
        finally
        {
            if (!succeeded)
            {
                DeleteFileIfExists(outputPath);
            }
        }
    }

    private async Task TranscodeAudioTrackAsync(
        string mediaPath,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        AddCommonAudioOutputArguments(startInfo, mediaPath);
        AddFlacTranscodeArguments(startInfo);
        startInfo.ArgumentList.Add(outputPath);

        var succeeded = false;
        try
        {
            var result = await RunProcessAsync(startInfo, _mediaProcessingTimeout, cancellationToken);
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException($"ffmpeg failed: {result.Error}");
            }
            if (!File.Exists(outputPath))
            {
                throw new InvalidOperationException($"ffmpeg did not create expected output {outputPath}.");
            }
            succeeded = true;
        }
        finally
        {
            if (!succeeded)
            {
                DeleteFileIfExists(outputPath);
            }
        }
    }

    private async Task<string> CreateSegmentFileAsync(
        string mediaPath,
        AsrSegmentWindow window,
        string outputDirectory,
        long uploadLimitBytes,
        CancellationToken cancellationToken)
    {
        if (_preferOriginalSegmentCodec)
        {
            var copiedPath = Path.Combine(outputDirectory, $"segment-{window.Index}.mka");
            if (await TryCreateCopiedAudioSegmentFileAsync(mediaPath, window, copiedPath, cancellationToken))
            {
                if (new FileInfo(copiedPath).Length <= uploadLimitBytes)
                {
                    return copiedPath;
                }
                logger.LogInformation(
                    "Stream-copied segment {SegmentIndex} exceeds upload limit. Falling back to FLAC transcoding.",
                    window.Index);
                DeleteFileIfExists(copiedPath);
            }
        }

        var transcodedPath = Path.Combine(outputDirectory, $"segment-{window.Index}.flac");
        await CreateTranscodedSegmentFileAsync(mediaPath, window, transcodedPath, cancellationToken);
        if (new FileInfo(transcodedPath).Length > uploadLimitBytes)
        {
            DeleteFileIfExists(transcodedPath);
            throw new AsrMediaUploadLimitException(
                $"Transcoded ASR segment {window.Index} exceeds upload limit {uploadLimitBytes} bytes.");
        }
        return transcodedPath;
    }

    private async Task<bool> TryCreateCopiedAudioSegmentFileAsync(
        string mediaPath,
        AsrSegmentWindow window,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        AddCommonSegmentArguments(startInfo, mediaPath, window);
        startInfo.ArgumentList.Add("-vn");
        startInfo.ArgumentList.Add("-c:a");
        startInfo.ArgumentList.Add("copy");
        startInfo.ArgumentList.Add("-avoid_negative_ts");
        startInfo.ArgumentList.Add("make_zero");
        startInfo.ArgumentList.Add(outputPath);

        var result = await RunProcessAsync(startInfo, _segmentProcessingTimeout, cancellationToken);
        if (result.ExitCode == 0 && File.Exists(outputPath))
        {
            return true;
        }

        logger.LogWarning(
            "ffmpeg stream-copy segment failed for {MediaPath}. Falling back to FLAC transcoding. Error: {Error}",
            mediaPath,
            result.Error);
        DeleteFileIfExists(outputPath);
        return false;
    }

    private async Task CreateTranscodedSegmentFileAsync(
        string mediaPath,
        AsrSegmentWindow window,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        AddCommonSegmentArguments(startInfo, mediaPath, window);
        AddFlacTranscodeArguments(startInfo);
        startInfo.ArgumentList.Add(outputPath);

        var result = await RunProcessAsync(startInfo, _segmentProcessingTimeout, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"ffmpeg failed: {result.Error}");
        }
        if (!File.Exists(outputPath))
        {
            throw new InvalidOperationException($"ffmpeg did not create expected output {outputPath}.");
        }
    }

    private static void AddCommonSegmentArguments(
        ProcessStartInfo startInfo,
        string mediaPath,
        AsrSegmentWindow window)
    {
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
    }

    private static void AddCommonAudioOutputArguments(ProcessStartInfo startInfo, string mediaPath)
    {
        startInfo.ArgumentList.Add("-nostdin");
        startInfo.ArgumentList.Add("-hide_banner");
        startInfo.ArgumentList.Add("-loglevel");
        startInfo.ArgumentList.Add("error");
        startInfo.ArgumentList.Add("-y");
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(mediaPath);
        startInfo.ArgumentList.Add("-map");
        startInfo.ArgumentList.Add("0:a:0");
        startInfo.ArgumentList.Add("-vn");
    }

    private static void AddFlacTranscodeArguments(ProcessStartInfo startInfo)
    {
        startInfo.ArgumentList.Add("-ar");
        startInfo.ArgumentList.Add("16000");
        startInfo.ArgumentList.Add("-ac");
        startInfo.ArgumentList.Add("1");
        startInfo.ArgumentList.Add("-sample_fmt");
        startInfo.ArgumentList.Add("s16");
        startInfo.ArgumentList.Add("-c:a");
        startInfo.ArgumentList.Add("flac");
    }

    private void DeleteFileIfExists(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Failed to clean up incomplete ffmpeg output {OutputPath}.", path);
        }
    }

    private static string FormatMilliseconds(long milliseconds)
    {
        return (milliseconds / 1000d).ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static TimeSpan? ParseMetadataDuration(string? rawDuration)
    {
        if (!double.TryParse(rawDuration, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) ||
            !double.IsFinite(seconds) ||
            seconds <= 0)
        {
            return null;
        }
        return TimeSpan.FromSeconds(seconds);
    }

    private async Task<TimeSpan?> ProbeDecodedDurationAsync(
        string mediaPath,
        CancellationToken cancellationToken)
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
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(mediaPath);
        startInfo.ArgumentList.Add("-map");
        startInfo.ArgumentList.Add("0:a:0");
        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add("null");
        startInfo.ArgumentList.Add("-");
        startInfo.ArgumentList.Add("-progress");
        startInfo.ArgumentList.Add("pipe:1");

        var result = await RunProcessAsync(startInfo, _mediaProcessingTimeout, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"ffmpeg duration probe failed: {result.Error}");
        }
        return ParseDecodedDuration(result.Output);
    }

    protected virtual async Task<ProcessResult> RunProcessAsync(
        ProcessStartInfo startInfo,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var limiterLease = concurrencyLimiter == null
            ? null
            : await concurrencyLimiter.EnterAsync(cancellationToken);
        using var process = new Process();
        process.StartInfo = startInfo;
        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start {startInfo.FileName}.");
        }

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            timeoutSource.Token,
            cancellationToken);
        try
        {
            await process.WaitForExitAsync(linkedSource.Token);
        }
        catch (OperationCanceledException ex)
        {
            await TerminateProcessAsync(process, startInfo.FileName);
            await Task.WhenAll(outputTask, errorTask);
            if (!timeoutSource.IsCancellationRequested)
            {
                throw;
            }
            logger.LogError(
                ex,
                "{ProcessName} timed out after {TimeoutSeconds} seconds.",
                startInfo.FileName,
                timeout.TotalSeconds);
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

    protected sealed record ProcessResult(int ExitCode, string Output, string Error);

    private sealed class FfprobePayload
    {
        [JsonProperty("streams")]
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

public class AsrMediaUploadLimitException(string message) : InvalidOperationException(message);
