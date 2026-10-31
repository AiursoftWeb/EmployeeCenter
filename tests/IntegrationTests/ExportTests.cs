using System.Text;
using Aiursoft.EmployeeCenter.Configuration;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.EmployeeCenter.Services.FileStorage;
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
    [DataRow("")]
    [DataRow("a")]
    [DataRow("账")]
    [DataRow("😀")]
    public async Task TestExportLogic(string repeatedCharacter)
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

        var audio = new Audio
        {
            Name = "Test Meeting",
            FilePath = "audio/test-meeting.mp3"
        };
        var emptyAudio = new Audio
        {
            Name = "Empty Meeting",
            FilePath = "audio/empty-meeting.mp3"
        };
        db.Audios.AddRange(audio, emptyAudio);
        await db.SaveChangesAsync();
        db.AudioAsrResults.AddRange(
            new AudioAsrResult
            {
                AudioId = audio.Id,
                PlainText = "Meeting transcript content",
                MeetingMinutesMarkdown = "# Meeting minutes\n\n- Decision"
            },
            new AudioAsrResult
            {
                AudioId = emptyAudio.Id,
                PlainText = string.Empty
            });

        const string cloudflareSecret = "cloudflare-secret-must-never-be-exported";
        var cloudflareSetting = await db.GlobalSettings.FindAsync(SettingsMap.CloudflareApiToken);
        Assert.IsNotNull(cloudflareSetting);
        cloudflareSetting.Value = cloudflareSecret;
        const string retiredGitHubToken = "retired-github-token-must-never-be-exported";
        var retiredSetting = await db.GlobalSettings.FindAsync("GitLab_GitHub_Token");
        if (retiredSetting == null)
        {
            retiredSetting = new GlobalSetting { Key = "GitLab_GitHub_Token" };
            db.GlobalSettings.Add(retiredSetting);
        }
        retiredSetting.Value = retiredGitHubToken;

        // 4. Setup Company Entity
        var company = new CompanyEntity
        {
            CompanyName = "Test Company " + Guid.NewGuid(),
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
            Description = string.IsNullOrEmpty(repeatedCharacter)
                ? "Test Transaction"
                : string.Concat(Enumerable.Repeat(repeatedCharacter, 300)),
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

        var secondTransaction = new Transaction
        {
            Description = transaction.Description + " second",
            SourceAccountId = account.Id,
            DestinationAccountId = account.Id,
            Amount = 200,
            InvoicePath = "second-invoice.pdf",
            TransactionTime = transaction.TransactionTime
        };
        db.Transactions.Add(secondTransaction);
        await db.SaveChangesAsync();
        var secondInvoicePath = storageService.GetFilePhysicalPath(secondTransaction.InvoicePath, isVault: true);
        Directory.CreateDirectory(Path.GetDirectoryName(secondInvoicePath)!);
        await File.WriteAllTextAsync(secondInvoicePath, "Second Invoice Content");

        // Mock the physical file for the transaction invoice
        var txInvoicePhysicalPath = storageService.GetFilePhysicalPath(transaction.InvoicePath, isVault: true);
        Directory.CreateDirectory(Path.GetDirectoryName(txInvoicePhysicalPath)!);
        await File.WriteAllTextAsync(txInvoicePhysicalPath, "Fake Invoice PDF Content");

        // A public workspace file with the same logical name must never replace the private invoice.
        var workspaceInvoicePath = storageService.GetFilePhysicalPath(transaction.InvoicePath);
        Directory.CreateDirectory(Path.GetDirectoryName(workspaceInvoicePath)!);
        await File.WriteAllTextAsync(workspaceInvoicePath, "Wrong workspace invoice content");

        // Mock the physical file for the contract
        var physicalPath = storageService.GetFilePhysicalPath(contract.FilePath, isVault: true);
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
            scope.ServiceProvider.GetRequiredService<ILogger<ExportService>>());
        await exportService.ExportAsync();

        // 6. Verify results
        // Verify Ledger
        var ledgerPath = Path.Combine(_testExportPath, "Ledger", company.CompanyName, "Test Account");
        var attachmentDirectories = Directory.GetDirectories(Path.Combine(ledgerPath, "Attachments"));
        Assert.HasCount(2, attachmentDirectories);
        var txDir = attachmentDirectories.Single(d => Path.GetFileName(d).EndsWith($"_{transaction.Id}"));
        var secondTxDir = attachmentDirectories.Single(d => Path.GetFileName(d).EndsWith($"_{secondTransaction.Id}"));
        foreach (var directory in attachmentDirectories)
        {
            var name = Path.GetFileName(directory);
            Assert.IsTrue(Encoding.UTF8.GetByteCount(name) <= 255);
            Assert.IsFalse(name.Contains('\uFFFD'), "Directory names must preserve complete Unicode characters.");
        }
        if (string.IsNullOrEmpty(repeatedCharacter))
        {
            Assert.AreEqual($"2023-10-15_Test Transaction_{transaction.Id}", Path.GetFileName(txDir));
        }
        Assert.AreEqual("Fake Invoice PDF Content", await File.ReadAllTextAsync(Path.Combine(txDir, "Invoice.pdf")));
        Assert.AreEqual("Invoice OCR Content", await File.ReadAllTextAsync(Path.Combine(txDir, "Invoice.md")));
        Assert.AreEqual("Second Invoice Content", await File.ReadAllTextAsync(Path.Combine(secondTxDir, "Invoice.pdf")));
        var ledgerMarkdown = await File.ReadAllTextAsync(Path.Combine(ledgerPath, "Transactions.md"));
        StringAssert.Contains(ledgerMarkdown, transaction.Description);
        StringAssert.Contains(ledgerMarkdown, secondTransaction.Description);

        // GitLab repositories are no longer exported by Employee Center.
        Assert.IsFalse(Directory.Exists(Path.Combine(_testExportPath, "GitProjects")));

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

        var meetingTranscriptFile = Path.Combine(_testExportPath, "MeetingTranscripts", $"Test Meeting_{audio.Id}.md");
        var meetingMinutesFile = Path.Combine(_testExportPath, "MeetingTranscripts", $"Test Meeting_{audio.Id}_minutes.md");
        var emptyMeetingTranscriptFile = Path.Combine(_testExportPath, "MeetingTranscripts", $"Empty Meeting_{emptyAudio.Id}.md");
        var emptyMeetingMinutesFile = Path.Combine(_testExportPath, "MeetingTranscripts", $"Empty Meeting_{emptyAudio.Id}_minutes.md");
        Assert.IsTrue(File.Exists(meetingTranscriptFile), $"Meeting transcript file not found at {meetingTranscriptFile}");
        Assert.AreEqual("Meeting transcript content", await File.ReadAllTextAsync(meetingTranscriptFile));
        Assert.IsTrue(File.Exists(meetingMinutesFile), $"Meeting minutes file not found at {meetingMinutesFile}");
        Assert.AreEqual("# Meeting minutes\n\n- Decision", await File.ReadAllTextAsync(meetingMinutesFile));
        Assert.IsFalse(File.Exists(emptyMeetingTranscriptFile), $"Empty meeting transcript should not be exported at {emptyMeetingTranscriptFile}");
        Assert.IsFalse(File.Exists(emptyMeetingMinutesFile), $"Missing meeting minutes should not be exported at {emptyMeetingMinutesFile}");

        // Verify Company Entities
        var companyFile = Path.Combine(_testExportPath, "CompanyEntities", company.CompanyName + ".md");
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
        Assert.DoesNotContain(cloudflareSecret, settingsContent);
        Assert.DoesNotContain(retiredGitHubToken, settingsContent);
        Assert.DoesNotContain("GitLab_GitHub_Token", settingsContent);
        Assert.Contains("[REDACTED]", settingsContent);

        // Existing GitLab backups must survive subsequent non-GitLab exports.
        var legacyGitProjectsDir = Path.Combine(_testExportPath, "GitProjects");
        Directory.CreateDirectory(legacyGitProjectsDir);
        var legacyFile = Path.Combine(legacyGitProjectsDir, "existing-backup.txt");
        await File.WriteAllTextAsync(legacyFile, "keep me");
        await exportService.ExportAsync();
        Assert.AreEqual("keep me", await File.ReadAllTextAsync(legacyFile));
    }
}
