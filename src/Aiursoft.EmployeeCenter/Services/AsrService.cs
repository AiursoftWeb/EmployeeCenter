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

    [JsonProperty("segments")]
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
    private const long AsrUploadLimitBytes = 1L << 30;
    private const string TranscriptionEndpointSuffix = "/audio/transcriptions";
    private static readonly TimeSpan CancelRequestTimeout = TimeSpan.FromSeconds(30);
    private readonly AsrSettings _asrSettings = asrSettings.Value;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".wav", ".m4a", ".ogg", ".flac", ".aac", ".webm", ".amr", ".mka",
        ".mp4", ".mov", ".mkv", ".avi"
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".mkv", ".avi", ".webm"
    };

    private async Task<string?> RecognizeMediaAsync(Audio audio, string filePath, string processingToken)
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
        if (!IsSupportedMediaExtension(extension))
        {
            logger.LogInformation("File extension {Extension} is not a supported media format. Skipping ASR.", extension);
            return null;
        }

        _asrSettings.ValidateSegmentation();
        var policy = new AsrTranscriptionPolicy(
            AsrUploadLimitBytes,
            _asrSettings.SegmentDurationSeconds,
            _asrSettings.SegmentOverlapSeconds);
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
            var response = await RecognizeTrackedFileAsync(
                audio,
                processingToken,
                filePath,
                Path.GetFileName(filePath),
                "json",
                0);
            return GetRecognizedText(response);
        }

        return await RecognizeSegmentedMediaAsync(
            audio,
            filePath,
            probe.Duration,
            policy,
            existingSegments,
            processingToken);
    }

    private async Task<string?> RecognizeSegmentedMediaAsync(
        Audio audio,
        string filePath,
        TimeSpan mediaDuration,
        AsrTranscriptionPolicy policy,
        IReadOnlyList<AudioAsrSegment> existingSegments,
        string processingToken)
    {
        var windows = AsrMediaProcessor.CreateSegmentWindows(
            mediaDuration,
            policy.SegmentDurationSeconds,
            policy.OverlapSeconds);
        var completedIndices = existingSegments.Select(segment => segment.SegmentIndex).ToHashSet();
        var missingWindows = windows.Where(window => !completedIndices.Contains(window.Index)).ToList();
        if (missingWindows.Count > 0)
        {
            var completed = await TranscribeMissingSegmentsAsync(
                audio,
                filePath,
                policy,
                missingWindows,
                windows.Count == 1,
                processingToken);
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
        bool allowTextFallback,
        string processingToken)
    {
        var temporaryDirectory = Directory.CreateTempSubdirectory("asr-media-");
        try
        {
            foreach (var window in missingWindows)
            {
                if (!await OwnsProcessingAsync(audio.Id, processingToken))
                {
                    logger.LogInformation(
                        "Stopped stale ASR processing for audio {AudioId} before segment {SegmentIndex}.",
                        audio.Id,
                        window.Index);
                    return false;
                }
                var batchCompleted = await TranscribeSegmentBatchAsync(
                    audio,
                    filePath,
                    policy,
                    [window],
                    allowTextFallback,
                    processingToken,
                    temporaryDirectory.FullName);
                if (!batchCompleted)
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

    private async Task<bool> TranscribeSegmentBatchAsync(
        Audio audio,
        string filePath,
        AsrTranscriptionPolicy policy,
        IReadOnlyList<AsrSegmentWindow> windows,
        bool allowTextFallback,
        string processingToken,
        string temporaryDirectory)
    {
        var segmentFiles = await mediaProcessor.CreateSegmentFilesAsync(
            filePath,
            windows,
            temporaryDirectory,
            policy.UploadLimitBytes);
        try
        {
            if (!AreSegmentFilesWithinUploadLimit(audio.Id, windows, segmentFiles, policy.UploadLimitBytes))
            {
                return false;
            }

            var window = windows[0];
            var transcriptionResult = await TranscribeSegmentAsync(
                audio,
                window,
                segmentFiles[window.Index],
                policy,
                allowTextFallback,
                processingToken);
            if (transcriptionResult != null)
            {
                dbContext.AudioAsrSegments.Add(transcriptionResult);
            }
            MarkProcessingTokenForConcurrencyCheck(audio, processingToken);
            await dbContext.SaveChangesAsync();
            return transcriptionResult != null;
        }
        finally
        {
            DeleteSegmentFiles(segmentFiles.Values);
        }
    }

    private bool AreSegmentFilesWithinUploadLimit(
        int audioId,
        IReadOnlyList<AsrSegmentWindow> windows,
        IReadOnlyDictionary<int, string> segmentFiles,
        long uploadLimitBytes)
    {
        foreach (var window in windows)
        {
            if (new FileInfo(segmentFiles[window.Index]).Length <= uploadLimitBytes)
            {
                continue;
            }
            logger.LogError(
                "ASR segment {SegmentIndex} for audio {AudioId} exceeds upload limit {UploadLimitBytes}.",
                window.Index,
                audioId,
                uploadLimitBytes);
            return false;
        }
        return true;
    }

    private static void DeleteSegmentFiles(IEnumerable<string> segmentFiles)
    {
        foreach (var segmentFile in segmentFiles)
        {
            File.Delete(segmentFile);
        }
    }

    private async Task<AudioAsrSegment?> TranscribeSegmentAsync(
        Audio audio,
        AsrSegmentWindow window,
        string segmentPath,
        AsrTranscriptionPolicy policy,
        bool allowTextFallback,
        string processingToken)
    {
        try
        {
            var response = await RecognizeTrackedFileAsync(
                audio,
                processingToken,
                segmentPath,
                Path.GetFileName(segmentPath),
                "verbose_json",
                window.Index);
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

    private async Task<AsrResponse?> RecognizeTrackedFileAsync(
        Audio audio,
        string processingToken,
        string filePath,
        string fileName,
        string responseFormat,
        int segmentIndex)
    {
        var taskId = BuildTaskId(audio.Id, segmentIndex, audio.AsrAttemptCount);
        await SetActiveTaskAsync(audio, processingToken, taskId);
        try
        {
            return await RecognizeFileAsync(filePath, fileName, responseFormat, taskId);
        }
        finally
        {
            await ClearActiveTaskAsync(audio, processingToken, taskId);
        }
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
        if (audio.MediaStatus != AudioMediaStatus.Ready || audio.AsrTerminalError != null)
        {
            logger.LogInformation("Audio {AudioId} is not ready for ASR processing.", audioId);
            return;
        }

        var processingToken = Guid.NewGuid().ToString("N");
        try
        {
            await CancelActiveTaskAsync(audio);
            audio.AsrAttemptCount++;
            audio.LastAsrAttemptTime = DateTime.UtcNow;
            audio.AsrProcessingToken = processingToken;
            audio.AsrActiveTaskId = null;
            await dbContext.SaveChangesAsync();

            var filePath = storageService.GetVaultSubfolderFilePhysicalPath(audio.FilePath, "audio");
            var plainText = await RecognizeMediaAsync(audio, filePath, processingToken);
            if (plainText == null)
            {
                if (!await OwnsProcessingAsync(audioId, processingToken))
                {
                    dbContext.ChangeTracker.Clear();
                    logger.LogInformation(
                        "Stopped stale ASR processing for audio {AudioId} because a newer task took ownership.",
                        audioId);
                    return;
                }
                throw new InvalidOperationException($"ASR processing failed for audio {audioId}.");
            }
            if (string.IsNullOrWhiteSpace(plainText))
            {
                await RecordEmptyResultAsync(audio, processingToken);
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
            MarkProcessingTokenForConcurrencyCheck(audio, processingToken);
            await dbContext.SaveChangesAsync();
            logger.LogInformation("Successfully processed ASR for audio {AudioId}", audioId);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            logger.LogInformation(
                "Stopped stale ASR processing for audio {AudioId} because a newer task took ownership.",
                audioId);
        }
        catch (AsrMediaUploadLimitException ex)
        {
            dbContext.ChangeTracker.Clear();
            var terminalError = ex.Message.Length <= 1000 ? ex.Message : ex.Message[..1000];
            var updatedRows = await dbContext.Audios
                .Where(item => item.Id == audioId && item.AsrProcessingToken == processingToken)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.AsrTerminalError, terminalError));
            if (updatedRows == 0)
            {
                logger.LogInformation(
                    "Ignored stale ASR upload-limit failure for audio {AudioId} because a newer task took ownership.",
                    audioId);
                return;
            }
            logger.LogError(ex, "ASR media for audio {AudioId} cannot fit within the upload limit.", audioId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while processing ASR for audio {AudioId}", audioId);
            throw;
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

    public async Task CancelActiveTaskAsync(Audio audio)
    {
        if (string.IsNullOrEmpty(audio.AsrActiveTaskId))
        {
            return;
        }

        var cancelEndpoint = ResolveCancelEndpoint(_asrSettings.Endpoint, audio.AsrActiveTaskId);
        if (cancelEndpoint == null)
        {
            var exception = new InvalidOperationException(
                $"Cannot cancel ASR task {audio.AsrActiveTaskId} because the transcription endpoint is invalid.");
            logger.LogError(exception, "Failed to cancel ASR task {TaskId}.", audio.AsrActiveTaskId);
            throw exception;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, cancelEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _asrSettings.BearerToken);
            using var cancellationSource = new CancellationTokenSource(CancelRequestTimeout);
            using var response = await httpClient.SendAsync(request, cancellationSource.Token);
            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException(
                    $"Failed to cancel ASR task {audio.AsrActiveTaskId}: {content}",
                    null,
                    response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to cancel ASR task {TaskId}.", audio.AsrActiveTaskId);
            throw;
        }
    }

    private async Task RecordEmptyResultAsync(Audio audio, string processingToken)
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
        MarkProcessingTokenForConcurrencyCheck(audio, processingToken);
        await dbContext.SaveChangesAsync();
    }

    private bool HasRequiredSettings()
    {
        return !string.IsNullOrEmpty(_asrSettings.Endpoint) &&
               !string.IsNullOrEmpty(_asrSettings.BearerToken);
    }

    private async Task<bool> OwnsProcessingAsync(int audioId, string processingToken)
    {
        return await dbContext.Audios
            .AsNoTracking()
            .AnyAsync(audio => audio.Id == audioId && audio.AsrProcessingToken == processingToken);
    }

    private async Task SetActiveTaskAsync(Audio audio, string processingToken, string taskId)
    {
        audio.AsrActiveTaskId = taskId;
        MarkProcessingTokenForConcurrencyCheck(audio, processingToken);
        await dbContext.SaveChangesAsync();
    }

    private async Task ClearActiveTaskAsync(Audio audio, string processingToken, string taskId)
    {
        var ownsTask = await dbContext.Audios
            .AsNoTracking()
            .AnyAsync(item =>
                item.Id == audio.Id &&
                item.AsrProcessingToken == processingToken &&
                item.AsrActiveTaskId == taskId);
        if (!ownsTask)
        {
            return;
        }

        audio.AsrActiveTaskId = null;
        MarkProcessingTokenForConcurrencyCheck(audio, processingToken);
        await dbContext.SaveChangesAsync();
    }

    public static Uri? ResolveCancelEndpoint(string? endpoint, string taskId)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var transcriptionEndpoint) ||
            !transcriptionEndpoint.AbsolutePath.EndsWith(
                TranscriptionEndpointSuffix,
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new UriBuilder(transcriptionEndpoint)
        {
            Path = transcriptionEndpoint.AbsolutePath[..^TranscriptionEndpointSuffix.Length] +
                   $"/tasks/{Uri.EscapeDataString(taskId)}/cancel",
            Query = string.Empty,
            Fragment = string.Empty
        }.Uri;
    }

    public static bool IsSupportedMediaExtension(string extension)
    {
        return AllowedExtensions.Contains(extension);
    }

    public static string? GetRecognizedText(AsrResponse? response)
    {
        return response == null ? null : response.Text ?? string.Empty;
    }

    public static string BuildTaskId(int audioId, int segmentIndex, int attempt)
    {
        return $"audio-{audioId}-segment-{segmentIndex}-attempt-{attempt}-{Guid.NewGuid():N}";
    }

    private static string JoinSegmentText(IEnumerable<AsrTranscriptSegment> segments)
    {
        return string.Join(' ', segments.Select(segment => segment.Text).Where(text => !string.IsNullOrWhiteSpace(text)));
    }

    private static string NormalizeText(string text)
    {
        return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private void MarkProcessingTokenForConcurrencyCheck(Audio audio, string processingToken)
    {
        audio.AsrProcessingToken = processingToken;
        dbContext.Entry(audio).Property(item => item.AsrProcessingToken).IsModified = true;
    }

    private sealed record AsrTranscriptionPolicy(
        long UploadLimitBytes,
        int SegmentDurationSeconds,
        int OverlapSeconds);
}
