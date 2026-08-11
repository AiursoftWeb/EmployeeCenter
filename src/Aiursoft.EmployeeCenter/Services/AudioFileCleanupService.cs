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

        try
        {
            var physicalPath = storageService.GetVaultSubfolderFilePhysicalPath(filePath, "audio");
            if (File.Exists(physicalPath))
            {
                File.Delete(physicalPath);
            }
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Skipped deleting audio vault file path {FilePath}.", filePath);
            return false;
        }
    }
}
