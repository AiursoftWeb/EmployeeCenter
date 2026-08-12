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

    public void QueueDeletion(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
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
        var candidates = await context.AudioFileDeletions
            .Where(deletion => !context.Audios.Any(audio =>
                audio.FilePath == deletion.FilePath || audio.PendingFilePath == deletion.FilePath))
            .OrderBy(deletion => deletion.CreatedTime)
            .Take(BatchSize)
            .ToListAsync();
        var removed = new List<AudioFileDeletion>(candidates.Count);
        foreach (var deletion in candidates)
        {
            if (TryDeleteFile(deletion.FilePath))
            {
                removed.Add(deletion);
            }
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
        if (string.IsNullOrEmpty(filePath))
        {
            return true;
        }
        try
        {
            var physicalPath = storageService.GetVaultSubfolderFilePhysicalPath(filePath, "audio");
            if (File.Exists(physicalPath))
            {
                DeleteFile(physicalPath);
            }
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Skipped deleting audio vault file path {FilePath}.", filePath);
            return false;
        }
    }

    protected virtual void DeleteFile(string physicalPath)
    {
        File.Delete(physicalPath);
    }
}
