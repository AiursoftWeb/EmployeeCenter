using System.IO.Compression;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Services.FileStorage;
using Aiursoft.Scanner.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.EmployeeCenter.Services;

public record ImportResult(int FoldersCreated, int FilesImported, List<string> Errors);

public class ContractImportService(
    EmployeeCenterDbContext dbContext,
    StorageService storageService) : ITransientDependency
{
    public async Task<ImportResult> ImportFromZipAsync(
        Stream zipStream, int? targetFolderId, bool isPublic, ContractStatus status)
    {
        var foldersCreated = 0;
        var filesImported = 0;
        var errors = new List<string>();

        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
        var entries = archive.Entries.ToList();

        // Collect unique directory paths from zip entries
        var dirPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            var dirPath = Path.GetDirectoryName(entry.FullName)?.Replace('\\', '/') ?? "";
            dirPath = dirPath.TrimEnd('/');
            while (!string.IsNullOrEmpty(dirPath))
            {
                dirPaths.Add(dirPath);
                var lastSlash = dirPath.LastIndexOf('/');
                dirPath = lastSlash >= 0 ? dirPath[..lastSlash] : "";
            }
        }

        // Load all existing folders for find-or-create lookups
        var allFolders = await dbContext.ContractFolders.ToListAsync();

        // Map: full zip-dir-path -> ContractFolder object
        var pathFolderMap = new Dictionary<string, ContractFolder>(StringComparer.OrdinalIgnoreCase);

        // Sort by depth so parents are created before children
        var sortedDirs = dirPaths.OrderBy(p => p.Count(c => c == '/')).ToList();
        foreach (var dirPath in sortedDirs)
        {
            FindOrCreateFolder(dirPath);
        }

        // Save new folders to get real IDs
        await dbContext.SaveChangesAsync();

        // Process files
        foreach (var entry in entries)
        {
            if (entry.FullName.EndsWith('/')) continue;

            try
            {
                var fileName = Path.GetFileName(entry.FullName);
                if (string.IsNullOrEmpty(fileName)) continue;

                var dirPath = Path.GetDirectoryName(entry.FullName)?.Replace('\\', '/') ?? "";
                dirPath = dirPath.TrimEnd('/');

                int? parentFolderId = null;
                if (!string.IsNullOrEmpty(dirPath) && pathFolderMap.TryGetValue(dirPath, out var pf))
                    parentFolderId = pf.Id;

                var vaultPath = string.IsNullOrEmpty(dirPath)
                    ? $"contract/{fileName}"
                    : $"contract/{dirPath}/{fileName}";

                await using var entryStream = entry.Open();
                var savedPath = await storageService.SaveFromStream(vaultPath, entryStream, isVault: true);

                var contractName = Path.GetFileNameWithoutExtension(fileName);
                if (contractName.Length > 200)
                    contractName = contractName[..200];

                var contract = new Contract
                {
                    Name = contractName,
                    FilePath = savedPath,
                    Status = status,
                    IsPublic = isPublic,
                    CreateTime = DateTime.UtcNow,
                    FolderId = parentFolderId
                };
                dbContext.Contracts.Add(contract);
                filesImported++;
            }
            catch (Exception ex)
            {
                errors.Add($"'{entry.FullName}': {ex.Message}");
            }
        }

        await dbContext.SaveChangesAsync();

        return new ImportResult(foldersCreated, filesImported, errors);

        ContractFolder FindOrCreateFolder(string fullPath)
        {
            if (pathFolderMap.TryGetValue(fullPath, out var cached))
                return cached;

            var parts = fullPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            ContractFolder? currentParent = null;
            int? currentParentId = targetFolderId;
            var currentPath = "";

            foreach (var part in parts)
            {
                currentPath = string.IsNullOrEmpty(currentPath) ? part : $"{currentPath}/{part}";

                if (pathFolderMap.TryGetValue(currentPath, out var existing))
                {
                    currentParent = existing;
                    currentParentId = existing.Id;
                    continue;
                }

                // Find existing folder under current parent
                ContractFolder? folder;
                if (currentParent != null && currentParent.Id == 0)
                {
                    // Parent is newly created and not yet saved — check its SubFolders collection
                    folder = currentParent.SubFolders
                        .FirstOrDefault(f => f.Name.Equals(part, StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    // Parent has a real ID (or is null for root) — query allFolders
                    folder = allFolders
                        .FirstOrDefault(f => f.ParentFolderId == currentParentId &&
                                             f.Name.Equals(part, StringComparison.OrdinalIgnoreCase));
                }

                if (folder == null)
                {
                    folder = new ContractFolder
                    {
                        Name = part,
                        ParentFolderId = currentParentId,
                        CreateTime = DateTime.UtcNow
                    };
                    currentParent?.SubFolders.Add(folder);
                    dbContext.ContractFolders.Add(folder);
                    allFolders.Add(folder);
                    foldersCreated++;
                }

                pathFolderMap[currentPath] = folder;
                currentParent = folder;
                currentParentId = folder.Id;
            }

            return currentParent!;
        }
    }
}
