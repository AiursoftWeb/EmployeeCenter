using Aiursoft.EmployeeCenter.Services;
using Aiursoft.EmployeeCenter.Services.FileStorage;
using Aiursoft.EmployeeCenter.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aiursoft.EmployeeCenter.Tests;

[TestClass]
public class AudioFileDeletionTests
{
    [TestMethod]
    public async Task FailedDeletionRemainsQueuedAndIsRetried()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SqliteContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new SqliteContext(options);
        await db.Database.EnsureCreatedAsync();

        var storageRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:Path"] = storageRoot
            })
            .Build();
        var storage = new StorageService(
            new FeatureFoldersProvider(new StorageRootPathProvider(configuration)),
            null!,
            null!);
        var filePath = $"audio/deletion/{Guid.NewGuid():N}.mp3";
        var physicalPath = storage.GetVaultSubfolderFilePhysicalPath(filePath, "audio");
        Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);
        await File.WriteAllBytesAsync(physicalPath, "audio"u8.ToArray());
        var service = new FailsOnceAudioFileCleanupService(db, storage);

        try
        {
            service.QueueDeletion(filePath);
            await db.SaveChangesAsync();

            var firstRemoved = await service.CleanupQueuedAsync();

            Assert.AreEqual(0, firstRemoved);
            Assert.IsTrue(File.Exists(physicalPath));
            Assert.AreEqual(1, await db.AudioFileDeletions.CountAsync());

            var secondRemoved = await service.CleanupQueuedAsync();

            Assert.AreEqual(1, secondRemoved);
            Assert.IsFalse(File.Exists(physicalPath));
            Assert.AreEqual(0, await db.AudioFileDeletions.CountAsync());
        }
        finally
        {
            Directory.Delete(storageRoot, recursive: true);
        }
    }

    private sealed class FailsOnceAudioFileCleanupService(
        EmployeeCenterDbContext context,
        StorageService storageService)
        : AudioFileCleanupService(
            context,
            storageService,
            NullLogger<AudioFileCleanupService>.Instance)
    {
        private bool _failed;

        protected override void DeleteFile(string physicalPath)
        {
            if (!_failed)
            {
                _failed = true;
                throw new IOException("Temporary deletion failure.");
            }
            base.DeleteFile(physicalPath);
        }
    }
}
