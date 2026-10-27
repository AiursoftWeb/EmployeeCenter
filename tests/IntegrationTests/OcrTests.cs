using Aiursoft.EmployeeCenter.Configuration;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.EmployeeCenter.Services.BackgroundJobs;
using Aiursoft.EmployeeCenter.Services.FileStorage;
using Microsoft.Extensions.Options;



namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class OcrTests : TestBase
{
    [TestMethod]
    public async Task TestOcrServiceSkipWhenNotConfigured()
    {
        // 1. Setup - Create a scope to resolve scoped services
        using var scope = Server!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<StorageService>();
        var ocrService = scope.ServiceProvider.GetRequiredService<OcrService>();
        
        var contract = new Contract
        {
            Name = "Test Contract",
            FilePath = "test-skip.pdf",
            Status = ContractStatus.Active,
            IsPublic = true
        };
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();

        var physicalPath = storage.GetFilePhysicalPath(contract.FilePath);
        var dir = Path.GetDirectoryName(physicalPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        await File.WriteAllTextAsync(physicalPath, "%PDF-1.4 dummy content");

        // 2. Act - Process (should skip because endpoint/token is empty in default test config)
        await ocrService.ProcessContractOcrAsync(contract.Id);
        
        // 3. Assert - No result should be saved
        var result = await ocrService.GetOcrResultByContractIdAsync(contract.Id);
        Assert.IsNull(result); 
    }

    [TestMethod]
    public async Task TestContractOcrJobPicksUpUnprocessed()
    {
        using var scope = Server!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
        var job = scope.ServiceProvider.GetRequiredService<ContractOcrJob>();
        
        // Add a contract
        var contract = new Contract
        {
            Name = "Unprocessed Contract",
            FilePath = "unprocessed.pdf",
            Status = ContractStatus.Active,
            IsPublic = true
        };
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();

        // Run the job
        await job.ExecuteAsync();
        
        // Since API is not configured, it should not have a result, 
        // but the job should finish without exception.
        var hasResult = await db.ContractOcrResults.AnyAsync(r => r.ContractId == contract.Id);
        Assert.IsFalse(hasResult);
    }

    [TestMethod]
    public async Task TestTransactionOcrJobPicksUpUnprocessed()
    {
        using var scope = Server!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
        var job = scope.ServiceProvider.GetRequiredService<TransactionOcrJob>();

        var company = new CompanyEntity
        {
            CompanyName = "Test Company",
            EntityCode = "123",
            LegalRepresentative = "Boss"
        };
        db.CompanyEntities.Add(company);
        await db.SaveChangesAsync();

        var account = new FinanceAccount
        {
            AccountName = "Test Account",
            Currency = "CNY",
            CompanyEntityId = company.Id
        };
        db.FinanceAccounts.Add(account);
        await db.SaveChangesAsync();

        // Add a transaction with attachments
        var transaction = new Transaction
        {
            Description = "Unprocessed Transaction",
            SourceAccountId = account.Id,
            DestinationAccountId = account.Id,
            Amount = 100,
            InvoicePath = "invoice.pdf",
            MT103Path = "mt103.pdf",
            PaymentVoucherPath = "voucher.pdf"
        };
        db.Transactions.Add(transaction);
        await db.SaveChangesAsync();

        // Run the job
        await job.ExecuteAsync();

        // Since API is not configured, it should not have results,
        // but the job should finish without exception.
        var hasResult = await db.TransactionOcrResults.AnyAsync(r => r.TransactionId == transaction.Id);
        Assert.IsFalse(hasResult);
    }

    [TestMethod]
    public async Task TestOcrServiceSkipsNonPdfForTransaction()
    {
        using var scope = Server!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<StorageService>();
        var ocrService = scope.ServiceProvider.GetRequiredService<OcrService>();

        var company = new CompanyEntity
        {
            CompanyName = "Test Company",
            EntityCode = "123",
            LegalRepresentative = "Boss"
        };
        db.CompanyEntities.Add(company);
        await db.SaveChangesAsync();

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
            Description = "Non-PDF Attachment",
            SourceAccountId = account.Id,
            DestinationAccountId = account.Id,
            Amount = 100,
            InvoicePath = "test.png" // Not a PDF
        };
        db.Transactions.Add(transaction);
        await db.SaveChangesAsync();

        var physicalPath = storage.GetFilePhysicalPath(transaction.InvoicePath, isVault: true);
        Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);
        await File.WriteAllTextAsync(physicalPath, "dummy image content");

        // Act
        await ocrService.ProcessTransactionOcrAsync(transaction.Id);

        // Assert - No result because it's not a PDF
        var hasResult = await db.TransactionOcrResults.AnyAsync(r => r.TransactionId == transaction.Id);
        Assert.IsFalse(hasResult);
    }

    [TestMethod]
    public async Task TestResetOcrResetsAttemptCountAndTriggersProcessing()
    {
        await LoginAsAdmin();

        using var scope = Server!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
        var ocrSettings = scope.ServiceProvider.GetRequiredService<IOptions<OcrSettings>>().Value;

        var maxRetry = ocrSettings.ContractOcrMaxRetryCount;
        var contract = new Contract
        {
            Name = "Retry Contract",
            FilePath = "retry.pdf",
            Status = ContractStatus.Active,
            IsPublic = true,
            OcrAttemptCount = maxRetry,
            LastOcrAttemptTime = DateTime.UtcNow.AddHours(-1)
        };
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();

        var response = await PostForm(
            $"/ManageContract/ResetOcr",
            new Dictionary<string, string> { { "id", contract.Id.ToString() } },
            tokenUrl: $"/ManageContract/OcrResults/{contract.Id}");

        AssertRedirect(response, $"/ManageContract/OcrResults/{contract.Id}");

        await db.Entry(contract).ReloadAsync();
        // After reset: count is 0. Then ProcessContractOcrAsync is called:
        // if OCR is enabled → count becomes 1; if disabled → stays 0.
        Assert.AreEqual(ocrSettings.Enabled ? 1 : 0, contract.OcrAttemptCount);
        if (ocrSettings.Enabled)
        {
            Assert.IsTrue(contract.LastOcrAttemptTime.HasValue);
            Assert.IsTrue(contract.LastOcrAttemptTime!.Value > DateTime.UtcNow.AddMinutes(-1));
        }
    }

    [TestMethod]
    public async Task TestContractOcrJobSkipsExhaustedContracts()
    {
        using var scope = Server!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
        var job = scope.ServiceProvider.GetRequiredService<ContractOcrJob>();
        var ocrSettings = scope.ServiceProvider.GetRequiredService<IOptions<OcrSettings>>().Value;

        var maxRetry = ocrSettings.ContractOcrMaxRetryCount;
        var contractA = new Contract
        {
            Name = "Below Max Retry",
            FilePath = "below-max.pdf",
            Status = ContractStatus.Active,
            IsPublic = true,
            OcrAttemptCount = 0
        };
        var contractB = new Contract
        {
            Name = "At Max Retry",
            FilePath = "at-max.pdf",
            Status = ContractStatus.Active,
            IsPublic = true,
            OcrAttemptCount = maxRetry
        };
        db.Contracts.AddRange(contractA, contractB);
        await db.SaveChangesAsync();

        await job.ExecuteAsync();

        await db.Entry(contractA).ReloadAsync();
        await db.Entry(contractB).ReloadAsync();

        if (ocrSettings.Enabled)
        {
            // Contract A processed: count incremented to 1
            Assert.AreEqual(1, contractA.OcrAttemptCount);
        }
        else
        {
            // OCR disabled: job returns early, nothing processed
            Assert.AreEqual(0, contractA.OcrAttemptCount);
        }
        // Contract B always skipped: at max retry or job didn't run
        Assert.AreEqual(maxRetry, contractB.OcrAttemptCount);
    }

    [TestMethod]
    public async Task TestOcrResultsPageShowsResetButtonWhenExhausted()
    {
        await LoginAsAdmin();

        using var scope = Server!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
        var ocrSettings = scope.ServiceProvider.GetRequiredService<IOptions<OcrSettings>>().Value;

        var contract = new Contract
        {
            Name = "Exhausted Contract",
            FilePath = "exhausted.pdf",
            Status = ContractStatus.Active,
            IsPublic = true,
            OcrAttemptCount = ocrSettings.ContractOcrMaxRetryCount
        };
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();

        var response = await Http.GetAsync($"/ManageContract/OcrResults/{contract.Id}");
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();

        StringAssert.Contains(html, "action=\"/ManageContract/ResetOcr\"");
        StringAssert.Contains(html, ">Reset");
    }

    [TestMethod]
    [DataRow("png")]
    [DataRow("JPG")]
    [DataRow("mp4")]
    public async Task UnsupportedFilesDoNotConsumeAttempts(string extension)
    {
        using var scope = Server!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
        var contract = new Contract { Name = "Unsupported", FilePath = $"file.{extension}" };
        var transaction = await CreateOcrTransactionAsync(db, $"invoice.{extension}");
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();

        await scope.ServiceProvider.GetRequiredService<ContractOcrJob>().ExecuteAsync();
        await scope.ServiceProvider.GetRequiredService<TransactionOcrJob>().ExecuteAsync();
        var service = scope.ServiceProvider.GetRequiredService<OcrService>();
        await service.ProcessContractOcrAsync(contract.Id);
        await service.ProcessTransactionOcrAsync(transaction.Id);

        await db.Entry(contract).ReloadAsync();
        await db.Entry(transaction).ReloadAsync();
        Assert.AreEqual(0, contract.OcrAttemptCount);
        Assert.AreEqual(0, transaction.OcrAttemptCount);
        Assert.IsNull(contract.LastOcrAttemptTime);
        Assert.IsNull(transaction.LastOcrAttemptTime);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("Recognized text")]
    public async Task SavedResultsAreTerminalForJobsAndServices(string text)
    {
        using var scope = Server!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
        var contract = new Contract { Name = "Completed", FilePath = "done.pdf", OcrAttemptCount = 1 };
        var transaction = await CreateOcrTransactionAsync(db, "done.pdf", voucher: "unsupported.png");
        transaction.OcrAttemptCount = 1;
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();
        db.ContractOcrResults.Add(new ContractOcrResult
        {
            ContractId = contract.Id, JsonResult = "{}", PlainText = text
        });
        db.TransactionOcrResults.Add(new TransactionOcrResult
        {
            TransactionId = transaction.Id, AttachmentType = TransactionAttachmentType.Invoice,
            JsonResult = "{}", PlainText = text
        });
        await db.SaveChangesAsync();

        await scope.ServiceProvider.GetRequiredService<ContractOcrJob>().ExecuteAsync();
        await scope.ServiceProvider.GetRequiredService<TransactionOcrJob>().ExecuteAsync();
        var service = scope.ServiceProvider.GetRequiredService<OcrService>();
        await service.ProcessContractOcrAsync(contract.Id);
        await service.ProcessTransactionOcrAsync(transaction.Id);

        await db.Entry(contract).ReloadAsync();
        await db.Entry(transaction).ReloadAsync();
        Assert.AreEqual(1, contract.OcrAttemptCount);
        Assert.AreEqual(1, transaction.OcrAttemptCount);
        Assert.IsNull(contract.LastOcrAttemptTime);
        Assert.IsNull(transaction.LastOcrAttemptTime);
        Assert.AreEqual(1, await db.ContractOcrResults.CountAsync(r => r.ContractId == contract.Id));
        Assert.AreEqual(1, await db.TransactionOcrResults.CountAsync(r => r.TransactionId == transaction.Id));
    }

    [TestMethod]
    public async Task UnsupportedFilesDoNotStarveLaterPdfs()
    {
        using var scope = Server!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
        var transaction = await CreateOcrTransactionAsync(db, "last.PDF");
        transaction.TransactionTime = DateTime.UtcNow.AddDays(-1);
        var contract = new Contract { Name = "Supported", FilePath = "last.PDF", CreateTime = DateTime.UtcNow.AddDays(-1) };
        db.Contracts.Add(contract);
        for (var i = 0; i < 51; i++)
        {
            db.Contracts.Add(new Contract { Name = "Unsupported", FilePath = $"file-{i}.png" });
            db.Transactions.Add(new Transaction
            {
                Description = "Unsupported", SourceAccountId = transaction.SourceAccountId,
                DestinationAccountId = transaction.DestinationAccountId, InvoicePath = $"file-{i}.png"
            });
        }
        await db.SaveChangesAsync();

        await scope.ServiceProvider.GetRequiredService<ContractOcrJob>().ExecuteAsync();
        await scope.ServiceProvider.GetRequiredService<TransactionOcrJob>().ExecuteAsync();

        await db.Entry(contract).ReloadAsync();
        await db.Entry(transaction).ReloadAsync();
        var enabled = scope.ServiceProvider.GetRequiredService<IOptions<OcrSettings>>().Value.Enabled;
        Assert.AreEqual(enabled ? 1 : 0, contract.OcrAttemptCount);
        Assert.AreEqual(enabled ? 1 : 0, transaction.OcrAttemptCount);
        Assert.AreEqual(0, await db.Contracts.CountAsync(c => c.FilePath.EndsWith(".png") && c.OcrAttemptCount > 0));
        Assert.AreEqual(0, await db.Transactions.CountAsync(t => t.InvoicePath!.EndsWith(".png") && t.OcrAttemptCount > 0));
    }

    [TestMethod]
    public async Task OnlyPendingPdfsAreRecognizedAndRetryLimitsRemainBounded()
    {
        using var scope = Server!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
        var transaction = await CreateOcrTransactionAsync(db, "completed.pdf", $"{Guid.NewGuid()}.PDF", "ignored.png");
        db.TransactionOcrResults.Add(new TransactionOcrResult
        {
            TransactionId = transaction.Id, AttachmentType = TransactionAttachmentType.Invoice,
            JsonResult = "{}", PlainText = "Preserved invoice"
        });
        await db.SaveChangesAsync();
        await WritePdfAsync(scope, transaction.MT103Path!);
        using var handler = new OcrHandler("New text");
        using var client = new HttpClient(handler);
        var service = CreateOcrService(scope, db, client);

        await service.ProcessTransactionOcrAsync(transaction.Id);
        await service.ProcessTransactionOcrAsync(transaction.Id);
        Assert.AreEqual(1, handler.RequestCount);
        Assert.AreEqual(1, transaction.OcrAttemptCount);
        Assert.AreEqual(2, await db.TransactionOcrResults.CountAsync(r => r.TransactionId == transaction.Id));
        Assert.AreEqual("Preserved invoice", (await db.TransactionOcrResults.SingleAsync(r => r.TransactionId == transaction.Id && r.AttachmentType == TransactionAttachmentType.Invoice)).PlainText);

        transaction.PaymentVoucherPath = "pending.pdf";
        transaction.OcrAttemptCount = 30;
        await db.SaveChangesAsync();
        await service.ProcessTransactionOcrAsync(transaction.Id);
        Assert.AreEqual(1, handler.RequestCount);
        Assert.AreEqual(30, transaction.OcrAttemptCount);
    }

    [TestMethod]
    public async Task EmptyPdfBecomesTerminalAtExistingEmptyRetryLimit()
    {
        using var scope = Server!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
        var transaction = await CreateOcrTransactionAsync(db, $"{Guid.NewGuid()}.pdf");
        await WritePdfAsync(scope, transaction.InvoicePath!);
        using var handler = new OcrHandler("");
        using var client = new HttpClient(handler);
        var service = CreateOcrService(scope, db, client, emptyLimit: 2);

        await service.ProcessTransactionOcrAsync(transaction.Id);
        Assert.AreEqual(0, await db.TransactionOcrResults.CountAsync(r => r.TransactionId == transaction.Id));
        await service.ProcessTransactionOcrAsync(transaction.Id);
        await service.ProcessTransactionOcrAsync(transaction.Id);
        await scope.ServiceProvider.GetRequiredService<TransactionOcrJob>().ExecuteAsync();

        await db.Entry(transaction).ReloadAsync();
        Assert.AreEqual(2, handler.RequestCount);
        Assert.AreEqual(2, transaction.OcrAttemptCount);
        Assert.AreEqual(2, transaction.EmptyResultCount);
        Assert.AreEqual("", (await db.TransactionOcrResults.SingleAsync(r => r.TransactionId == transaction.Id)).PlainText);
    }

    [TestMethod]
    [DataRow(0, false)]
    [DataRow(1, false)]
    [DataRow(2, false)]
    [DataRow(0, true)]
    [DataRow(1, true)]
    [DataRow(2, true)]
    [DataRow(-1, false)]
    public async Task EditingAttachmentsInvalidatesOnlyChangedResults(int changedType, bool remove)
    {
        await LoginAsAdmin();
        using var scope = Server!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
        var transaction = await CreateOcrTransactionAsync(db, "invoice.pdf", "mt103.pdf", "voucher.pdf");
        transaction.OcrAttemptCount = 30;
        transaction.EmptyResultCount = 10;
        var lastAttempt = DateTime.UtcNow.AddDays(-1);
        transaction.LastOcrAttemptTime = lastAttempt;
        foreach (var type in Enum.GetValues<TransactionAttachmentType>())
        {
            db.TransactionOcrResults.Add(new TransactionOcrResult
            {
                TransactionId = transaction.Id, AttachmentType = type,
                JsonResult = "{}", PlainText = $"Preserved {type}"
            });
        }
        await db.SaveChangesAsync();
        var originalResults = await db.TransactionOcrResults.AsNoTracking().Where(r => r.TransactionId == transaction.Id).ToListAsync();
        var paths = new[] { transaction.InvoicePath!, transaction.MT103Path!, transaction.PaymentVoucherPath! };
        if (changedType >= 0) paths[changedType] = remove ? "" : $"{Guid.NewGuid()}.PDF";
        var account = await db.FinanceAccounts.FindAsync(transaction.SourceAccountId);
        var response = await PostForm("/Ledger/EditTransaction", new Dictionary<string, string>
        {
            ["EntityId"] = account!.CompanyEntityId.ToString(),
            ["TransactionId"] = transaction.Id.ToString(),
            ["Description"] = "Edited description", ["SourceAccountId"] = transaction.SourceAccountId.ToString(),
            ["DestinationAccountId"] = transaction.DestinationAccountId.ToString(),
            ["Amount"] = "100", ["ExchangeRate"] = "1", ["TransactionTime"] = transaction.TransactionTime.ToString("O"),
            ["InvoicePath"] = paths[0], ["MT103Path"] = paths[1], ["PaymentVoucherPath"] = paths[2]
        }, tokenUrl: $"/Ledger/EditTransaction/{transaction.Id}");
        AssertRedirect(response, $"/Ledger/Dashboard/{account.CompanyEntityId}");
        await db.Entry(transaction).ReloadAsync();
        var remaining = await db.TransactionOcrResults.AsNoTracking().Where(r => r.TransactionId == transaction.Id).ToListAsync();
        Assert.AreEqual(changedType >= 0 ? 0 : 30, transaction.OcrAttemptCount);
        Assert.AreEqual(changedType >= 0 ? 0 : 10, transaction.EmptyResultCount);
        if (changedType >= 0) Assert.IsNull(transaction.LastOcrAttemptTime);
        else Assert.AreEqual(lastAttempt, transaction.LastOcrAttemptTime);
        CollectionAssert.AreEquivalent(originalResults.Where(r => (int)r.AttachmentType != changedType).Select(r => r.Id).ToArray(), remaining.Select(r => r.Id).ToArray());

        if (changedType >= 0 && !remove) await WritePdfAsync(scope, paths[changedType]);
        using var handler = new OcrHandler("Replacement text");
        using var client = new HttpClient(handler);
        await CreateOcrService(scope, db, client).ProcessTransactionOcrAsync(transaction.Id);
        Assert.AreEqual(changedType >= 0 && !remove ? 1 : 0, handler.RequestCount);
        if (changedType >= 0 && !remove)
        {
            Assert.AreEqual("Replacement text", (await db.TransactionOcrResults.AsNoTracking().SingleAsync(r => r.TransactionId == transaction.Id && (int)r.AttachmentType == changedType)).PlainText);
        }
    }

    private static async Task<Transaction> CreateOcrTransactionAsync(EmployeeCenterDbContext db,
        string invoice, string? mt103 = null, string? voucher = null)
    {
        var company = new CompanyEntity { CompanyName = "OCR test", EntityCode = "OCR", LegalRepresentative = "Test" };
        var account = new FinanceAccount { AccountName = "OCR account", Currency = "USD", CompanyEntity = company };
        db.FinanceAccounts.Add(account);
        await db.SaveChangesAsync();
        var transaction = new Transaction
        {
            Description = "OCR test", SourceAccountId = account.Id, DestinationAccountId = account.Id,
            Amount = 100, InvoicePath = invoice, MT103Path = mt103, PaymentVoucherPath = voucher
        };
        db.Transactions.Add(transaction);
        await db.SaveChangesAsync();
        return transaction;
    }

    private static OcrService CreateOcrService(IServiceScope scope, EmployeeCenterDbContext db,
        HttpClient client, int emptyLimit = 10) => new(client, Options.Create(new OcrSettings
        {
            Endpoint = "http://ocr.test/api/ocr/pdf", BearerToken = "test-only",
            TransactionOcrMaxEmptyRetryCount = emptyLimit
        }), db, scope.ServiceProvider.GetRequiredService<StorageService>(),
        Microsoft.Extensions.Logging.Abstractions.NullLogger<OcrService>.Instance);

    private static async Task WritePdfAsync(IServiceScope scope, string path)
    {
        var storage = scope.ServiceProvider.GetRequiredService<StorageService>();
        var physicalPath = storage.GetFilePhysicalPath(path, isVault: true);
        Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);
        await File.WriteAllTextAsync(physicalPath, "%PDF-1.4 test content");
    }

    private sealed class OcrHandler(string text) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { status = "ok", results = new[] { new { text } } })
            });
        }
    }
}
