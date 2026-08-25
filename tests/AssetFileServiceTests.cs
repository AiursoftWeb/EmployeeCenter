using Aiursoft.EmployeeCenter.Services;
using Aiursoft.EmployeeCenter.Services.FileStorage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;

namespace Aiursoft.EmployeeCenter.Tests;

[TestClass]
public class AssetFileServiceTests
{
    private AssetFileService _assetFiles = null!;
    private StorageService _storage = null!;
    private string _tempPath = null!;

    [TestInitialize]
    public void Initialize()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), $"EmployeeCenterAssetFiles_{Guid.NewGuid():N}");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:Path"] = _tempPath
            })
            .Build();
        var folders = new FeatureFoldersProvider(new StorageRootPathProvider(configuration));
        var locks = new FileLockProvider(new MemoryCache(new MemoryCacheOptions()));
        _storage = new StorageService(folders, locks, new EphemeralDataProtectionProvider());
        _assetFiles = new AssetFileService(_storage);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempPath))
        {
            Directory.Delete(_tempPath, recursive: true);
        }
    }

    [TestMethod]
    public async Task NewVaultFileIsValidatedAndDeliveredPrivately()
    {
        const string path = "asset-invoices/invoice.pdf";
        await using var content = new MemoryStream("invoice"u8.ToArray());
        await _storage.SaveFromStream(path, content, isVault: true);

        Assert.IsTrue(_assetFiles.IsExistingFile(
            path,
            AssetFileService.AssetInvoiceFolder,
            isVault: true));
        StringAssert.StartsWith(_assetFiles.GetInternetUrl(path), "/download-private/asset-invoices/invoice.pdf?token=");
    }

    [TestMethod]
    public void LegacyWorkspaceFileRemainsPublicAndCanBePreserved()
    {
        const string path = "assets/legacy-invoice.pdf";

        Assert.IsTrue(_assetFiles.IsValidReplacement(
            path,
            path,
            AssetFileService.AssetInvoiceFolder,
            isVault: true));
        Assert.AreEqual("/download/assets/legacy-invoice.pdf", _assetFiles.GetInternetUrl(path));
    }

    [TestMethod]
    public void MissingOrCrossFolderReplacementIsRejected()
    {
        Assert.IsFalse(_assetFiles.IsValidReplacement(
            "asset-invoices/missing.pdf",
            "assets/legacy.pdf",
            AssetFileService.AssetInvoiceFolder,
            isVault: true));
        Assert.IsFalse(_assetFiles.IsValidReplacement(
            "contract/another-file.pdf",
            "assets/legacy.pdf",
            AssetFileService.AssetInvoiceFolder,
            isVault: true));
    }
}
