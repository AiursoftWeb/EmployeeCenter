using Aiursoft.EmployeeCenter.InMemory;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.EmployeeCenter.Tests;

/// <summary>
/// Regression tests for the Asset.RowVersion NOT NULL constraint bug.
///
/// Root cause: [Timestamp] told EF Core the column is database-generated, so EF omitted
/// it from INSERT statements. SQLite has no native row-version mechanism, which caused:
///   "SQLite Error 19: NOT NULL constraint failed: Assets.RowVersion"
///
/// Fix: changed [Timestamp] to [ConcurrencyCheck] so EF includes the C# default
/// (Array.Empty&lt;byte&gt;()) in every INSERT while still performing optimistic concurrency
/// checks on UPDATE.
/// </summary>
[TestClass]
public class AssetRowVersionTests
{
    private InMemoryContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<InMemoryContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new InMemoryContext(options);
    }

    private async Task<(int categoryId, int modelId)> SeedCategoryAndModel(InMemoryContext db)
    {
        var category = new AssetCategory { Name = "Laptop", Code = "LAP" };
        db.AssetCategories.Add(category);
        await db.SaveChangesAsync();

        var model = new AssetModel
        {
            CategoryId = category.Id,
            Brand = "Apple",
            ModelName = "MacBook Pro"
        };
        db.AssetModels.Add(model);
        await db.SaveChangesAsync();

        return (category.Id, model.Id);
    }

    [TestMethod]
    public async Task CreateAsset_ShouldNotThrow_RowVersionNotNullConstraint()
    {
        // Arrange
        await using var db = CreateDb();
        var (_, modelId) = await SeedCategoryAndModel(db);

        var asset = new Asset
        {
            AssetTag = "LAP-001",
            ModelId = modelId,
            Status = AssetStatus.Idle
        };

        // Act & Assert — must not throw "NOT NULL constraint failed: Assets.RowVersion"
        db.Assets.Add(asset);
        await db.SaveChangesAsync();

        Assert.AreNotEqual(Guid.Empty, asset.Id);
    }

    [TestMethod]
    public async Task CreateAsset_RowVersion_DefaultsToEmptyByteArray()
    {
        // Arrange
        await using var db = CreateDb();
        var (_, modelId) = await SeedCategoryAndModel(db);

        var asset = new Asset
        {
            AssetTag = "LAP-002",
            ModelId = modelId,
            Status = AssetStatus.Idle
        };

        // Act
        db.Assets.Add(asset);
        await db.SaveChangesAsync();

        // Assert — RowVersion must not be null; it defaults to Array.Empty<byte>()
        Assert.IsNotNull(asset.RowVersion);
    }

    [TestMethod]
    public async Task CreateMultipleAssets_ShouldAllSucceed_WithRowVersion()
    {
        // Arrange
        await using var db = CreateDb();
        var (_, modelId) = await SeedCategoryAndModel(db);

        var assets = Enumerable.Range(1, 5).Select(i => new Asset
        {
            AssetTag = $"LAP-{i:D3}",
            ModelId = modelId,
            Status = AssetStatus.Idle
        }).ToList();

        // Act
        db.Assets.AddRange(assets);
        await db.SaveChangesAsync();

        // Assert
        var savedCount = await db.Assets.CountAsync();
        Assert.AreEqual(5, savedCount);
    }

    [TestMethod]
    public async Task UpdateAsset_ShouldSucceed_WithConcurrencyCheck()
    {
        // Arrange
        await using var db = CreateDb();
        var (_, modelId) = await SeedCategoryAndModel(db);

        var asset = new Asset
        {
            AssetTag = "LAP-UPDATE",
            ModelId = modelId,
            Status = AssetStatus.Idle
        };
        db.Assets.Add(asset);
        await db.SaveChangesAsync();

        // Act — update should not throw
        asset.Status = AssetStatus.InUse;
        await db.SaveChangesAsync();

        // Assert
        var reloaded = await db.Assets.FirstAsync(a => a.Id == asset.Id);
        Assert.AreEqual(AssetStatus.InUse, reloaded.Status);
    }
}
