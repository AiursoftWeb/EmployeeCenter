namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class CollectionChannelsTests
{
    private readonly int _port;
    private readonly HttpClient _http;
    private IHost? _server;

    public CollectionChannelsTests()
    {
        var cookieContainer = new CookieContainer();
        var handler = new HttpClientHandler
        {
            CookieContainer = cookieContainer,
            AllowAutoRedirect = false
        };
        _port = Network.GetAvailablePort();
        _http = new HttpClient(handler)
        {
            BaseAddress = new Uri($"http://localhost:{_port}")
        };
    }

    [TestInitialize]
    public async Task CreateServer()
    {
        _server = await AppAsync<Startup>([], port: _port);
        using (var scope = _server.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            await context.Database.EnsureCreatedAsync();
            await _server.SeedAsync();
        }
        await _server.StartAsync();
    }

    [TestCleanup]
    public async Task CleanServer()
    {
        if (_server == null) return;
        await _server.StopAsync();
        _server.Dispose();
    }

    private async Task<string> GetAntiCsrfToken(string url)
    {
        var response = await _http.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(html, @"__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""");
        if (!match.Success)
        {
            throw new InvalidOperationException($"Could not find anti-CSRF token on page: {url}");
        }

        return match.Groups[1].Value;
    }

    [TestMethod]
    public async Task CollectionChannelLifecycleTest()
    {
        // 1. Login as admin
        var loginToken = await GetAntiCsrfToken("/Account/Login");
        var loginContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "EmailOrUserName", "admin" },
            { "Password", "Admin@123456!" },
            { "__RequestVerificationToken", loginToken }
        });
        await _http.PostAsync("/Account/Login", loginContent);

        // 2. Prepare Data (Company Entities and Contract)
        int payerId, payeeId, contractId;
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var payer = new CompanyEntity { CompanyName = "Payer Co", EntityCode = "P001" };
            var payee = new CompanyEntity { CompanyName = "Payee Co", EntityCode = "E001" };
            var contract = new Contract { Name = "Service Contract", FilePath = "contracts/test.pdf", Status = ContractStatus.Active };
            
            db.CompanyEntities.AddRange(payer, payee);
            db.Contracts.Add(contract);
            await db.SaveChangesAsync();
            
            payerId = payer.Id;
            payeeId = payee.Id;
            contractId = contract.Id;
        }

        // 3. Create Collection Channel
        var createToken = await GetAntiCsrfToken("/CollectionChannels/Create");
        var createContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "PayerId", payerId.ToString() },
            { "PayeeId", payeeId.ToString() },
            { "ContractId", contractId.ToString() },
            { "ReferenceAmount", "1000.50" },
            { "Currency", "USD" },
            { "PaymentMethod", "Bank Transfer" },
            { "StartBillingDate", "2024-01-01" },
            { "FirstPaymentDate", "2024-02-01" },
            { "IsRecurring", "true" },
            { "RecurringPeriod", "Month" },
            { "__RequestVerificationToken", createToken }
        });
        var createResponse = await _http.PostAsync("/CollectionChannels/Create", createContent);
        Assert.AreEqual(HttpStatusCode.Redirect, createResponse.StatusCode);

        int channelId;
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var channel = await db.CollectionChannels.FirstOrDefaultAsync(c => c.PayerId == payerId);
            Assert.IsNotNull(channel);
            Assert.AreEqual(100050, channel.ReferenceAmount);
            Assert.AreEqual("USD", channel.Currency);
            channelId = channel.Id;
        }

        // 4. Create Collection Record
        var recordCreateToken = await GetAntiCsrfToken($"/CollectionRecords/Create?channelId={channelId}");
        var recordCreateContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "ChannelId", channelId.ToString() },
            { "ExpectedAmount", "1000.50" },
            { "ActualAmount", "0" },
            { "DueDate", "2024-02-01" },
            { "Remark", "Phase 1 payment" },
            { "Status", "Pending" },
            { "__RequestVerificationToken", recordCreateToken }
        });
        var recordCreateResponse = await _http.PostAsync("/CollectionRecords/Create", recordCreateContent);
        Assert.AreEqual(HttpStatusCode.Redirect, recordCreateResponse.StatusCode);

        int recordId;
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var record = await db.CollectionRecords.FirstOrDefaultAsync(r => r.ChannelId == channelId);
            Assert.IsNotNull(record);
            Assert.AreEqual(CollectionRecordStatus.Pending, record.Status);
            Assert.AreEqual("Phase 1 payment", record.Remark);
            recordId = record.Id;
        }

        // 5. Update Collection Record
        var recordEditToken = await GetAntiCsrfToken($"/CollectionRecords/Edit/{recordId}");
        var recordEditContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Id", recordId.ToString() },
            { "ChannelId", channelId.ToString() },
            { "ExpectedAmount", "1000.50" },
            { "ActualAmount", "1000.50" },
            { "DueDate", "2024-02-01" },
            { "PaidDate", "2024-02-05" },
            { "Remark", "Phase 1 payment - updated" },
            { "Status", "Settled" },
            { "TransactionId", "TX12345" },
            { "__RequestVerificationToken", recordEditToken }
        });
        var recordEditResponse = await _http.PostAsync("/CollectionRecords/Edit", recordEditContent);
        Assert.AreEqual(HttpStatusCode.Redirect, recordEditResponse.StatusCode);

        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var record = await db.CollectionRecords.FindAsync(recordId);
            Assert.AreEqual(CollectionRecordStatus.Settled, record!.Status);
            Assert.AreEqual("TX12345", record.TransactionId);
            Assert.AreEqual("Phase 1 payment - updated", record.Remark);
        }

        // 6. Terminate Channel
        var terminateToken = await GetAntiCsrfToken($"/CollectionChannels/Edit/{channelId}");
        var terminateContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "id", channelId.ToString() },
            { "__RequestVerificationToken", terminateToken }
        });
        var terminateResponse = await _http.PostAsync("/CollectionChannels/Terminate", terminateContent);
        Assert.AreEqual(HttpStatusCode.Redirect, terminateResponse.StatusCode);

        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var channel = await db.CollectionChannels.FindAsync(channelId);
            Assert.AreEqual(CollectionChannelStatus.Terminated, channel!.Status);
        }
    }
}
