using Aiursoft.EmployeeCenter.Services.FileStorage;

namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class AssetFileCompatibilityTests : TestBase
{
    [TestMethod]
    public async Task LegacyInvoiceCanBeKeptAndReplacedByVaultFile()
    {
        await LoginAsAdmin();

        Guid assetId;
        int modelId;
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var category = new AssetCategory
            {
                Name = $"Compatibility {Guid.NewGuid():N}",
                Code = $"C{Guid.NewGuid():N}"[..10]
            };
            var model = new AssetModel
            {
                Category = category,
                Brand = "Test",
                ModelName = "Compatibility model"
            };
            var asset = new Asset
            {
                Id = Guid.NewGuid(),
                AssetTag = $"LEGACY-{Guid.NewGuid():N}"[..20],
                Model = model,
                Status = AssetStatus.Idle,
                InvoiceFileUrl = "assets/legacy-invoice.pdf"
            };
            db.Assets.Add(asset);
            await db.SaveChangesAsync();
            assetId = asset.Id;
            modelId = model.Id;
        }

        var editPage = await Http.GetAsync($"/Assets/Edit/{assetId}");
        editPage.EnsureSuccessStatusCode();
        var html = await editPage.Content.ReadAsStringAsync();
        StringAssert.Contains(html, "/download/assets/legacy-invoice.pdf");
        StringAssert.Contains(html, "/upload-private/asset-invoices");

        var legacyEdit = await EditAsset(assetId, modelId, "assets/legacy-invoice.pdf");
        AssertRedirect(legacyEdit, "/Assets");

        var legacyDownload = await Http.GetAsync($"/Assets/DownloadInvoice?id={assetId}");
        AssertRedirect(legacyDownload, "/download/assets/legacy-invoice.pdf");

        var newPath = $"asset-invoices/{Guid.NewGuid():N}.pdf";
        var storage = GetService<StorageService>();
        await using (var content = new MemoryStream("invoice"u8.ToArray()))
        {
            await storage.SaveFromStream(newPath, content, isVault: true);
        }

        var vaultEdit = await EditAsset(assetId, modelId, newPath);
        AssertRedirect(vaultEdit, "/Assets");

        var vaultDownload = await Http.GetAsync($"/Assets/DownloadInvoice?id={assetId}");
        Assert.AreEqual(HttpStatusCode.Found, vaultDownload.StatusCode);
        StringAssert.StartsWith(
            vaultDownload.Headers.Location?.OriginalString ?? string.Empty,
            "/download-private/asset-invoices/");
    }

    private async Task<HttpResponseMessage> EditAsset(Guid id, int modelId, string invoicePath)
    {
        return await PostForm($"/Assets/Edit/{id}", new Dictionary<string, string>
        {
            ["Id"] = id.ToString(),
            ["AssetTag"] = $"EDITED-{id:N}"[..20],
            ["ModelId"] = modelId.ToString(),
            ["Status"] = ((int)AssetStatus.Idle).ToString(),
            ["InvoiceFileUrl"] = invoicePath
        });
    }
}
