using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.Scanner.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.EmployeeCenter.Services;

public class AudioUploadCleanupService(
    EmployeeCenterDbContext context,
    AudioFileCleanupService fileCleanupService,
    ILogger<AudioUploadCleanupService> logger) : ITransientDependency
{
    private const int BatchSize = 100;

    public async Task<int> CleanupAsync(DateTime utcNow)
    {
        var candidates = await context.AudioUploads
            .Where(upload =>
                (upload.ConsumedTime != null || upload.ExpiresTime <= utcNow) &&
                !context.Audios.Any(audio =>
                    audio.FilePath == upload.FilePath || audio.PendingFilePath == upload.FilePath))
            .OrderBy(upload => upload.CreatedTime)
            .Take(BatchSize)
            .ToListAsync();

        var removableUploads = new List<AudioUpload>(candidates.Count);
        foreach (var upload in candidates)
        {
            if (fileCleanupService.TryDeleteFile(upload.FilePath))
            {
                removableUploads.Add(upload);
            }
        }

        context.AudioUploads.RemoveRange(removableUploads);
        await context.SaveChangesAsync();
        var removedDeletions = await fileCleanupService.CleanupQueuedAsync();
        logger.LogInformation(
            "Removed {RemovedCount} of {CandidateCount} audio upload records and {DeletionCount} queued files.",
            removableUploads.Count,
            candidates.Count,
            removedDeletions);
        return removableUploads.Count;
    }
}
