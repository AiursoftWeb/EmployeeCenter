namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class ContractFinanceTests : TestBase
{
    [TestMethod]
    public async Task TestContractFinanceDetails()
    {
        await LoginAsAdmin();

        // 1. Setup - Create a contract, companies, and collection channels with records
        using var scope = Server!.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();

        var internalCompany = new CompanyEntity
        {
            CompanyName = "Internal Co",
            EntityCode = "INTERNAL001",
            CreateLedger = true
        };
        var externalCompany = new CompanyEntity
        {
            CompanyName = "External Co",
            EntityCode = "EXTERNAL001",
            CreateLedger = false
        };
        context.CompanyEntities.AddRange(internalCompany, externalCompany);
        await context.SaveChangesAsync();

        var contract = new Contract
        {
            Name = "Test Finance Contract",
            FilePath = "test.pdf",
            Status = ContractStatus.Active,
            CreateTime = DateTime.UtcNow
        };
        context.Contracts.Add(contract);
        await context.SaveChangesAsync();

        // Income channel: External pays Internal
        var incomeChannel = new CollectionChannel
        {
            ContractId = contract.Id,
            PayerId = externalCompany.Id,
            PayeeId = internalCompany.Id,
            ReferenceAmount = 100000, // 1000.00
            Currency = "CNY",
            PaymentMethod = "Bank",
            Status = CollectionChannelStatus.Active
        };
        context.CollectionChannels.Add(incomeChannel);
        await context.SaveChangesAsync();

        var record1 = new CollectionRecord
        {
            ChannelId = incomeChannel.Id,
            ExpectedAmount = 50000,
            ActualAmount = 50000,
            Status = CollectionRecordStatus.Settled,
            DueDate = DateTime.UtcNow
        };
        var record2 = new CollectionRecord
        {
            ChannelId = incomeChannel.Id,
            ExpectedAmount = 50000,
            ActualAmount = 0,
            Status = CollectionRecordStatus.Pending,
            DueDate = DateTime.UtcNow.AddMonths(1)
        };
        context.CollectionRecords.AddRange(record1, record2);
        await context.SaveChangesAsync();

        // 2. Test Finance Details Action
        var response = await Http.GetAsync($"/ManageContract/Finance/{contract.Id}");
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        
        Assert.IsTrue(content.Contains("1,000.00")); // Total expected income (500 + 500)
        Assert.IsTrue(content.Contains("500.00")); // Total actual income / pending income
    }

    [TestMethod]
    public async Task TestContractFinanceStats()
    {
        await LoginAsAdmin();

        // 1. Setup - Use the existing data or add more
        using var scope = Server!.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();

        var internalCompany = new CompanyEntity
        {
            CompanyName = "Internal Co 2",
            EntityCode = "INTERNAL002",
            CreateLedger = true
        };
        context.CompanyEntities.Add(internalCompany);
        await context.SaveChangesAsync();

        var contract = new Contract
        {
            Name = "Stats Contract",
            FilePath = "stats.pdf",
            Status = ContractStatus.Active,
            CreateTime = DateTime.UtcNow
        };
        context.Contracts.Add(contract);
        await context.SaveChangesAsync();

        var channel = new CollectionChannel
        {
            ContractId = contract.Id,
            PayerId = internalCompany.Id,
            PayeeId = internalCompany.Id, // Internal transfer, counts as both in my implementation
            ReferenceAmount = 200000,
            Currency = "USD",
            PaymentMethod = "Wire",
            Status = CollectionChannelStatus.Active
        };
        context.CollectionChannels.Add(channel);
        await context.SaveChangesAsync();

        // 2. Test Finance Stats Action
        var response = await Http.GetAsync("/ManageContract/FinanceStats");
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();

        Assert.IsTrue(content.Contains("Stats Contract"));
        Assert.IsTrue(content.Contains("USD"));
    }
}
