using Aiursoft.EmployeeCenter.Services;
using Aiursoft.EmployeeCenter.Services.BackgroundJobs;
using Aiursoft.EmployeeCenter.Services.FileStorage;
using Aiursoft.EmployeeCenter.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aiursoft.EmployeeCenter.Tests;

[TestClass]
public class AudioFileDeletionTests
{
    [TestMethod]
    public async Task FailedInitialUploadCanBeDeletedWhileRecordIsRetained()
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
        const string filePath = "audio/failed-upload.mp4";
        var physicalPath = storage.GetVaultSubfolderFilePhysicalPath(filePath, "audio");
        Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);
        await File.WriteAllBytesAsync(physicalPath, "video"u8.ToArray());
        db.Audios.Add(new Audio
        {
            Name = "Failed upload",
            FilePath = filePath,
            MediaStatus = AudioMediaStatus.Failed
        });
        db.AudioFileDeletions.Add(new AudioFileDeletion { FilePath = filePath });
        await db.SaveChangesAsync();
        var service = new AudioFileCleanupService(db, storage, NullLogger<AudioFileCleanupService>.Instance);

        try
        {
            Assert.AreEqual(1, await service.CleanupQueuedAsync());
            Assert.IsFalse(File.Exists(physicalPath));
            Assert.IsTrue(await db.Audios.AnyAsync(audio => audio.FilePath == filePath));
        }
        finally
        {
            Directory.Delete(storageRoot, recursive: true);
        }
    }

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

            var deletion = await db.AudioFileDeletions.SingleAsync();
            deletion.NextAttemptTime = DateTime.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();
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

    [TestMethod]
    public async Task CleanupJobRetriesDueDeletion()
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
        db.AudioFileDeletions.Add(new AudioFileDeletion
        {
            FilePath = filePath,
            NextAttemptTime = DateTime.UtcNow.AddSeconds(-1)
        });
        await db.SaveChangesAsync();
        var service = new AudioFileCleanupService(db, storage, NullLogger<AudioFileCleanupService>.Instance);
        var job = new AudioFileCleanupJob(service, NullLogger<AudioFileCleanupJob>.Instance);

        try
        {
            await job.ExecuteAsync();

            Assert.IsFalse(File.Exists(physicalPath));
            Assert.AreEqual(0, await db.AudioFileDeletions.CountAsync());
        }
        finally
        {
            Directory.Delete(storageRoot, recursive: true);
        }
    }

    [TestMethod]
    public async Task PermanentFailuresDoNotStarveLaterDeletion()
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
        const string deletablePath = "audio/deletion/deletable.mp3";
        var physicalPath = storage.GetVaultSubfolderFilePhysicalPath(deletablePath, "audio");
        Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);
        await File.WriteAllBytesAsync(physicalPath, "audio"u8.ToArray());
        var createdTime = DateTime.UtcNow.AddHours(-1);
        var failingDeletions = Enumerable.Range(0, 100).Select(index => new AudioFileDeletion
        {
            FilePath = $"audio/deletion/failing-{index}.mp3",
            CreatedTime = createdTime.AddSeconds(index),
            NextAttemptTime = createdTime.AddSeconds(index)
        }).ToList();
        foreach (var deletion in failingDeletions)
        {
            var failingPhysicalPath = storage.GetVaultSubfolderFilePhysicalPath(deletion.FilePath, "audio");
            await File.WriteAllBytesAsync(failingPhysicalPath, "audio"u8.ToArray());
        }
        db.AudioFileDeletions.AddRange(failingDeletions);
        db.AudioFileDeletions.Add(new AudioFileDeletion
        {
            FilePath = deletablePath,
            CreatedTime = createdTime.AddSeconds(100),
            NextAttemptTime = createdTime.AddSeconds(100)
        });
        await db.SaveChangesAsync();
        var service = new FailsSelectedAudioFileCleanupService(db, storage);

        try
        {
            Assert.AreEqual(0, await service.CleanupQueuedAsync());
            Assert.AreEqual(1, await service.CleanupQueuedAsync());

            Assert.IsFalse(File.Exists(physicalPath));
            Assert.IsFalse(await db.AudioFileDeletions.AnyAsync(deletion => deletion.FilePath == deletablePath));
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

    private sealed class FailsSelectedAudioFileCleanupService(
        EmployeeCenterDbContext context,
        StorageService storageService)
        : AudioFileCleanupService(
            context,
            storageService,
            NullLogger<AudioFileCleanupService>.Instance)
    {
        protected override void DeleteFile(string physicalPath)
        {
            if (Path.GetFileName(physicalPath).StartsWith("failing-", StringComparison.Ordinal))
            {
                throw new IOException("Permanent deletion failure.");
            }
            base.DeleteFile(physicalPath);
        }
    }
}
