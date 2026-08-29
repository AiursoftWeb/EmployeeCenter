namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class ServersTests : TestBase
{
    [TestMethod]
    public async Task TestServersIndex()
    {
        await LoginAsAdmin();
        var response = await Http.GetAsync("/Servers/Index");
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        StringAssert.Contains(content, "Servers");
    }

    [TestMethod]
    public async Task TestAssociatedServiceCountLinksToFilteredServiceTable()
    {
        await LoginAsAdmin();
        var db = GetService<EmployeeCenterDbContext>();
        var server = new Server { Hostname = "associated-server", ServerIp = "192.0.2.20" };
        var otherServer = new Server { Hostname = "other-server", ServerIp = "192.0.2.21" };
        db.Servers.AddRange(server, otherServer);
        await db.SaveChangesAsync();

        db.Services.AddRange(
            new Service { Domain = "running.example.com", ServerId = server.Id },
            new Service { Domain = "frps.example.com", ServerId = otherServer.Id, IsViaFrps = true, FrpsServerId = server.Id },
            new Service { Domain = "both.example.com", ServerId = server.Id, IsViaFrps = true, FrpsServerId = server.Id },
            new Service { Domain = "unrelated.example.com", ServerId = otherServer.Id });
        await db.SaveChangesAsync();

        var indexResponse = await Http.GetAsync("/Servers/Index");
        indexResponse.EnsureSuccessStatusCode();
        var indexContent = await indexResponse.Content.ReadAsStringAsync();
        StringAssert.Matches(
            indexContent,
            new Regex($"href=\"[^\"]*serverId={server.Id}[^\"]*\"[^>]*>\\s*3\\s*</a>"));

        var filteredResponse = await Http.GetAsync($"/Services/List?serverId={server.Id}");
        filteredResponse.EnsureSuccessStatusCode();
        var filteredContent = await filteredResponse.Content.ReadAsStringAsync();
        StringAssert.Contains(filteredContent, "Services associated with associated-server");
        StringAssert.Contains(filteredContent, "running.example.com");
        StringAssert.Contains(filteredContent, "frps.example.com");
        StringAssert.Contains(filteredContent, "both.example.com");
        Assert.IsFalse(filteredContent.Contains("unrelated.example.com", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task TestServerCrud()
    {
        await LoginAsAdmin();

        // Create a company entity first
        var db = GetService<EmployeeCenterDbContext>();
        var company = new CompanyEntity
        {
            CompanyName = "Test Company",
            EntityCode = "TC123",
            BaseCurrency = "CNY"
        };
        db.CompanyEntities.Add(company);
        await db.SaveChangesAsync();

        // Create
        var response = await Http.GetAsync("/Servers/Create");
        response.EnsureSuccessStatusCode();

        var postResponse = await PostForm("/Servers/Create", new Dictionary<string, string>
        {
            { "Hostname", "test-server-01" },
            { "ServerIp", "192.168.1.100" },
            { "Ipv6Address", "2001:db8::100" },
            { "DetailLink", "https://example.com" },
            { "CompanyEntityId", company.Id.ToString() }
        });

        Assert.AreEqual(HttpStatusCode.Redirect, postResponse.StatusCode);

        db = GetService<EmployeeCenterDbContext>();
        db.ChangeTracker.Clear();
        var server = await db.Servers.FirstOrDefaultAsync(s => s.Hostname == "test-server-01");
        Assert.IsNotNull(server);
        Assert.AreEqual("192.168.1.100", server.ServerIp);
        Assert.AreEqual("2001:db8::100", server.Ipv6Address);
        Assert.AreEqual(company.Id, server.CompanyEntityId);

        // Edit
        var editResponse = await PostForm("/Servers/Edit", new Dictionary<string, string>
        {
            { "Id", server.Id.ToString() },
            { "Hostname", "test-server-01-updated" },
            { "ServerIp", "192.168.1.101" },
            { "Ipv6Address", "2001:db8::101" },
            { "CompanyEntityId", company.Id.ToString() }
        });

        Assert.AreEqual(HttpStatusCode.Redirect, editResponse.StatusCode);

        db = GetService<EmployeeCenterDbContext>();
        db.Entry(server).Reload();
        Assert.AreEqual("test-server-01-updated", server.Hostname);
        Assert.AreEqual("2001:db8::101", server.Ipv6Address);
        Assert.AreEqual(company.Id, server.CompanyEntityId);

        // Delete
        var deleteResponse = await PostForm($"/Servers/Delete/{server.Id}", new Dictionary<string, string>());
        Assert.AreEqual(HttpStatusCode.Redirect, deleteResponse.StatusCode);

        db = GetService<EmployeeCenterDbContext>();
        db.ChangeTracker.Clear();
        var deletedServer = await db.Servers.FindAsync(server.Id);
        Assert.IsNull(deletedServer);
    }
}
