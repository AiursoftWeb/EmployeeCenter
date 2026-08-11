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
        string? convertedPath = null;
        try
        {
            var finalPath = sourcePath;
            if (IsVideoFile(sourcePath))
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
            await context.SaveChangesAsync();

            if (replaced)
            {
                await fileCleanupService.DeleteIfUnreferencedAsync(originalPath);
            }
            if (convertedPath != null)
            {
                await fileCleanupService.DeleteIfUnreferencedAsync(sourcePath);
            }

            taskQueue.QueueWithDependency<AsrService>(
                queueName: "asr",
                taskName: $"Process ASR for audio {audioId}",
                task: service => service.ProcessAudioAsrAsync(audioId));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to process uploaded media for audio {AudioId}.", audioId);
            await MarkFailedAsync(audioId, processingToken, ex.Message);
            if (convertedPath != null)
            {
                await fileCleanupService.DeleteIfUnreferencedAsync(convertedPath);
            }
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

    private async Task MarkFailedAsync(int audioId, string processingToken, string error)
    {
        context.ChangeTracker.Clear();
        var audio = await context.Audios.FirstOrDefaultAsync(item => item.Id == audioId);
        if (audio == null || audio.MediaProcessingToken != processingToken)
        {
            return;
        }
        audio.MediaStatus = AudioMediaStatus.Failed;
        audio.MediaProcessingError = error.Length <= 1000 ? error : error[..1000];
        audio.MediaProcessingToken = null;
        audio.MediaProcessingStartedTime = null;
        await context.SaveChangesAsync();
    }

    private static bool IsVideoFile(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".mov", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".mkv", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".avi", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".webm", StringComparison.OrdinalIgnoreCase);
    }
}
