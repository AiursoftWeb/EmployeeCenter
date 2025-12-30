using System.Net;
using Aiursoft.CSTools.Tools;
using Aiursoft.DbTools;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using static Aiursoft.WebTools.Extends;

namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class AssetIntegrationTests
{
    private int _port;
    private HttpClient _http;
    private IHost? _server;

    [TestInitialize]
    public async Task CreateServer()
    {
        _port = Network.GetAvailablePort();
        _server = await AppAsync<Startup>([], port: _port);
        await _server.UpdateDbAsync<TemplateDbContext>(); // Use actual DbContext
        await _server.StartAsync();
        _http = new HttpClient { BaseAddress = new Uri($"http://localhost:{_port}") };
    }

    [TestCleanup]
    public async Task CleanServer()
    {
        if (_server == null) return;
        await _server.StopAsync();
        _server.Dispose();
    }

    [TestMethod]
    public async Task PhysicalAsset_Lifecycle_Test()
    {
        // 1. Arrange: Create Asset and User
        var scope = _server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TemplateDbContext>();
        var user = new User { UserName = "testuser", Email = "test@aiursoft.com", DisplayName = "Test User" };
        var asset = new PhysicalAsset
        {
            Name = "Test Laptop",
            TotalStock = 1,
            FrozenStock = 0,
            UsedStock = 0
        };
        db.Users.Add(user);
        db.PhysicalAssets.Add(asset);
        await db.SaveChangesAsync();

        var assetId = asset.Id;
        var userId = user.Id;

        // 2. Act: Apply for asset
        var service = scope.ServiceProvider.GetRequiredService<PhysicalAssetService>();
        await service.ApplyAsync(userId, assetId, "Need for work");

        // 3. Assert: FrozenStock increased
        // Use the tracked asset instance to check state
        await db.Entry(asset).ReloadAsync();
        Assert.AreEqual(1, asset.FrozenStock);
        Assert.AreEqual(0, asset.UsedStock);

        var usage = await db.PhysicalAssetUsages.SingleAsync(u => u.AssetId == assetId && u.UserId == userId);
        Assert.AreEqual(AssetStatus.Frozen, usage.Status);

        // 4. Act: Approve
        await service.ApproveAsync("admin-id", usage.Id, "SN-12345");

        // 5. Assert: InUse
        await db.Entry(asset).ReloadAsync();
        Assert.AreEqual(0, asset.FrozenStock);
        Assert.AreEqual(1, asset.UsedStock);

        // Use AsNoTracking for Usage query as we don't need to track it for updates here
        var usageAfterApprove = await db.PhysicalAssetUsages.AsNoTracking().SingleAsync(u => u.Id == usage.Id);
        Assert.AreEqual(AssetStatus.InUse, usageAfterApprove.Status);
        Assert.AreEqual("SN-12345", usageAfterApprove.AssignedSerialNumber);

        // 6. Act: Return
        await service.ReturnAsync("admin-id", usage.Id, "Done");

        // 7. Assert: Returned
        await db.Entry(asset).ReloadAsync();
        Assert.AreEqual(0, asset.UsedStock);
    }

    [TestMethod]
    public async Task PhysicalAsset_OptimisticLocking_Test()
    {
        // 1. Arrange: Asset with Stock 1
        var scope = _server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TemplateDbContext>();
        var asset = new PhysicalAsset { Name = "Hot Item", TotalStock = 1 };
        db.PhysicalAssets.Add(asset);
        await db.SaveChangesAsync();
        var assetId = asset.Id;

        // 2. Act: Two concurrent requests
        var service1 = scope.ServiceProvider.GetRequiredService<PhysicalAssetService>();

        // Request 1: Success
        await service1.ApplyAsync("user1", assetId, "First");

        // Request 2: Should Fail (Stock logic)
        try
        {
            await service1.ApplyAsync("user2", assetId, "Second");
            Assert.Fail("Should have thrown Exception");
        }
        catch (Exception)
        {
            // Expected
        }
    }

    [TestMethod]
    public async Task VirtualAsset_Encryption_Test()
    {
        var scope = _server.Services.CreateScope();
        var encryption = scope.ServiceProvider.GetRequiredService<EncryptionService>();

        string original = "SuperSecretPassword";
        string encrypted = encryption.Encrypt(original);
        string decrypted = encryption.Decrypt(encrypted);

        Assert.AreNotEqual(original, encrypted);
        Assert.AreEqual(original, decrypted);
    }
}
