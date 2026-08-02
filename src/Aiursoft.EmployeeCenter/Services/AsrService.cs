using System.Net.Http.Headers;
using Aiursoft.EmployeeCenter.Configuration;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Services.FileStorage;
using Aiursoft.Scanner.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Aiursoft.EmployeeCenter.Services;

public class AsrResponse
{
    public string? Text { get; set; }
    public string? Error { get; set; }
    public List<AsrApiSegment>? Segments { get; set; }
}

public class AsrApiSegment
{
    public double Start { get; set; }
    public double End { get; set; }
    public string Text { get; set; } = string.Empty;
}

public sealed record AsrTranscriptSegment(long StartMilliseconds, long EndMilliseconds, string Text);

public class AsrService(
    HttpClient httpClient,
    IOptions<AsrSettings> asrSettings,
    EmployeeCenterDbContext dbContext,
    StorageService storageService,
    AsrMediaProcessor mediaProcessor,
    ILogger<AsrService> logger) : ITransientDependency
{
    private readonly AsrSettings _asrSettings = asrSettings.Value;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".wav", ".m4a", ".ogg", ".flac", ".aac", ".webm", ".amr",
        ".mp4", ".mov", ".mkv", ".avi"
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".mkv", ".avi", ".webm"
    };

    private async Task<string?> RecognizeMediaAsync(Audio audio, string filePath)
    {
        if (!HasRequiredSettings())
        {
            logger.LogWarning("ASR settings are not configured.");
            return null;
        }
        if (!File.Exists(filePath))
        {
            logger.LogError("File not found at {FilePath}", filePath);
            return null;
        }

        var extension = Path.GetExtension(filePath);
        if (!AllowedExtensions.Contains(extension))
        {
            logger.LogInformation("File extension {Extension} is not a supported media format. Skipping ASR.", extension);
            return null;
        }

        var policy = await GetTranscriptionPolicyAsync();
        var probe = await mediaProcessor.ProbeAsync(filePath);
        var existingSegments = await dbContext.AudioAsrSegments
            .Where(segment => segment.AudioId == audio.Id)
            .OrderBy(segment => segment.SegmentIndex)
            .ToListAsync();
        if (existingSegments.Count > 0)
        {
            policy = policy with
            {
                SegmentDurationSeconds = existingSegments[0].SegmentDurationSeconds,
                OverlapSeconds = existingSegments[0].OverlapSeconds
            };
        }

        var requiresPreprocessing =
            VideoExtensions.Contains(extension) ||
            probe.Duration.TotalSeconds > policy.SegmentDurationSeconds ||
            new FileInfo(filePath).Length > policy.UploadLimitBytes;
        if (!requiresPreprocessing)
        {
            var response = await RecognizeFileAsync(
                filePath,
                Path.GetFileName(filePath),
                "json",
                BuildTaskId(audio.Id, 0, audio.AsrAttemptCount));
            return response?.Text ?? string.Empty;
        }

        return await RecognizeSegmentedMediaAsync(audio, filePath, probe.Duration, policy, existingSegments);
    }

    private async Task<string?> RecognizeSegmentedMediaAsync(
        Audio audio,
        string filePath,
        TimeSpan mediaDuration,
        AsrTranscriptionPolicy policy,
        IReadOnlyList<AudioAsrSegment> existingSegments)
    {
        var windows = AsrMediaProcessor.CreateSegmentWindows(
            mediaDuration,
            policy.SegmentDurationSeconds,
            policy.OverlapSeconds);
        var completedIndices = existingSegments.Select(segment => segment.SegmentIndex).ToHashSet();
        var missingWindows = windows.Where(window => !completedIndices.Contains(window.Index)).ToList();
        if (missingWindows.Count > 0)
        {
            var completed = await TranscribeMissingSegmentsAsync(audio, filePath, policy, missingWindows, windows.Count == 1);
            if (!completed)
            {
                return null;
            }
        }

        var completedSegments = await dbContext.AudioAsrSegments
            .Where(segment => segment.AudioId == audio.Id)
            .OrderBy(segment => segment.SegmentIndex)
            .ToListAsync();
        if (completedSegments.Count != windows.Count)
        {
            logger.LogError(
                "ASR segment count mismatch for audio {AudioId}. Expected {ExpectedCount}, found {ActualCount}.",
                audio.Id,
                windows.Count,
                completedSegments.Count);
            return null;
        }

        return MergeTranscriptSegments(completedSegments);
    }

    private async Task<bool> TranscribeMissingSegmentsAsync(
        Audio audio,
        string filePath,
        AsrTranscriptionPolicy policy,
        IReadOnlyList<AsrSegmentWindow> missingWindows,
        bool allowTextFallback)
    {
        var temporaryDirectory = Directory.CreateTempSubdirectory("asr-media-");
        try
        {
            var segmentFiles = await mediaProcessor.CreateSegmentFilesAsync(
                filePath,
                missingWindows,
                temporaryDirectory.FullName);
            foreach (var window in missingWindows)
            {
                var segmentPath = segmentFiles[window.Index];
                if (new FileInfo(segmentPath).Length > policy.UploadLimitBytes)
                {
                    logger.LogError(
                        "ASR segment {SegmentIndex} for audio {AudioId} exceeds upload limit {UploadLimitBytes}.",
                        window.Index,
                        audio.Id,
                        policy.UploadLimitBytes);
                    return false;
                }
            }

            foreach (var windowBatch in missingWindows.Chunk(2))
            {
                var transcriptionTasks = windowBatch.Select(window => TranscribeSegmentAsync(
                    audio,
                    window,
                    segmentFiles[window.Index],
                    policy,
                    allowTextFallback));
                var transcriptionResults = await Task.WhenAll(transcriptionTasks);
                var batchSucceeded = true;
                foreach (var result in transcriptionResults)
                {
                    if (result == null)
                    {
                        batchSucceeded = false;
                        continue;
                    }
                    dbContext.AudioAsrSegments.Add(result);
                }
                await dbContext.SaveChangesAsync();
                if (!batchSucceeded)
                {
                    return false;
                }
            }
            return true;
        }
        finally
        {
            temporaryDirectory.Delete(recursive: true);
        }
    }

    private async Task<AudioAsrSegment?> TranscribeSegmentAsync(
        Audio audio,
        AsrSegmentWindow window,
        string segmentPath,
        AsrTranscriptionPolicy policy,
        bool allowTextFallback)
    {
        try
        {
            var response = await RecognizeFileAsync(
                segmentPath,
                Path.GetFileName(segmentPath),
                "verbose_json",
                BuildTaskId(audio.Id, window.Index, audio.AsrAttemptCount));
            if (response == null)
            {
                return null;
            }

            var transcriptSegments = SelectTranscriptSegments(response.Segments, window, allowTextFallback, response.Text);
            if (transcriptSegments == null)
            {
                logger.LogError(
                    "ASR response for audio {AudioId} segment {SegmentIndex} did not contain timestamps.",
                    audio.Id,
                    window.Index);
                return null;
            }

            return new AudioAsrSegment
            {
                AudioId = audio.Id,
                SegmentIndex = window.Index,
                CoreStartMilliseconds = window.CoreStartMilliseconds,
                CoreEndMilliseconds = window.CoreEndMilliseconds,
                InputStartMilliseconds = window.InputStartMilliseconds,
                InputEndMilliseconds = window.InputEndMilliseconds,
                SegmentDurationSeconds = policy.SegmentDurationSeconds,
                OverlapSeconds = policy.OverlapSeconds,
                SegmentsJson = JsonConvert.SerializeObject(transcriptSegments),
                PlainText = JoinSegmentText(transcriptSegments)
            };
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "ASR request failed for audio {AudioId} segment {SegmentIndex}.",
                audio.Id,
                window.Index);
            return null;
        }
    }

    public static IReadOnlyList<AsrTranscriptSegment>? SelectTranscriptSegments(
        IReadOnlyList<AsrApiSegment>? responseSegments,
        AsrSegmentWindow window,
        bool allowTextFallback,
        string? fallbackText)
    {
        if (responseSegments == null)
        {
            if (!allowTextFallback)
            {
                return null;
            }
            return string.IsNullOrWhiteSpace(fallbackText)
                ? []
                : [new AsrTranscriptSegment(window.CoreStartMilliseconds, window.CoreEndMilliseconds, fallbackText.Trim())];
        }

        var selected = new List<AsrTranscriptSegment>();
        foreach (var segment in responseSegments)
        {
            if (!double.IsFinite(segment.Start) || !double.IsFinite(segment.End) || segment.End < segment.Start)
            {
                continue;
            }

            var start = window.InputStartMilliseconds + checked((long)Math.Round(segment.Start * 1000));
            var end = window.InputStartMilliseconds + checked((long)Math.Round(segment.End * 1000));
            var midpoint = start + (end - start) / 2;
            if (midpoint < window.CoreStartMilliseconds || midpoint >= window.CoreEndMilliseconds)
            {
                continue;
            }
            if (!string.IsNullOrWhiteSpace(segment.Text))
            {
                selected.Add(new AsrTranscriptSegment(start, end, segment.Text.Trim()));
            }
        }

        return selected;
    }

    public static string MergeTranscriptSegments(IReadOnlyList<AudioAsrSegment> storedSegments)
    {
        var merged = new List<AsrTranscriptSegment>();
        foreach (var storedSegment in storedSegments.OrderBy(segment => segment.SegmentIndex))
        {
            var segments = JsonConvert.DeserializeObject<List<AsrTranscriptSegment>>(storedSegment.SegmentsJson) ?? [];
            foreach (var segment in segments.OrderBy(item => item.StartMilliseconds))
            {
                var previous = merged.LastOrDefault();
                var isDuplicate = previous != null &&
                                  previous.EndMilliseconds >= segment.StartMilliseconds &&
                                  NormalizeText(previous.Text) == NormalizeText(segment.Text);
                if (!isDuplicate)
                {
                    merged.Add(segment);
                }
            }
        }
        return JoinSegmentText(merged);
    }

    private async Task<AsrResponse?> RecognizeFileAsync(
        string filePath,
        string fileName,
        string responseFormat,
        string taskId)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(_asrSettings.Model), "model");
        form.Add(new StringContent(responseFormat), "response_format");
        if (!string.IsNullOrEmpty(_asrSettings.Level))
        {
            form.Add(new StringContent(_asrSettings.Level), "level");
        }
        if (!string.IsNullOrEmpty(_asrSettings.Language))
        {
            form.Add(new StringContent(_asrSettings.Language), "language");
        }

        await using var fileStream = File.OpenRead(filePath);
        using var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(fileContent, "file", fileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, _asrSettings.Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _asrSettings.BearerToken);
        request.Headers.Add("X-Task-Id", taskId);
        request.Content = form;
        using var response = await httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("ASR API request failed with status {StatusCode}: {Content}", response.StatusCode, content);
            return null;
        }

        var asrResponse = JsonConvert.DeserializeObject<AsrResponse>(content);
        if (!string.IsNullOrEmpty(asrResponse?.Error))
        {
            logger.LogError("ASR API returned error: {Error}", asrResponse.Error);
            return null;
        }
        return asrResponse ?? new AsrResponse();
    }

    private async Task<AsrTranscriptionPolicy> GetTranscriptionPolicyAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _asrSettings.SystemEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _asrSettings.BearerToken);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var response = await httpClient.SendAsync(request, timeout.Token);
        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"ASR system endpoint returned {(int)response.StatusCode}: {content}");
        }

        var system = JsonConvert.DeserializeObject<AsrSystemResponse>(content);
        var policy = system?.TranscriptionPolicy;
        if (system == null || policy == null ||
            system.UploadLimitBytes <= 0 ||
            policy.RecommendedSegmentDurationSeconds <= 0 ||
            policy.SegmentOverlapSeconds < 0 ||
            policy.SegmentOverlapSeconds >= policy.RecommendedSegmentDurationSeconds ||
            policy.TranscriptionTimeoutSeconds <= 0)
        {
            throw new InvalidOperationException("ASR system endpoint returned an invalid transcription policy.");
        }
        if (_asrSettings.TimeoutSeconds <= policy.TranscriptionTimeoutSeconds)
        {
            throw new InvalidOperationException(
                "ASR client timeout must be greater than the server transcription timeout.");
        }

        return new AsrTranscriptionPolicy(
            system.UploadLimitBytes,
            policy.RecommendedSegmentDurationSeconds,
            policy.SegmentOverlapSeconds,
            policy.TranscriptionTimeoutSeconds);
    }

    public async Task ProcessAudioAsrAsync(int audioId)
    {
        if (!_asrSettings.Enabled)
        {
            return;
        }

        var audio = await dbContext.Audios.FindAsync(audioId);
        if (audio == null)
        {
            logger.LogWarning("Audio with ID {AudioId} not found for ASR processing", audioId);
            return;
        }

        audio.AsrAttemptCount++;
        audio.LastAsrAttemptTime = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        try
        {
            var filePath = storageService.GetFilePhysicalPath(audio.FilePath, isVault: true);
            var plainText = await RecognizeMediaAsync(audio, filePath);
            if (plainText == null)
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(plainText))
            {
                await RecordEmptyResultAsync(audio);
                return;
            }

            audio.EmptyResultCount = 0;
            var existing = await dbContext.AudioAsrResults.FirstOrDefaultAsync(result => result.AudioId == audioId);
            if (existing != null)
            {
                dbContext.AudioAsrResults.Remove(existing);
            }
            dbContext.AudioAsrResults.Add(new AudioAsrResult
            {
                AudioId = audioId,
                PlainText = plainText
            });
            await dbContext.SaveChangesAsync();
            logger.LogInformation("Successfully processed ASR for audio {AudioId}", audioId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while processing ASR for audio {AudioId}", audioId);
        }
    }

    public async Task<string?> GetAsrResultByAudioIdAsync(int audioId)
    {
        var result = await dbContext.AudioAsrResults
            .Where(item => item.AudioId == audioId)
            .OrderByDescending(item => item.CreateTime)
            .FirstOrDefaultAsync();
        return result?.PlainText;
    }

    private async Task RecordEmptyResultAsync(Audio audio)
    {
        audio.EmptyResultCount++;
        var segments = await dbContext.AudioAsrSegments
            .Where(segment => segment.AudioId == audio.Id)
            .ToListAsync();
        dbContext.AudioAsrSegments.RemoveRange(segments);

        if (audio.EmptyResultCount >= _asrSettings.AsrMaxEmptyRetryCount)
        {
            dbContext.AudioAsrResults.Add(new AudioAsrResult
            {
                AudioId = audio.Id,
                PlainText = string.Empty
            });
            logger.LogInformation(
                "Audio {AudioId} ASR returned empty results {AttemptCount} times. Marking as permanently empty.",
                audio.Id,
                audio.EmptyResultCount);
        }
        await dbContext.SaveChangesAsync();
    }

    private bool HasRequiredSettings()
    {
        return !string.IsNullOrEmpty(_asrSettings.Endpoint) &&
               !string.IsNullOrEmpty(_asrSettings.SystemEndpoint) &&
               !string.IsNullOrEmpty(_asrSettings.BearerToken);
    }

    private static string BuildTaskId(int audioId, int segmentIndex, int attempt)
    {
        return $"audio-{audioId}-segment-{segmentIndex}-attempt-{attempt}";
    }

    private static string JoinSegmentText(IEnumerable<AsrTranscriptSegment> segments)
    {
        return string.Join(' ', segments.Select(segment => segment.Text).Where(text => !string.IsNullOrWhiteSpace(text)));
    }

    private static string NormalizeText(string text)
    {
        return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private sealed class AsrSystemResponse
    {
        [JsonProperty("upload_limit_bytes")]
        public long UploadLimitBytes { get; set; }

        [JsonProperty("transcription_policy")]
        public AsrSystemTranscriptionPolicy? TranscriptionPolicy { get; set; }
    }

    private sealed class AsrSystemTranscriptionPolicy
    {
        [JsonProperty("recommended_segment_duration_seconds")]
        public int RecommendedSegmentDurationSeconds { get; set; }

        [JsonProperty("segment_overlap_seconds")]
        public int SegmentOverlapSeconds { get; set; }

        [JsonProperty("transcription_timeout_seconds")]
        public int TranscriptionTimeoutSeconds { get; set; }
    }

    private sealed record AsrTranscriptionPolicy(
        long UploadLimitBytes,
        int SegmentDurationSeconds,
        int OverlapSeconds,
        int TranscriptionTimeoutSeconds);
}
