using Aiursoft.EmployeeCenter.Configuration;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.EmployeeCenter.Services.FileStorage;
using Aiursoft.EmployeeCenter.Services.GitLab;
using Microsoft.Extensions.Options;

namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class ExportTests : TestBase
{
    private string _testExportPath = null!;
    private string _testStoragePath = null!;

    [TestInitialize]
    public override async Task CreateServer()
    {
        await base.CreateServer();
        _testExportPath = Path.Combine(Path.GetTempPath(), "EC_Export_Test_" + Guid.NewGuid());
        _testStoragePath = Path.Combine(Path.GetTempPath(), "EC_Storage_Test_" + Guid.NewGuid());
        
        Directory.CreateDirectory(_testExportPath);
        Directory.CreateDirectory(_testStoragePath);
        Directory.CreateDirectory(Path.Combine(_testStoragePath, "Workspace"));
    }

    [TestCleanup]
    public override async Task CleanServer()
    {
        await base.CleanServer();
        if (Directory.Exists(_testExportPath)) Directory.Delete(_testExportPath, true);
        if (Directory.Exists(_testStoragePath)) Directory.Delete(_testStoragePath, true);
    }

    [TestMethod]
    public async Task TestExportLogic()
    {
        using var scope = Server!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
        var storageService = scope.ServiceProvider.GetRequiredService<StorageService>();

        // 1. Setup Blueprints with folder structure
        var bFolder1 = new BlueprintFolder { Name = "Level1" };
        db.BlueprintFolders.Add(bFolder1);
        await db.SaveChangesAsync();

        var bFolder2 = new BlueprintFolder { Name = "Level2", ParentFolderId = bFolder1.Id };
        db.BlueprintFolders.Add(bFolder2);
        await db.SaveChangesAsync();

        var blueprint = new Blueprint
        {
            Title = "Test Blueprint",
            Content = "# Hello World",
            AuthorId = (await db.Users.FirstAsync()).Id,
            FolderId = bFolder2.Id
        };
        db.Blueprints.Add(blueprint);

        // 2. Setup Contracts with folder structure and OCR
        var cFolder = new ContractFolder { Name = "ContractDir" };
        db.ContractFolders.Add(cFolder);
        await db.SaveChangesAsync();

        var contract = new Contract
        {
            Name = "Test Contract",
            FilePath = "test-contract.pdf",
            FolderId = cFolder.Id
        };
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();

        var ocrResult = new ContractOcrResult
        {
            ContractId = contract.Id,
            JsonResult = "{}",
            PlainText = "OCR Content"
        };
        db.ContractOcrResults.Add(ocrResult);

        // 3. Setup Weekly Reports
        var user = await db.Users.FirstAsync();
        var report = new WeeklyReport
        {
            UserId = user.Id,
            Content = "Weekly Content",
            WeekStartDate = new DateTime(2023, 10, 1)
        };
        db.WeeklyReports.Add(report);

        // 4. Setup Company Entity
        var company = new CompanyEntity
        {
            CompanyName = "Test Company",
            EntityCode = "123456",
            LegalRepresentative = "Test Boss",
            CreateLedger = true
        };
        db.CompanyEntities.Add(company);
        await db.SaveChangesAsync();

        // 4.1 Setup Ledger
        var account = new FinanceAccount
        {
            AccountName = "Test Account",
            Currency = "CNY",
            CompanyEntityId = company.Id
        };
        db.FinanceAccounts.Add(account);
        await db.SaveChangesAsync();

        var transaction = new Transaction
        {
            Description = "Test Transaction",
            SourceAccountId = account.Id,
            DestinationAccountId = account.Id,
            Amount = 100,
            InvoicePath = "test-invoice.pdf",
            TransactionTime = new DateTime(2023, 10, 15)
        };
        db.Transactions.Add(transaction);
        await db.SaveChangesAsync();

        var txOcrResult = new TransactionOcrResult
        {
            TransactionId = transaction.Id,
            AttachmentType = TransactionAttachmentType.Invoice,
            JsonResult = "{}",
            PlainText = "Invoice OCR Content"
        };
        db.TransactionOcrResults.Add(txOcrResult);
        await db.SaveChangesAsync();

        // Mock the physical file for the transaction invoice
        var txInvoicePhysicalPath = storageService.GetFilePhysicalPath(transaction.InvoicePath);
        Directory.CreateDirectory(Path.GetDirectoryName(txInvoicePhysicalPath)!);
        await File.WriteAllTextAsync(txInvoicePhysicalPath, "Fake Invoice PDF Content");

        // Mock the physical file for the contract
        var physicalPath = storageService.GetFilePhysicalPath(contract.FilePath);
        Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);
        await File.WriteAllTextAsync(physicalPath, "Fake PDF Content");

        // 5. Run Export
        var options = Options.Create(new AppSettings
        {
            ExportPath = _testExportPath,
            AuthProvider = "Local",
            Local = new LocalSettings
            {
                AllowRegister = true,
                AllowWeakPassword = true
            },
            OIDC = new OidcSettings
            {
                Authority = "https://auth.aiursoft.com",
                ClientId = "test",
                ClientSecret = "test",
                RolePropertyName = "groups",
                UsernamePropertyName = "username",
                UserDisplayNamePropertyName = "name",
                EmailPropertyName = "email",
                UserIdentityPropertyName = "sub"
            },
            OCR = new OcrSettings { Enabled = false },
            Agent = new AgentSettings { Endpoint = "http://localhost:8000/ask" }
        });

        var exportService = new ExportService(
            db,
            options,
            storageService,
            scope.ServiceProvider.GetRequiredService<GitLabService>(),
            scope.ServiceProvider.GetRequiredService<ILogger<ExportService>>());
        await exportService.ExportAsync();

        // 6. Verify results
        // Verify Ledger
        var txDirName = $"2023-10-15_Test Transaction_{transaction.Id}";
        var txAttachmentPdf = Path.Combine(_testExportPath, "Ledger", "Test Company", "Test Account", "Attachments", txDirName, "Invoice.pdf");
        var txAttachmentMd = Path.Combine(_testExportPath, "Ledger", "Test Company", "Test Account", "Attachments", txDirName, "Invoice.md");

        Assert.IsTrue(File.Exists(txAttachmentPdf), $"Transaction attachment PDF not found at {txAttachmentPdf}");
        Assert.IsTrue(File.Exists(txAttachmentMd), $"Transaction attachment OCR MD not found at {txAttachmentMd}");
        Assert.AreEqual("Invoice OCR Content", await File.ReadAllTextAsync(txAttachmentMd));

        // Verify Git Projects
        var gitProjectsDir = Path.Combine(_testExportPath, "GitProjects");
        Assert.IsTrue(Directory.Exists(gitProjectsDir), $"GitProjects directory not found at {gitProjectsDir}");

        // Verify Blueprints
        var blueprintFile = Path.Combine(_testExportPath, "Blueprints", "Level1", "Level2", "Test Blueprint.md");
        Assert.IsTrue(File.Exists(blueprintFile), $"Blueprint file not found at {blueprintFile}");
        Assert.AreEqual("# Hello World", await File.ReadAllTextAsync(blueprintFile));

        // Verify Contracts
        var contractPdf = Path.Combine(_testExportPath, "Contracts", "ContractDir", "Test Contract.pdf");
        var contractMd = Path.Combine(_testExportPath, "Contracts", "ContractDir", "Test Contract.md");
        
        Assert.IsTrue(File.Exists(contractPdf), $"Contract PDF not found at {contractPdf}");
        Assert.IsTrue(File.Exists(contractMd), $"Contract OCR MD not found at {contractMd}");
        Assert.AreEqual("OCR Content", await File.ReadAllTextAsync(contractMd));

        // Verify Weekly Reports
        var reportFile = Path.Combine(_testExportPath, "WeeklyReports", "2023-10-01", $"{user.DisplayName}.md");
        Assert.IsTrue(File.Exists(reportFile), $"Weekly report file not found at {reportFile}");
        Assert.AreEqual("Weekly Content", await File.ReadAllTextAsync(reportFile));

        // Verify Company Entities
        var companyFile = Path.Combine(_testExportPath, "CompanyEntities", "Test Company.md");
        Assert.IsTrue(File.Exists(companyFile), $"Company entity file not found at {companyFile}");
        var companyContent = await File.ReadAllTextAsync(companyFile);
        Assert.IsTrue(companyContent.Contains("Test Company"));
        Assert.IsTrue(companyContent.Contains("123456"));
        Assert.IsTrue(companyContent.Contains("Test Boss"));

        // Verify Global Settings
        var settingsFile = Path.Combine(_testExportPath, "GlobalSettings", "settings.md");
        Assert.IsTrue(File.Exists(settingsFile), $"Global settings file not found at {settingsFile}");
        var settingsContent = await File.ReadAllTextAsync(settingsFile);
        Assert.IsTrue(settingsContent.Contains("ProjectName"));
    }
}