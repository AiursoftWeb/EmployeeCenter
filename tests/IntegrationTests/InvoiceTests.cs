namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class InvoiceTests : TestBase
{
    [TestMethod]
    public async Task TestInvoiceFlow()
    {
        // 1. Setup - Create entities
        int sellerId, buyerId;
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var seller = new CompanyEntity
            {
                CompanyName = "Seller Entity",
                CompanyNameEnglish = "Seller Ltd",
                EntityCode = "SE001",
                OfficeAddress = "Seller Address",
                BankName = "Seller Bank",
                BankAccount = "123456",
                BankAccountName = "Seller Ltd",
                CreateLedger = true
            };
            var buyer = new CompanyEntity
            {
                CompanyName = "Buyer Entity",
                EntityCode = "BE001",
                OfficeAddress = "Buyer Address",
                CreateLedger = true
            };
            db.CompanyEntities.AddRange(seller, buyer);
            await db.SaveChangesAsync();
            sellerId = seller.Id;
            buyerId = buyer.Id;
        }

        // 2. Login as Admin
        await LoginAsAdmin();

        // 3. View Index
        var indexResponse = await Http.GetAsync("/Invoice/Index");
        indexResponse.EnsureSuccessStatusCode();
        var indexContent = await indexResponse.Content.ReadAsStringAsync();
        Assert.Contains("Seller Entity", indexContent, "Index should contain seller entity name.");
        Assert.Contains("Buyer Entity", indexContent, "Index should contain buyer entity name.");

        // 4. View Create
        var createResponse = await Http.GetAsync($"/Invoice/Create?sellerId={sellerId}&buyerId={buyerId}");
        createResponse.EnsureSuccessStatusCode();
        var createContent = await createResponse.Content.ReadAsStringAsync();
        Assert.Contains("Seller Ltd", createContent, "Create page should contain English seller name.");
        Assert.Contains("Buyer Entity", createContent, "Create page should contain buyer name.");
        Assert.Contains("Seller Address", createContent, "Create page should contain seller address.");
        Assert.Contains("Buyer Address", createContent, "Create page should contain buyer address.");

        // 5. Submit Preview
        var previewResponse = await PostForm("/Invoice/Preview", new Dictionary<string, string>
        {
            { "SellerId", sellerId.ToString() },
            { "SellerName", "Seller Ltd" },
            { "SellerAddress", "Seller Address" },
            { "SellerCode", "SE001" },
            { "BuyerId", buyerId.ToString() },
            { "BuyerName", "Buyer Entity" },
            { "BuyerAddress", "Buyer Address" },
            { "InvoiceNo", "INV-TEST-01" },
            { "Date", DateTime.Today.ToString("yyyy-MM-dd") },
            { "Currency", "HKD" },
            { "BankName", "Seller Bank" },
            { "BankAccount", "123456" },
            { "BeneficiaryName", "Seller Ltd" },
            { "Items[0].Description", "Test Item" },
            { "Items[0].Quantity", "1" },
            { "Items[0].UnitPrice", "1000" }
        }, tokenUrl: $"/Invoice/Create?sellerId={sellerId}&buyerId={buyerId}");

        previewResponse.EnsureSuccessStatusCode();
        var previewContent = await previewResponse.Content.ReadAsStringAsync();
        Assert.Contains("RECEIPT", previewContent, "Preview should contain the title RECEIPT.");
        Assert.Contains("INV-TEST-01", previewContent, "Preview should contain the invoice number.");
        Assert.Contains("Test Item", previewContent, "Preview should contain the item description.");
        Assert.Contains("1,000.00", previewContent, "Preview should contain the formatted unit price.");

        // 6. Submit Proforma Preview
        var proformaPreviewResponse = await PostForm("/Invoice/Preview", new Dictionary<string, string>
        {
            { "SellerId", sellerId.ToString() },
            { "SellerName", "Seller Ltd" },
            { "SellerAddress", "Seller Address" },
            { "SellerCode", "SE001" },
            { "BuyerId", buyerId.ToString() },
            { "BuyerName", "Buyer Entity" },
            { "BuyerAddress", "Buyer Address" },
            { "InvoiceNo", "PRO-TEST-01" },
            { "Type", "1" }, // InvoiceType.Proforma
            { "Date", DateTime.Today.ToString("yyyy-MM-dd") },
            { "Currency", "HKD" },
            { "BankName", "Seller Bank" },
            { "BankAccount", "123456" },
            { "BeneficiaryName", "Seller Ltd" },
            { "Items[0].Description", "Service Subscription" },
            { "Items[0].Quantity", "1" },
            { "Items[0].UnitPrice", "2000" }
        }, tokenUrl: $"/Invoice/Create?sellerId={sellerId}&buyerId={buyerId}");

        proformaPreviewResponse.EnsureSuccessStatusCode();
        var proformaPreviewContent = await proformaPreviewResponse.Content.ReadAsStringAsync();
        Assert.Contains("PROFORMA INVOICE", proformaPreviewContent, "Preview should contain the title PROFORMA INVOICE.");
        Assert.Contains("PRO-TEST-01", proformaPreviewContent, "Preview should contain the invoice number.");
        Assert.Contains("Service Subscription", proformaPreviewContent, "Preview should contain the item description.");
        Assert.Contains("2,000.00", proformaPreviewContent, "Preview should contain the formatted unit price.");
    }
}
