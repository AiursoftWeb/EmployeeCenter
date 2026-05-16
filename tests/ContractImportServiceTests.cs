using System.IO.Compression;
using System.Text;
using Aiursoft.EmployeeCenter.InMemory;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.EmployeeCenter.Services.FileStorage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;

namespace Aiursoft.EmployeeCenter.Tests;

[TestClass]
public class ContractImportServiceTests
{
    private InMemoryContext _dbContext = null!;
    private StorageService _storageService = null!;
    private ContractImportService _importService = null!;
    private string _tempPath = null!;

    [TestInitialize]
    public async Task Initialize()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), "ContractImportTest_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempPath);

        // Setup in-memory SQLite database
        var options = new DbContextOptionsBuilder<InMemoryContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _dbContext = new InMemoryContext(options);
        await _dbContext.Database.OpenConnectionAsync();
        await _dbContext.Database.EnsureCreatedAsync();

        // Setup StorageService with temp directory
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Storage:Path", _tempPath }
            })
            .Build();
        var rootProvider = new StorageRootPathProvider(config);
        var foldersProvider = new FeatureFoldersProvider(rootProvider);
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var fileLockProvider = new FileLockProvider(memoryCache);
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        _storageService = new StorageService(foldersProvider, fileLockProvider, dataProtectionProvider);

        _importService = new ContractImportService(_dbContext, _storageService);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        await _dbContext.Database.CloseConnectionAsync();
        await _dbContext.DisposeAsync();
        if (Directory.Exists(_tempPath))
            Directory.Delete(_tempPath, true);
    }

    private static Stream CreateTestZip(Action<ZipArchive> populate)
    {
        var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            populate(archive);
        }
        ms.Position = 0;
        return ms;
    }

    private static void AddFileEntry(ZipArchive archive, string path, string content = "test content")
    {
        var entry = archive.CreateEntry(path);
        using var stream = entry.Open();
        stream.Write(Encoding.UTF8.GetBytes(content));
    }

    [TestMethod]
    public async Task ImportFromZipAsync_FlatFiles_CreatesContracts()
    {
        await using var zipStream = CreateTestZip(zip =>
        {
            AddFileEntry(zip, "doc1.pdf");
            AddFileEntry(zip, "doc2.pdf");
        });

        var result = await _importService.ImportFromZipAsync(zipStream, targetFolderId: null, isPublic: false, status: ContractStatus.Active);

        Assert.AreEqual(0, result.FoldersCreated);
        Assert.AreEqual(2, result.FilesImported);
        Assert.AreEqual(0, result.Errors.Count);

        var contracts = await _dbContext.Contracts.ToListAsync();
        Assert.AreEqual(2, contracts.Count);
        Assert.IsTrue(contracts.All(c => c.FolderId == null));
        Assert.IsTrue(contracts.All(c => c.Status == ContractStatus.Active));
        Assert.IsTrue(contracts.All(c => !c.IsPublic));
        Assert.IsTrue(contracts.All(c => c.FilePath.StartsWith("contract/")));
    }

    [TestMethod]
    public async Task ImportFromZipAsync_NestedFolders_CreatesHierarchy()
    {
        await using var zipStream = CreateTestZip(zip =>
        {
            AddFileEntry(zip, "A - Registers/Directors.pdf");
            AddFileEntry(zip, "A - Registers/Members.pdf");
            AddFileEntry(zip, "B - Certificates/Cert1.pdf");
        });

        var result = await _importService.ImportFromZipAsync(zipStream, targetFolderId: null, isPublic: true, status: ContractStatus.PendingSignature);

        Assert.AreEqual(2, result.FoldersCreated);
        Assert.AreEqual(3, result.FilesImported);
        Assert.AreEqual(0, result.Errors.Count);

        var folders = await _dbContext.ContractFolders.ToListAsync();
        Assert.AreEqual(2, folders.Count);
        Assert.IsTrue(folders.Any(f => f.Name == "A - Registers" && f.ParentFolderId == null));
        Assert.IsTrue(folders.Any(f => f.Name == "B - Certificates" && f.ParentFolderId == null));

        var contracts = await _dbContext.Contracts.Include(c => c.Folder).ToListAsync();
        Assert.AreEqual(3, contracts.Count);
        Assert.AreEqual(2, contracts.Count(c => c.Folder?.Name == "A - Registers"));
        Assert.AreEqual(1, contracts.Count(c => c.Folder?.Name == "B - Certificates"));
    }

    [TestMethod]
    public async Task ImportFromZipAsync_DeeplyNestedFolders_PreservesPath()
    {
        await using var zipStream = CreateTestZip(zip =>
        {
            AddFileEntry(zip, "Minutes/1 - Directors/Resolution1.pdf");
            AddFileEntry(zip, "Minutes/1 - Directors/Resolution2.pdf");
            AddFileEntry(zip, "Minutes/2 - Shareholders/Appointment.pdf");
        });

        var result = await _importService.ImportFromZipAsync(zipStream, targetFolderId: null, isPublic: false, status: ContractStatus.Active);

        Assert.AreEqual(3, result.FoldersCreated); // Minutes, 1 - Directors, 2 - Shareholders
        Assert.AreEqual(3, result.FilesImported);

        var folders = await _dbContext.ContractFolders.ToListAsync();
        var minutes = folders.FirstOrDefault(f => f.Name == "Minutes");
        Assert.IsNotNull(minutes);

        var directors = folders.FirstOrDefault(f => f.Name == "1 - Directors" && f.ParentFolderId == minutes.Id);
        Assert.IsNotNull(directors);

        var shareholders = folders.FirstOrDefault(f => f.Name == "2 - Shareholders" && f.ParentFolderId == minutes.Id);
        Assert.IsNotNull(shareholders);

        var contracts = await _dbContext.Contracts.Include(c => c.Folder).ToListAsync();
        Assert.AreEqual(2, contracts.Count(c => c.Folder?.Id == directors.Id));
        Assert.AreEqual(1, contracts.Count(c => c.Folder?.Id == shareholders.Id));
    }

    [TestMethod]
    public async Task ImportFromZipAsync_ExistingFolders_ReusesThem()
    {
        // Pre-create a folder
        var existing = new ContractFolder { Name = "Existing", ParentFolderId = null, CreateTime = DateTime.UtcNow };
        _dbContext.ContractFolders.Add(existing);
        await _dbContext.SaveChangesAsync();

        await using var zipStream = CreateTestZip(zip =>
        {
            AddFileEntry(zip, "Existing/file.pdf");
            AddFileEntry(zip, "New/file.pdf");
        });

        var result = await _importService.ImportFromZipAsync(zipStream, targetFolderId: null, isPublic: false, status: ContractStatus.Active);

        Assert.AreEqual(1, result.FoldersCreated); // Only "New"
        Assert.AreEqual(2, result.FilesImported);

        var folders = await _dbContext.ContractFolders.ToListAsync();
        Assert.AreEqual(2, folders.Count); // Existing + New, no duplicate
    }

    [TestMethod]
    public async Task ImportFromZipAsync_TargetFolder_ImportsUnderTarget()
    {
        var target = new ContractFolder { Name = "Target", ParentFolderId = null, CreateTime = DateTime.UtcNow };
        _dbContext.ContractFolders.Add(target);
        await _dbContext.SaveChangesAsync();

        await using var zipStream = CreateTestZip(zip =>
        {
            AddFileEntry(zip, "Sub/f1.pdf");
            AddFileEntry(zip, "f2.pdf");
        });

        var result = await _importService.ImportFromZipAsync(zipStream, targetFolderId: target.Id, isPublic: false, status: ContractStatus.Active);

        Assert.AreEqual(1, result.FoldersCreated); // Sub
        Assert.AreEqual(2, result.FilesImported);

        var folders = await _dbContext.ContractFolders.ToListAsync();
        var sub = folders.FirstOrDefault(f => f.Name == "Sub");
        Assert.IsNotNull(sub);
        Assert.AreEqual(target.Id, sub.ParentFolderId);

        var contracts = await _dbContext.Contracts.Include(c => c.Folder).ToListAsync();
        Assert.AreEqual(1, contracts.Count(c => c.Folder?.Id == sub.Id));
        Assert.AreEqual(1, contracts.Count(c => c.FolderId == null)); // f2 at root, but root = target folder in zip context
    }

    [TestMethod]
    public async Task ImportFromZipAsync_EmptyZip_NoErrors()
    {
        await using var zipStream = CreateTestZip(_ => { });

        var result = await _importService.ImportFromZipAsync(zipStream, targetFolderId: null, isPublic: false, status: ContractStatus.Active);

        Assert.AreEqual(0, result.FoldersCreated);
        Assert.AreEqual(0, result.FilesImported);
        Assert.AreEqual(0, result.Errors.Count);
    }

    [TestMethod]
    public async Task ImportFromZipAsync_ReturnsCorrectImportResult()
    {
        await using var zipStream = CreateTestZip(zip =>
        {
            AddFileEntry(zip, "A/file1.pdf");
            AddFileEntry(zip, "A/file2.pdf");
            AddFileEntry(zip, "B/file3.pdf");
            AddFileEntry(zip, "C/D/file4.pdf");
        });

        var result = await _importService.ImportFromZipAsync(zipStream, targetFolderId: null, isPublic: true, status: ContractStatus.Invalid);

        Assert.AreEqual(4, result.FoldersCreated); // A, B, C, C/D
        Assert.AreEqual(4, result.FilesImported);
        Assert.AreEqual(0, result.Errors.Count);

        var contracts = await _dbContext.Contracts.ToListAsync();
        Assert.IsTrue(contracts.All(c => c.IsPublic));
        Assert.IsTrue(contracts.All(c => c.Status == ContractStatus.Invalid));
    }

    [TestMethod]
    public async Task ImportFromZipAsync_FilesSavedToVault()
    {
        await using var zipStream = CreateTestZip(zip =>
        {
            AddFileEntry(zip, "mydoc.pdf", "PDF content here");
        });

        var result = await _importService.ImportFromZipAsync(zipStream, targetFolderId: null, isPublic: false, status: ContractStatus.Active);

        Assert.AreEqual(1, result.FilesImported);

        var contract = await _dbContext.Contracts.FirstAsync();
        // Verify file exists on disk in Vault
        var physicalPath = _storageService.GetFilePhysicalPath(contract.FilePath, isVault: true);
        Assert.IsTrue(File.Exists(physicalPath));
    }

    [TestMethod]
    public async Task ImportFromZipAsync_DuplicateFiles_HandlesRenaming()
    {
        // First import
        await using (var zip1 = CreateTestZip(zip => AddFileEntry(zip, "doc.pdf", "content v1")))
        {
            await _importService.ImportFromZipAsync(zip1, targetFolderId: null, isPublic: false, status: ContractStatus.Active);
        }

        // Second import of the same zip
        await using var zip2 = CreateTestZip(zip => AddFileEntry(zip, "doc.pdf", "content v2"));
        var result = await _importService.ImportFromZipAsync(zip2, targetFolderId: null, isPublic: false, status: ContractStatus.Active);

        Assert.AreEqual(1, result.FilesImported);
        Assert.AreEqual(0, result.Errors.Count);

        var contracts = await _dbContext.Contracts.OrderBy(c => c.CreateTime).ToListAsync();
        Assert.AreEqual(2, contracts.Count);
        Assert.AreNotEqual(contracts[0].FilePath, contracts[1].FilePath); // Renamed
    }

    [TestMethod]
    public async Task ImportFromZipAsync_LongFileName_TruncatesToMaxLength()
    {
        var longName = new string('A', 250) + ".pdf";

        await using var zipStream = CreateTestZip(zip => AddFileEntry(zip, longName));

        var result = await _importService.ImportFromZipAsync(zipStream, targetFolderId: null, isPublic: false, status: ContractStatus.Active);

        Assert.AreEqual(1, result.FilesImported);
        var contract = await _dbContext.Contracts.FirstAsync();
        Assert.IsTrue(contract.Name.Length <= 200);
    }

    [TestMethod]
    public async Task ImportFromZipAsync_ContractNameDerivedFromFileName()
    {
        await using var zipStream = CreateTestZip(zip =>
        {
            AddFileEntry(zip, "2025 - Employment Agreement - Signed.pdf");
        });

        await _importService.ImportFromZipAsync(zipStream, targetFolderId: null, isPublic: false, status: ContractStatus.Active);

        var contract = await _dbContext.Contracts.FirstAsync();
        Assert.AreEqual("2025 - Employment Agreement - Signed", contract.Name);
    }
}
