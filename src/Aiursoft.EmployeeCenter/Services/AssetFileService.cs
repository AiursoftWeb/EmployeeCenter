using Aiursoft.EmployeeCenter.Services.FileStorage;
using Aiursoft.Scanner.Abstractions;

namespace Aiursoft.EmployeeCenter.Services;

public class AssetFileService(StorageService storage) : ITransientDependency
{
    public const string AssetInvoiceFolder = "asset-invoices";
    public const string IntangibleAssetInvoiceFolder = "intangible-asset-invoices";
    public const string IntangibleAssetCertificateFolder = "intangible-asset-certificates";
    public const string TrademarkImageFolder = "intangible-assets/trademark-images";

    private static readonly string[] VaultFolders =
    [
        AssetInvoiceFolder,
        IntangibleAssetInvoiceFolder,
        IntangibleAssetCertificateFolder
    ];

    public bool IsExistingFile(string? logicalPath, string folder, bool isVault)
    {
        if (!IsDirectChild(logicalPath, folder))
        {
            return false;
        }

        try
        {
            return File.Exists(storage.GetFilePhysicalPath(logicalPath!, isVault));
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public bool IsValidReplacement(
        string? submittedPath,
        string? existingPath,
        string folder,
        bool isVault)
    {
        return string.IsNullOrWhiteSpace(submittedPath) ||
               string.Equals(submittedPath, existingPath, StringComparison.Ordinal) ||
               IsExistingFile(submittedPath, folder, isVault);
    }

    public string GetInternetUrl(string logicalPath)
    {
        if (Uri.TryCreate(logicalPath, UriKind.Absolute, out var uri) &&
            (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) ||
             string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)))
        {
            return uri.AbsoluteUri;
        }

        return storage.RelativePathToInternetUrl(logicalPath, IsVaultFile(logicalPath));
    }

    public static bool IsVaultFile(string? logicalPath)
    {
        return logicalPath is not null && VaultFolders.Any(folder => IsDirectChild(logicalPath, folder));
    }

    private static bool IsDirectChild(string? logicalPath, string folder)
    {
        if (string.IsNullOrWhiteSpace(logicalPath) ||
            !logicalPath.StartsWith(folder + "/", StringComparison.Ordinal))
        {
            return false;
        }

        var fileName = logicalPath[(folder.Length + 1)..];
        return fileName.Length > 0 && !fileName.Contains('/') && !fileName.Contains('\\');
    }
}
