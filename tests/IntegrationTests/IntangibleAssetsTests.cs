using Aiursoft.EmployeeCenter.Services.FileStorage;

namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class IntangibleAssetTests : TestBase
{
    [TestMethod]
    public async Task IntangibleAssetLifecycleWithNewFieldsTest()
    {
        // 1. Login as admin
        await LoginAsAdmin();

        // 2. Create a Company Entity first
        int entityId;
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var entity = new CompanyEntity
            {
                CompanyName = "Test Entity for Intangible",
                EntityCode = "TE-INT-01"
            };
            db.CompanyEntities.Add(entity);
            await db.SaveChangesAsync();
            entityId = entity.Id;
        }

        // 3. Create Intangible Asset with new fields
        var assetName = "Test Domain with New Fields";
        var suffix = Guid.NewGuid().ToString("N");
        var trademarkImageUrl = $"intangible-assets/trademark-images/{suffix}.png";
        var updatedTrademarkImageUrl = $"intangible-assets/trademark-images/{suffix}-updated.jpg";
        var invoiceFileUrl = $"intangible-asset-invoices/{suffix}.pdf";
        var registrationCertificateUrl = $"intangible-asset-certificates/{suffix}.pdf";
        var storage = GetService<StorageService>();
        await SaveFile(storage, trademarkImageUrl, isVault: false);
        await SaveFile(storage, updatedTrademarkImageUrl, isVault: false);
        await SaveFile(storage, invoiceFileUrl, isVault: true);
        await SaveFile(storage, registrationCertificateUrl, isVault: true);
        var createResponse = await PostForm("/IntangibleAssets/Create", new Dictionary<string, string>
        {
            { "Name", assetName },
            { "Type", ((int)IntangibleAssetType.Domain).ToString() },
            { "Status", ((int)IntangibleAssetStatus.Active).ToString() },
            { "Supplier", "GoDaddy" },
            { "Currency", "USD" },
            { "PurchasePrice", "99.99" },
            { "TrademarkImageUrl", trademarkImageUrl },
            { "InvoiceFileUrl", invoiceFileUrl },
            { "RegistrationCertificateUrl", registrationCertificateUrl },
            { "IsPublic", "true" },
            { "CompanyEntityId", entityId.ToString() }
        });
        AssertRedirect(createResponse, "/IntangibleAssets");

        Guid assetId;
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var asset = await db.IntangibleAssets.FirstAsync(a => a.Name == assetName);
            assetId = asset.Id;
            Assert.AreEqual("USD", asset.Currency);
            Assert.AreEqual(99.99m, asset.PurchasePrice);
            Assert.AreEqual(trademarkImageUrl, asset.TrademarkImageUrl);
            Assert.AreEqual(invoiceFileUrl, asset.InvoiceFileUrl);
            Assert.AreEqual(registrationCertificateUrl, asset.RegistrationCertificateUrl);
            Assert.IsTrue(asset.IsPublic);
            Assert.AreEqual(entityId, asset.CompanyEntityId);
        }

        var editPageResponse = await Http.GetAsync($"/IntangibleAssets/Edit/{assetId}");
        editPageResponse.EnsureSuccessStatusCode();
        var editPageHtml = await editPageResponse.Content.ReadAsStringAsync();
        StringAssert.Contains(editPageHtml, trademarkImageUrl);

        // 4. Edit Intangible Asset
        var editResponse = await PostForm($"/IntangibleAssets/Edit/{assetId}", new Dictionary<string, string>
        {
            { "Id", assetId.ToString() },
            { "Name", "Updated Domain Name" },
            { "Type", ((int)IntangibleAssetType.Domain).ToString() },
            { "Status", ((int)IntangibleAssetStatus.Running).ToString() },
            { "Supplier", "NameCheap" },
            { "Currency", "HKD" },
            { "PurchasePrice", "150.00" },
            { "TrademarkImageUrl", updatedTrademarkImageUrl },
            { "InvoiceFileUrl", invoiceFileUrl },
            { "RegistrationCertificateUrl", registrationCertificateUrl },
            { "IsPublic", "false" },
            { "CompanyEntityId", entityId.ToString() }
        });
        AssertRedirect(editResponse, "/IntangibleAssets");

        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var asset = await db.IntangibleAssets.FirstAsync(a => a.Id == assetId);
            Assert.AreEqual("Updated Domain Name", asset.Name);
            Assert.AreEqual("HKD", asset.Currency);
            Assert.AreEqual(150.00m, asset.PurchasePrice);
            Assert.AreEqual(updatedTrademarkImageUrl, asset.TrademarkImageUrl);
            Assert.AreEqual(invoiceFileUrl, asset.InvoiceFileUrl);
            Assert.AreEqual(registrationCertificateUrl, asset.RegistrationCertificateUrl);
            Assert.IsFalse(asset.IsPublic);
        }

        // 5. Test Visibility in CompanyIntangibleAssets (Public view)
        // Publicly visible first
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var asset = await db.IntangibleAssets.FirstAsync(a => a.Id == assetId);
            asset.IsPublic = true;
            await db.SaveChangesAsync();
        }

        var managementDetailsResponse = await Http.GetAsync($"/IntangibleAssets/Details/{assetId}");
        managementDetailsResponse.EnsureSuccessStatusCode();
        var managementDetailsHtml = await managementDetailsResponse.Content.ReadAsStringAsync();
        StringAssert.Contains(managementDetailsHtml, "View Trademark Image");
        StringAssert.Contains(managementDetailsHtml, updatedTrademarkImageUrl);

        var publicDetailsWithImageResponse = await Http.GetAsync($"/CompanyIntangibleAssets/Details/{assetId}");
        publicDetailsWithImageResponse.EnsureSuccessStatusCode();
        var publicDetailsWithImageHtml = await publicDetailsWithImageResponse.Content.ReadAsStringAsync();
        StringAssert.Contains(publicDetailsWithImageHtml, "View Trademark Image");
        StringAssert.Contains(publicDetailsWithImageHtml, updatedTrademarkImageUrl);

        var publicIndexResponse = await Http.GetAsync("/CompanyIntangibleAssets");
        publicIndexResponse.EnsureSuccessStatusCode();
        var indexHtml = await publicIndexResponse.Content.ReadAsStringAsync();
        Assert.IsTrue(indexHtml.Contains("Updated Domain Name"));

        // Now make it private
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var asset = await db.IntangibleAssets.FirstAsync(a => a.Id == assetId);
            asset.IsPublic = false;
            await db.SaveChangesAsync();
        }

        publicIndexResponse = await Http.GetAsync("/CompanyIntangibleAssets");
        publicIndexResponse.EnsureSuccessStatusCode();
        indexHtml = await publicIndexResponse.Content.ReadAsStringAsync();
        Assert.IsFalse(indexHtml.Contains("Updated Domain Name"));

        var publicDetailsResponse = await Http.GetAsync($"/CompanyIntangibleAssets/Details/{assetId}");
        Assert.AreEqual(HttpStatusCode.NotFound, publicDetailsResponse.StatusCode);

        // 6. Test Assigned Visibility
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var asset = await db.IntangibleAssets.FirstAsync(a => a.Id == assetId);
            var user = await db.Users.FirstAsync(u => u.UserName == "admin");
            asset.IsPublic = false;
            asset.AssigneeId = user.Id;
            await db.SaveChangesAsync();
        }

        // Should now be visible because assigned to me (admin)
        var assignedIndexResponse = await Http.GetAsync("/CompanyIntangibleAssets");
        assignedIndexResponse.EnsureSuccessStatusCode();
        var assignedHtml = await assignedIndexResponse.Content.ReadAsStringAsync();
        Assert.IsTrue(assignedHtml.Contains("Updated Domain Name"));

        var assignedDetailsResponse = await Http.GetAsync($"/CompanyIntangibleAssets/Details/{assetId}");
        assignedDetailsResponse.EnsureSuccessStatusCode();

        // 7. Delete
        var deleteResponse = await PostForm($"/IntangibleAssets/Delete/{assetId}", new Dictionary<string, string>());
        AssertRedirect(deleteResponse, "/IntangibleAssets");
    }

    [TestMethod]
    public async Task LegacyWorkspaceDocumentsRemainEditable()
    {
        await LoginAsAdmin();

        var asset = new IntangibleAsset
        {
            Id = Guid.NewGuid(),
            Name = "Legacy intangible asset",
            Type = IntangibleAssetType.Trademark,
            Status = IntangibleAssetStatus.Active,
            InvoiceFileUrl = "intangible-assets/invoices/legacy-invoice.pdf",
            RegistrationCertificateUrl = "intangible-assets/certificates/legacy-certificate.pdf"
        };
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            db.IntangibleAssets.Add(asset);
            await db.SaveChangesAsync();
        }

        var editPage = await Http.GetAsync($"/IntangibleAssets/Edit/{asset.Id}");
        editPage.EnsureSuccessStatusCode();
        var html = await editPage.Content.ReadAsStringAsync();
        StringAssert.Contains(html, "/download/intangible-assets/invoices/legacy-invoice.pdf");
        StringAssert.Contains(html, "/upload-private/intangible-asset-invoices");

        var editResponse = await PostForm($"/IntangibleAssets/Edit/{asset.Id}", new Dictionary<string, string>
        {
            ["Id"] = asset.Id.ToString(),
            ["Name"] = "Updated legacy intangible asset",
            ["Type"] = ((int)asset.Type).ToString(),
            ["Status"] = ((int)asset.Status).ToString(),
            ["Currency"] = asset.Currency,
            ["InvoiceFileUrl"] = asset.InvoiceFileUrl!,
            ["RegistrationCertificateUrl"] = asset.RegistrationCertificateUrl!
        });
        AssertRedirect(editResponse, "/IntangibleAssets");

        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var updated = await db.IntangibleAssets.FindAsync(asset.Id);
            Assert.AreEqual("Updated legacy intangible asset", updated!.Name);
            Assert.AreEqual(asset.InvoiceFileUrl, updated.InvoiceFileUrl);
            Assert.AreEqual(asset.RegistrationCertificateUrl, updated.RegistrationCertificateUrl);
        }
    }

    [TestMethod]
    public async Task IntangibleAssetCanBeCreatedAndEditedWithoutTrademarkImageTest()
    {
        await LoginAsAdmin();

        var assetName = $"Intangible Asset Without Trademark Image {Guid.NewGuid()}";
        var createResponse = await PostForm("/IntangibleAssets/Create", new Dictionary<string, string>
        {
            { "Name", assetName },
            { "Type", ((int)IntangibleAssetType.Trademark).ToString() },
            { "Status", ((int)IntangibleAssetStatus.Applying).ToString() },
            { "Currency", "CNY" }
        });
        AssertRedirect(createResponse, "/IntangibleAssets");

        Guid assetId;
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var asset = await db.IntangibleAssets.FirstAsync(a => a.Name == assetName);
            assetId = asset.Id;
            Assert.IsNull(asset.TrademarkImageUrl);
        }

        var editResponse = await PostForm($"/IntangibleAssets/Edit/{assetId}", new Dictionary<string, string>
        {
            { "Id", assetId.ToString() },
            { "Name", $"{assetName} Updated" },
            { "Type", ((int)IntangibleAssetType.Trademark).ToString() },
            { "Status", ((int)IntangibleAssetStatus.Active).ToString() },
            { "Currency", "CNY" }
        });
        AssertRedirect(editResponse, "/IntangibleAssets");

        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var asset = await db.IntangibleAssets.FirstAsync(a => a.Id == assetId);
            Assert.IsNull(asset.TrademarkImageUrl);
        }

        var deleteResponse = await PostForm($"/IntangibleAssets/Delete/{assetId}", new Dictionary<string, string>());
        AssertRedirect(deleteResponse, "/IntangibleAssets");
    }

    private static async Task SaveFile(StorageService storage, string path, bool isVault)
    {
        await using var content = new MemoryStream("test"u8.ToArray());
        await storage.SaveFromStream(path, content, isVault);
    }
}
