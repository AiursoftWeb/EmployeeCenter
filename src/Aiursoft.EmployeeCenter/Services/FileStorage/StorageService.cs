using Aiursoft.Scanner.Abstractions;
using Microsoft.AspNetCore.DataProtection;

namespace Aiursoft.EmployeeCenter.Services.FileStorage;

public enum FilePermission
{
    Upload,
    Download
}

/// <summary>
/// Represents a service for storing and managing files. (Level 3: Business Gateway)
/// </summary>
public class StorageService(
    FeatureFoldersProvider folders,
    FileLockProvider fileLockProvider,
    IDataProtectionProvider dataProtectionProvider) : ITransientDependency
{

    #region public async Task<string> Save(string logicalPath, IFormFile file, bool isVault = false)
    /// <summary>
    /// Saves a file to the storage.
    /// </summary>
    /// <param name="logicalPath">The logical path (relative to Workspace) where the file will be saved.</param>
    /// <param name="file">The file to be saved.</param>
    /// <param name="isVault">Whether to save to the private Vault.</param>
    /// <returns>The actual logical path where the file is saved (may differ if renamed).</returns>
    public async Task<string> Save(string logicalPath, IFormFile file, bool isVault = false)
    {
        var (root, physicalPath) = await ResolveSavePath(logicalPath, isVault);

        await using var fileStream = new FileStream(physicalPath, FileMode.Create);
        await file.CopyToAsync(fileStream);

        return Path.GetRelativePath(root, physicalPath).Replace("\\", "/");
    }

    /// <summary>
    /// Saves a stream to the storage. Used for extracting files from archives, etc.
    /// </summary>
    public async Task<string> SaveFromStream(string logicalPath, Stream stream, bool isVault = false)
    {
        var (root, physicalPath) = await ResolveSavePath(logicalPath, isVault);

        await using var fileStream = new FileStream(physicalPath, FileMode.Create);
        await stream.CopyToAsync(fileStream);

        return Path.GetRelativePath(root, physicalPath).Replace("\\", "/");
    }

    private async Task<(string root, string physicalPath)> ResolveSavePath(string logicalPath, bool isVault)
    {
        var root = isVault ? folders.GetVaultFolder() : folders.GetWorkspaceFolder();

        var physicalPath = Path.GetFullPath(Path.Combine(root, logicalPath));

        if (!physicalPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Path traversal attempt detected!");
        }

        var directory = Path.GetDirectoryName(physicalPath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory!);
        }

        var lockObj = fileLockProvider.GetLock(directory!);
        await lockObj.WaitAsync();
        try
        {
            while (File.Exists(physicalPath))
            {
                var fileName = "_" + Path.GetFileName(physicalPath);
                physicalPath = Path.Combine(directory!, fileName);
            }

            File.Create(physicalPath).Close();
        }
        finally
        {
            lockObj.Release();
        }

        return (root, physicalPath);
    }
    #endregion

    #region public string GetFilePhysicalPath(string logicalPath, bool isVault = false)
    /// <summary>
    /// Retrieves the physical file path for a given logical path.
    /// Defaults to Workspace.
    /// </summary>
    public string GetFilePhysicalPath(string logicalPath, bool isVault = false)
    {
        var root = isVault ? folders.GetVaultFolder() : folders.GetWorkspaceFolder();
        var physicalPath = Path.GetFullPath(Path.Combine(root, logicalPath));

        if (!physicalPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Restricted path access!");
        }
        return physicalPath;
    }
    #endregion

    #region public string GetToken(string path, FilePermission permission)
    public string GetToken(string path, FilePermission permission)
    {
        // Create a time-limited data protector with 60-minute expiration
        var protector = dataProtectionProvider
            .CreateProtector("FileOperation")
            .ToTimeLimitedDataProtector();

        var tokenData = $"{path}|{permission}";

        // Protect the path with time-limited encryption
        var protectedData = protector.Protect(tokenData, TimeSpan.FromMinutes(60));
        return protectedData;
    }
    #endregion

    #region public bool ValidateToken(string requestPath, string tokenString, FilePermission requiredPermission)
    public bool ValidateToken(string requestPath, string tokenString, FilePermission requiredPermission)
    {
        if (string.IsNullOrEmpty(requestPath) || requestPath.Contains("..")) return false; // Patch for path traversal
        try
        {
            // Create the same protector used for token generation
            var protector = dataProtectionProvider
                .CreateProtector("FileOperation")
                .ToTimeLimitedDataProtector();

            // Unprotect and validate expiration automatically
            var tokenData = protector.Unprotect(tokenString);
            var parts = tokenData.Split('|');
            if (parts.Length != 2) return false;

            var authorizedPath = parts[0];
            var authorizedPermission = Enum.Parse<FilePermission>(parts[1]);

            if (authorizedPermission != requiredPermission) return false;

            // Verify the token authorizes access to the requested path
            // Fix: Enforce trailing slash to prevent partial directory matching (e.g. "A" matching "AA")
            var normalizedRequestPath = requestPath.TrimEnd('/') + "/";
            var normalizedAuthorizedPath = authorizedPath.TrimEnd('/') + "/";

            return normalizedRequestPath.StartsWith(normalizedAuthorizedPath, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // Token is invalid, expired, or tampered with
            return false;
        }
    }
    #endregion

    #region private string RelativePathToUriPath(string relativePath)
    /// <summary>
    /// Converts a logical path to a URI-compatible path.
    /// </summary>
    private string RelativePathToUriPath(string relativePath)
    {
        var urlPath = Uri.EscapeDataString(relativePath)
            .Replace("%5C", "/")
            .Replace("%5c", "/")
            .Replace("%2F", "/")
            .Replace("%2f", "/")
            .TrimStart('/');
        return urlPath;
    }
    #endregion

    #region public string RelativePathToInternetUrl(string relativePath, HttpContext context, bool isVault = false)
    public string RelativePathToInternetUrl(string relativePath, HttpContext context, bool isVault = false)
    {
        if (isVault)
        {
            var token = GetToken(relativePath, FilePermission.Download);
            return $"{context.Request.Scheme}://{context.Request.Host}/download-private/{RelativePathToUriPath(relativePath)}?token={token}";
        }
        return $"{context.Request.Scheme}://{context.Request.Host}/download/{RelativePathToUriPath(relativePath)}";
    }
    #endregion

    #region  public string RelativePathToInternetUrl(string relativePath, bool isVault = false)
    public string RelativePathToInternetUrl(string relativePath, bool isVault = false)
    {
        if (isVault)
        {
            var token = GetToken(relativePath, FilePermission.Download);
            return $"/download-private/{RelativePathToUriPath(relativePath)}?token={token}";
        }
        return $"/download/{RelativePathToUriPath(relativePath)}";
    }
    #endregion

    #region public string GetUploadUrl(string subfolder, bool isVault = false)
    public string GetUploadUrl(string subfolder, bool isVault = false)
    {
        var token = GetToken(subfolder, FilePermission.Upload);
        if (isVault)
        {
            return $"/upload-private/{subfolder}?token={token}";
        }
        return $"/upload/{subfolder}?token={token}";
    }
    #endregion

}
