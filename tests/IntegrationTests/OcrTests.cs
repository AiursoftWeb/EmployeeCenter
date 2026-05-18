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
        // Login as admin to access ManageContract
        await LoginAsAdmin();

        using var scope = Server!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();

        var contract = new Contract
        {
            Name = "Retry Contract",
            FilePath = "retry.pdf",
            Status = ContractStatus.Active,
            IsPublic = true,
            OcrAttemptCount = 5,
            LastOcrAttemptTime = DateTime.UtcNow.AddHours(-1)
        };
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();

        // POST to ResetOcr
        var response = await PostForm(
            $"/ManageContract/ResetOcr",
            new Dictionary<string, string> { { "id", contract.Id.ToString() } },
            tokenUrl: $"/ManageContract/OcrResults/{contract.Id}");

        // Should redirect back to OcrResults
        AssertRedirect(response, $"/ManageContract/OcrResults/{contract.Id}");

        // Verify OcrAttemptCount was reset and then incremented by ProcessContractOcrAsync
        await db.Entry(contract).ReloadAsync();
        Assert.AreEqual(1, contract.OcrAttemptCount);
        Assert.IsTrue(contract.LastOcrAttemptTime.HasValue);
        Assert.IsTrue(contract.LastOcrAttemptTime!.Value > DateTime.UtcNow.AddMinutes(-1));
    }

    [TestMethod]
    public async Task TestContractOcrJobSkipsExhaustedContracts()
    {
        using var scope = Server!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
        var job = scope.ServiceProvider.GetRequiredService<ContractOcrJob>();
        var ocrSettings = scope.ServiceProvider.GetRequiredService<IOptions<OcrSettings>>().Value;

        // Contract A: below max retry — should be processed
        var contractA = new Contract
        {
            Name = "Below Max Retry",
            FilePath = "below-max.pdf",
            Status = ContractStatus.Active,
            IsPublic = true,
            OcrAttemptCount = 0
        };
        // Contract B: at max retry — should be skipped
        var contractB = new Contract
        {
            Name = "At Max Retry",
            FilePath = "at-max.pdf",
            Status = ContractStatus.Active,
            IsPublic = true,
            OcrAttemptCount = ocrSettings.ContractOcrMaxRetryCount
        };
        db.Contracts.AddRange(contractA, contractB);
        await db.SaveChangesAsync();

        await job.ExecuteAsync();

        // Contract A was processed: count incremented
        await db.Entry(contractA).ReloadAsync();
        Assert.AreEqual(1, contractA.OcrAttemptCount);

        // Contract B was skipped: count unchanged
        await db.Entry(contractB).ReloadAsync();
        Assert.AreEqual(ocrSettings.ContractOcrMaxRetryCount, contractB.OcrAttemptCount);
    }

    [TestMethod]
    public async Task TestOcrResultsPageShowsResetButtonWhenExhausted()
    {
        await LoginAsAdmin();

        using var scope = Server!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();

        var contract = new Contract
        {
            Name = "Exhausted Contract",
            FilePath = "exhausted.pdf",
            Status = ContractStatus.Active,
            IsPublic = true,
            OcrAttemptCount = 5
        };
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();

        var response = await Http.GetAsync($"/ManageContract/OcrResults/{contract.Id}");
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();

        // The reset form should be present
        StringAssert.Contains(html, "action=\"/ManageContract/ResetOcr\"");
        StringAssert.Contains(html, ">Reset");
    }
}
