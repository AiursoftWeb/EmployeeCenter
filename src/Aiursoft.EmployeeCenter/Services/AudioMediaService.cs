using Aiursoft.Canon.TaskQueue;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Services.FileStorage;
using Aiursoft.Scanner.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.EmployeeCenter.Services;

public class AudioMediaService(
    EmployeeCenterDbContext context,
    AsrService asrService,
    AsrMediaProcessor mediaProcessor,
    StorageService storageService,
    AudioFileCleanupService fileCleanupService,
    ServiceTaskQueue taskQueue,
    ILogger<AudioMediaService> logger) : ITransientDependency
{
    public async Task ProcessAsync(int audioId)
    {
        var audio = await context.Audios.FindAsync(audioId);
        if (audio == null || audio.MediaStatus != AudioMediaStatus.Uploaded)
        {
            return;
        }

        var processingToken = Guid.NewGuid().ToString("N");
        audio.MediaStatus = AudioMediaStatus.Processing;
        audio.MediaProcessingToken = processingToken;
        audio.MediaProcessingStartedTime = DateTime.UtcNow;
        audio.MediaProcessingError = null;
        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return;
        }

        var sourcePath = audio.PendingFilePath ?? audio.FilePath;
        var replacing = audio.PendingFilePath != null;
        string? convertedPath = null;
        try
        {
            var extension = Path.GetExtension(sourcePath);
            if (!AsrService.IsSupportedMediaExtension(extension))
            {
                throw new InvalidOperationException($"Media extension {extension} is not supported.");
            }

            var physicalSourcePath = storageService.GetVaultSubfolderFilePhysicalPath(sourcePath, "audio");
            var probe = await mediaProcessor.ProbeAsync(physicalSourcePath);
            var finalPath = sourcePath;
            if (probe.HasVideoStream)
            {
                convertedPath = await ExtractAudioAsync(sourcePath);
                finalPath = convertedPath;
            }

            if (audio.PendingFilePath != null)
            {
                await asrService.CancelActiveTaskAsync(audio);
            }
            var originalPath = audio.FilePath;
            var replaced = audio.PendingFilePath != null;
            if (replaced)
            {
                await ClearAsrResultsAsync(audio);
            }

            audio.FilePath = finalPath;
            audio.PendingFilePath = null;
            audio.MediaStatus = AudioMediaStatus.Ready;
            audio.MediaProcessingToken = null;
            audio.MediaProcessingStartedTime = null;
            audio.AsrProcessingToken = Guid.NewGuid().ToString("N");
            if (replaced)
            {
                fileCleanupService.QueueDeletion(originalPath);
            }
            if (convertedPath != null)
            {
                fileCleanupService.QueueDeletion(sourcePath);
            }
            await context.SaveChangesAsync();
            await fileCleanupService.TryCleanupQueuedAsync();

            var asrProcessingToken = audio.AsrProcessingToken;
            taskQueue.QueueWithDependency<AsrService>(
                queueName: "asr",
                taskName: $"Process ASR for audio {audioId}",
                task: service => service.ProcessAudioAsrAsync(audioId, asrProcessingToken));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to process uploaded media for audio {AudioId}.", audioId);
            await HandleFailureAsync(
                audioId,
                processingToken,
                sourcePath,
                convertedPath,
                replacing,
                GetUserFacingError(ex));
        }
    }

    private async Task<string> ExtractAudioAsync(string sourcePath)
    {
        var physicalSourcePath = storageService.GetVaultSubfolderFilePhysicalPath(sourcePath, "audio");
        var outputDirectory = Path.GetDirectoryName(physicalSourcePath) ??
                              throw new InvalidOperationException("The uploaded video path has no parent directory.");
        var outputPrefix = $"{Path.GetFileNameWithoutExtension(sourcePath)}-audio-{Guid.NewGuid():N}";
        var physicalAudioPath = await mediaProcessor.ExtractAudioTrackAsync(
            physicalSourcePath,
            outputDirectory,
            outputPrefix);
        var logicalDirectory = Path.GetDirectoryName(sourcePath)?.Replace("\\", "/");
        return $"{logicalDirectory}/{Path.GetFileName(physicalAudioPath)}";
    }

    private async Task ClearAsrResultsAsync(Audio audio)
    {
        var transcript = await context.AudioAsrResults.FindAsync(audio.Id);
        if (transcript != null)
        {
            context.AudioAsrResults.Remove(transcript);
        }
        var segments = await context.AudioAsrSegments
            .Where(segment => segment.AudioId == audio.Id)
            .ToListAsync();
        context.AudioAsrSegments.RemoveRange(segments);
        audio.AsrAttemptCount = 0;
        audio.EmptyResultCount = 0;
        audio.LastAsrAttemptTime = null;
        audio.AsrProcessingToken = Guid.NewGuid().ToString("N");
        audio.AsrActiveTaskId = null;
        audio.AsrTerminalError = null;
    }

    private async Task HandleFailureAsync(
        int audioId,
        string processingToken,
        string sourcePath,
        string? convertedPath,
        bool replacing,
        string error)
    {
        context.ChangeTracker.Clear();
        var audio = await context.Audios.FirstOrDefaultAsync(item => item.Id == audioId);
        if (audio == null || audio.MediaProcessingToken != processingToken)
        {
            if (convertedPath != null)
            {
                fileCleanupService.QueueDeletion(convertedPath);
                await context.SaveChangesAsync();
                await fileCleanupService.TryCleanupQueuedAsync();
            }
            return;
        }

        if (replacing && audio.PendingFilePath == sourcePath)
        {
            audio.PendingFilePath = null;
            audio.MediaStatus = AudioMediaStatus.Ready;
        }
        else
        {
            audio.MediaStatus = AudioMediaStatus.Failed;
        }
        audio.MediaProcessingError = error;
        audio.MediaProcessingToken = null;
        audio.MediaProcessingStartedTime = null;
        fileCleanupService.QueueDeletion(sourcePath);
        if (convertedPath != null)
        {
            fileCleanupService.QueueDeletion(convertedPath);
        }
        await context.SaveChangesAsync();
        await fileCleanupService.TryCleanupQueuedAsync();
    }

    private static string GetUserFacingError(Exception exception)
    {
        if (exception is OperationCanceledException or TimeoutException)
        {
            return "Media processing timed out.";
        }
        if (exception.Message.Contains("does not contain a decodable audio stream", StringComparison.Ordinal))
        {
            return "The uploaded media does not contain a decodable audio stream.";
        }
        if (exception.Message.StartsWith("Media extension ", StringComparison.Ordinal))
        {
            return "The uploaded file extension is not supported.";
        }
        return "The uploaded file could not be processed.";
    }

}
