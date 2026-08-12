using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Services.FileStorage;
using Aiursoft.Scanner.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.EmployeeCenter.Services;

public class AudioFileCleanupService(
    EmployeeCenterDbContext context,
    StorageService storageService,
    ILogger<AudioFileCleanupService> logger) : ITransientDependency
{
    private const int BatchSize = 100;
    private const int MaxAttemptCount = 10;

    public void QueueDeletion(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return;
        }

        var alreadyQueued = context.AudioFileDeletions.Local.Any(deletion => deletion.FilePath == filePath) ||
                            context.AudioFileDeletions.Any(deletion => deletion.FilePath == filePath);
        if (alreadyQueued)
        {
            return;
        }

        context.AudioFileDeletions.Add(new AudioFileDeletion
        {
            FilePath = filePath
        });
    }

    public async Task<int> CleanupQueuedAsync()
    {
        var utcNow = DateTime.UtcNow;
        var candidates = await context.AudioFileDeletions
            .Where(deletion => !deletion.IsDeadLetter && deletion.NextAttemptTime <= utcNow)
            .Where(deletion => !context.Audios.Any(audio =>
                audio.FilePath == deletion.FilePath || audio.PendingFilePath == deletion.FilePath))
            .OrderBy(deletion => deletion.NextAttemptTime)
            .ThenBy(deletion => deletion.CreatedTime)
            .Take(BatchSize)
            .ToListAsync();
        var removed = new List<AudioFileDeletion>(candidates.Count);
        foreach (var deletion in candidates)
        {
            var failure = TryDeleteFileWithFailure(deletion.FilePath);
            if (failure == null)
            {
                removed.Add(deletion);
                continue;
            }
            RecordFailure(deletion, failure, utcNow);
        }

        context.AudioFileDeletions.RemoveRange(removed);
        await context.SaveChangesAsync();
        return removed.Count;
    }

    public async Task TryCleanupQueuedAsync()
    {
        try
        {
            await CleanupQueuedAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Deferred cleanup of queued audio files until the next background retry.");
        }
    }

    public async Task<bool> DeleteIfUnreferencedAsync(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return true;
        }
        if (await context.Audios.AnyAsync(audio =>
                audio.FilePath == filePath || audio.PendingFilePath == filePath))
        {
            return false;
        }

        return TryDeleteFile(filePath);
    }

    internal bool TryDeleteFile(string? filePath)
    {
        return TryDeleteFileWithFailure(filePath) == null;
    }

    private Exception? TryDeleteFileWithFailure(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return null;
        }
        try
        {
            var physicalPath = storageService.GetVaultSubfolderFilePhysicalPath(filePath, "audio");
            if (File.Exists(physicalPath))
            {
                DeleteFile(physicalPath);
            }
            return null;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Skipped deleting audio vault file path {FilePath}.", filePath);
            return ex;
        }
    }

    private void RecordFailure(AudioFileDeletion deletion, Exception failure, DateTime utcNow)
    {
        deletion.AttemptCount++;
        deletion.LastError = failure.Message.Length <= 1000 ? failure.Message : failure.Message[..1000];
        deletion.IsDeadLetter = failure is ArgumentException || deletion.AttemptCount >= MaxAttemptCount;
        if (deletion.IsDeadLetter)
        {
            logger.LogError(
                failure,
                "Moved audio vault file path {FilePath} to the cleanup dead-letter queue after {AttemptCount} attempts.",
                deletion.FilePath,
                deletion.AttemptCount);
            return;
        }
        var delayMinutes = Math.Min(1 << Math.Min(deletion.AttemptCount - 1, 10), 24 * 60);
        deletion.NextAttemptTime = utcNow.AddMinutes(delayMinutes);
    }

    protected virtual void DeleteFile(string physicalPath)
    {
        File.Delete(physicalPath);
    }
}
