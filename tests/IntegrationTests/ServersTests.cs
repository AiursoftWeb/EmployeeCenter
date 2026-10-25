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
            new Service { PrimaryDomain = "running.example.com", ServerId = server.Id },
            new Service { PrimaryDomain = "frps.example.com", ServerId = otherServer.Id, IsViaFrps = true, FrpsServerId = server.Id },
            new Service { PrimaryDomain = "both.example.com", ServerId = server.Id, IsViaFrps = true, FrpsServerId = server.Id },
            new Service { PrimaryDomain = "unrelated.example.com", ServerId = otherServer.Id });
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
            { "ConcurrencyToken", server.ConcurrencyToken! },
            { "CompanyEntityId", company.Id.ToString() }
        });

        Assert.AreEqual(HttpStatusCode.Redirect, editResponse.StatusCode);

        db = GetService<EmployeeCenterDbContext>();
        db.Entry(server).Reload();
        Assert.AreEqual("test-server-01-updated", server.Hostname);
        Assert.AreEqual("2001:db8::101", server.Ipv6Address);
        Assert.AreEqual(company.Id, server.CompanyEntityId);

        // Delete
        var deleteResponse = await PostForm($"/Servers/Delete/{server.Id}", new Dictionary<string, string>
        {
            ["concurrencyToken"] = server.ConcurrencyToken!
        });
        Assert.AreEqual(HttpStatusCode.Redirect, deleteResponse.StatusCode);

        db = GetService<EmployeeCenterDbContext>();
        db.ChangeTracker.Clear();
        var retiredServer = await db.Servers.FindAsync(server.Id);
        Assert.IsNotNull(retiredServer);
        Assert.IsNotNull(retiredServer.RetiredAt);
    }

    [TestMethod]
    public async Task ActiveServicePreventsServerRetirement()
    {
        await LoginAsAdmin();
        var db = GetService<EmployeeCenterDbContext>();
        var server = new Server
        {
            Hostname = "in-use-server",
            ConcurrencyToken = Guid.NewGuid().ToString()
        };
        db.Services.Add(new Service
        {
            Name = "Active service",
            PrimaryDomain = "active-on-server.example.com",
            Server = server
        });
        await db.SaveChangesAsync();

        var response = await PostForm($"/Servers/Delete/{server.Id}", new Dictionary<string, string>
        {
            ["concurrencyToken"] = server.ConcurrencyToken
        });

        Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
        db.ChangeTracker.Clear();
        Assert.IsNull((await db.Servers.FindAsync(server.Id))?.RetiredAt);
    }
    [TestMethod]
    public async Task NotesRoundTripAndChangesAreAudited()
    {
        await LoginAsAdmin();
        const string original = "# Purpose\nShared FRP entry.\n\n## Maintenance\n- IPv6 enabled.";
        var create = await PostForm("/Servers/Create", new Dictionary<string, string>
        {
            ["Hostname"] = "notes-server",
            ["NotesMarkdown"] = original
        });
        Assert.AreEqual(HttpStatusCode.Found, create.StatusCode);
        var db = GetService<EmployeeCenterDbContext>();
        db.ChangeTracker.Clear();
        var server = await db.Servers.SingleAsync(item => item.Hostname == "notes-server");
        Assert.AreEqual(original, server.NotesMarkdown);
        var editPage = await Http.GetStringAsync($"/Servers/Edit/{server.Id}");
        Assert.Contains("Shared FRP entry.", editPage);
        Assert.Contains("server-notes-editor", editPage);
        const string updated = "## Maintenance\nIPv6 and registration verified. <script>alert('unsafe')</script>";
        var edit = await PostForm("/Servers/Edit", new Dictionary<string, string>
        {
            ["Id"] = server.Id.ToString(),
            ["Hostname"] = server.Hostname!,
            ["ConcurrencyToken"] = server.ConcurrencyToken!,
            ["NotesMarkdown"] = updated
        });
        Assert.AreEqual(HttpStatusCode.Found, edit.StatusCode);
        db.ChangeTracker.Clear();
        Assert.AreEqual(updated, (await db.Servers.FindAsync(server.Id))!.NotesMarkdown);
        var changes = await db.InfrastructureChangeLogs.Where(item => item.ResourceType == nameof(Server) && item.ResourceId == server.Id).ToListAsync();
        Assert.AreEqual(2, changes.Count);
        var update = changes.Single(item => item.Action == "Updated");
        using var before = System.Text.Json.JsonDocument.Parse(update.BeforeJson!);
        using var after = System.Text.Json.JsonDocument.Parse(update.AfterJson!);
        Assert.AreEqual(original, before.RootElement.GetProperty("notesMarkdown").GetString());
        Assert.AreEqual(updated, after.RootElement.GetProperty("notesMarkdown").GetString());
        var detail = await Http.GetStringAsync($"/Servers/Details/{server.Id}");
        Assert.Contains("IPv6 and registration verified.", detail);
        Assert.DoesNotContain("<script>alert('unsafe')</script>", detail);
        var index = await Http.GetStringAsync("/Servers/Index");
        Assert.Contains($"/Servers/Details/{server.Id}", index);
    }

    [TestMethod]
    public async Task NotesCannotOverwriteAConcurrentEdit()
    {
        await LoginAsAdmin();
        var db = GetService<EmployeeCenterDbContext>();
        var server = new Server { Hostname = "notes-concurrency", NotesMarkdown = "Before", ConcurrencyToken = Guid.NewGuid().ToString() };
        db.Servers.Add(server);
        await db.SaveChangesAsync();
        var staleToken = server.ConcurrencyToken;
        server.NotesMarkdown = "Another engineer's update";
        server.ConcurrencyToken = Guid.NewGuid().ToString();
        await db.SaveChangesAsync();
        var result = await PostForm("/Servers/Edit", new Dictionary<string, string>
        {
            ["Id"] = server.Id.ToString(), ["Hostname"] = server.Hostname,
            ["ConcurrencyToken"] = staleToken, ["NotesMarkdown"] = "Stale overwrite"
        });
        result.EnsureSuccessStatusCode();
        Assert.Contains("changed by another user", await result.Content.ReadAsStringAsync());
        db.ChangeTracker.Clear();
        Assert.AreEqual("Another engineer's update", (await db.Servers.FindAsync(server.Id))!.NotesMarkdown);
    }

    [TestMethod]
    public async Task NotesRespectLengthAndAntiforgeryValidation()
    {
        await LoginAsAdmin();
        var rejected = await PostForm("/Servers/Create", new Dictionary<string, string>
        {
            ["Hostname"] = "notes-too-long", ["NotesMarkdown"] = new string('x', 50001)
        });
        rejected.EnsureSuccessStatusCode();
        Assert.IsFalse(await GetService<EmployeeCenterDbContext>().Servers.AnyAsync(item => item.Hostname == "notes-too-long"));
        var noToken = await PostForm("/Servers/Create", new Dictionary<string, string>
        {
            ["Hostname"] = "notes-no-csrf", ["NotesMarkdown"] = "Must not save"
        }, includeToken: false);
        Assert.AreEqual(HttpStatusCode.BadRequest, noToken.StatusCode);
    }

    [TestMethod]
    public async Task NotesCanBeReadForEmptyAndRetiredServers()
    {
        await LoginAsAdmin();
        var db = GetService<EmployeeCenterDbContext>();
        var server = new Server { Hostname = "retired-notes", RetiredAt = DateTime.UtcNow };
        db.Servers.Add(server);
        await db.SaveChangesAsync();
        var html = await Http.GetStringAsync($"/Servers/Details/{server.Id}");
        Assert.Contains("No server notes yet.", html);
        Assert.DoesNotContain($"/Servers/Edit/{server.Id}", html);
        Assert.AreEqual(HttpStatusCode.NotFound, (await Http.GetAsync("/Servers/Details/2147483647")).StatusCode);
    }

}
