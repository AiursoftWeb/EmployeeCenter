using Aiursoft.EmployeeCenter.InMemory;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.EmployeeCenter.Services.FileStorage;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aiursoft.EmployeeCenter.Tests;

[TestClass]
public class AudioUploadCleanupTests
{
    [TestMethod]
    public async Task ExpiredAbandonedUploadDeletesFileAndRecord()
    {
        await using var fixture = await CleanupFixture.CreateAsync(fileExists: true);

        var removed = await fixture.Service.CleanupAsync(DateTime.UtcNow);

        Assert.AreEqual(1, removed);
        Assert.IsFalse(File.Exists(fixture.PhysicalPath));
        Assert.IsFalse(await fixture.Db.AudioUploads.AnyAsync());
    }

    [TestMethod]
    public async Task ExpiredUploadReferencedByAudioIsPreserved()
    {
        await using var fixture = await CleanupFixture.CreateAsync(fileExists: true, referenced: true);

        var removed = await fixture.Service.CleanupAsync(DateTime.UtcNow);

        Assert.AreEqual(0, removed);
        Assert.IsTrue(File.Exists(fixture.PhysicalPath));
        Assert.IsTrue(await fixture.Db.AudioUploads.AnyAsync());
    }

    [TestMethod]
    public async Task RetryAfterFileDeletionRemovesExpiredRecord()
    {
        await using var fixture = await CleanupFixture.CreateAsync(fileExists: false);

        var removed = await fixture.Service.CleanupAsync(DateTime.UtcNow);

        Assert.AreEqual(1, removed);
        Assert.IsFalse(await fixture.Db.AudioUploads.AnyAsync());
    }

    [TestMethod]
    public async Task ConsumedUploadRecordIsRemovedWithoutDeletingReferencedFile()
    {
        await using var fixture = await CleanupFixture.CreateAsync(
            fileExists: true,
            referenced: true,
            consumed: true);

        var removed = await fixture.Service.CleanupAsync(DateTime.UtcNow);

        Assert.AreEqual(1, removed);
        Assert.IsTrue(File.Exists(fixture.PhysicalPath));
        Assert.IsFalse(await fixture.Db.AudioUploads.AnyAsync());
    }

    private sealed class CleanupFixture : IAsyncDisposable
    {
        private CleanupFixture(
            InMemoryContext db,
            AudioUploadCleanupService service,
            string storageRoot,
            string physicalPath)
        {
            Db = db;
            Service = service;
            StorageRoot = storageRoot;
            PhysicalPath = physicalPath;
        }

        public InMemoryContext Db { get; }
        public AudioUploadCleanupService Service { get; }
        public string StorageRoot { get; }
        public string PhysicalPath { get; }

        public static async Task<CleanupFixture> CreateAsync(
            bool fileExists,
            bool referenced = false,
            bool consumed = false)
        {
            var options = new DbContextOptionsBuilder<InMemoryContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var db = new InMemoryContext(options);
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
            var fileCleanup = new AudioFileCleanupService(
                db,
                storage,
                NullLogger<AudioFileCleanupService>.Instance);
            var service = new AudioUploadCleanupService(
                db,
                fileCleanup,
                NullLogger<AudioUploadCleanupService>.Instance);
            var uploadId = Guid.NewGuid();
            var filePath = $"audio/cleanup/{uploadId:N}.mp3";
            var physicalPath = storage.GetVaultSubfolderFilePhysicalPath(filePath, "audio");
            if (fileExists)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);
                await File.WriteAllBytesAsync(physicalPath, "audio"u8.ToArray());
            }
            db.AudioUploads.Add(new AudioUpload
            {
                Id = uploadId,
                OwnerId = "cleanup-owner",
                FilePath = filePath,
                ExpiresTime = DateTime.UtcNow.AddHours(-1),
                ConsumedTime = consumed ? DateTime.UtcNow.AddMinutes(-1) : null
            });
            if (referenced)
            {
                db.Audios.Add(new Audio
                {
                    Name = "Referenced upload",
                    FilePath = filePath
                });
            }
            await db.SaveChangesAsync();
            return new CleanupFixture(db, service, storageRoot, physicalPath);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            if (Directory.Exists(StorageRoot))
            {
                Directory.Delete(StorageRoot, recursive: true);
            }
        }
    }
}
