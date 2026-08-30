namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class ServiceTests : TestBase
{
    [TestMethod]
    public async Task DnsAuditDashboardLinkRequiresAuditPermission()
    {
        await RegisterAndLoginAsync();

        var response = await Http.GetAsync("/Services/Index");

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("/ServiceAudit", content, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public async Task TestServiceIndex()
    {
        await LoginAsAdmin();

        var db = GetService<EmployeeCenterDbContext>();
        var location = new Location { Name = "Frankfurt" };
        var dnsProvider = new DnsProvider { Name = "Cloudflare" };
        var server = new Server
        {
            Hostname = "dashboard-server",
            ServerIp = "192.0.2.30",
            Location = location
        };
        db.Services.Add(new Service
        {
            Domain = "dashboard.example.com",
            Server = server,
            DnsProvider = dnsProvider,
            Status = ServiceStatus.Running,
            IsCloudflareProxied = true
        });
        await db.SaveChangesAsync();

        var response = await Http.GetAsync("/Services/Index");
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        StringAssert.Contains(content, "Services Dashboard");
        StringAssert.Contains(content, "service-dashboard-data");
        StringAssert.Contains(content, "dashboard-server");
        StringAssert.Contains(content, "Frankfurt");
        StringAssert.Contains(content, "Cloudflare");
        StringAssert.Contains(content, "/DomainAliases");
        StringAssert.Contains(content, "/ServiceAudit");

        var listResponse = await Http.GetAsync("/Services/List");
        listResponse.EnsureSuccessStatusCode();
        var listContent = await listResponse.Content.ReadAsStringAsync();
        StringAssert.Contains(listContent, "dashboard.example.com");
    }

    [TestMethod]
    public async Task TestServiceCreate()
    {
        await LoginAsAdmin();

        var response = await Http.GetAsync("/Services/Create");
        response.EnsureSuccessStatusCode();

        var postResponse = await PostForm("/Services/Create", new Dictionary<string, string>
        {
            { "Domain", "test-service.com" },
            { "Status", "1" },
            { "Purpose", "1" },
            { "IsSelfDeveloped", "false" }
        });

        Assert.AreEqual(HttpStatusCode.Redirect, postResponse.StatusCode);

        var db = GetService<EmployeeCenterDbContext>();
        var service = await db.Services.FirstOrDefaultAsync(s => s.Domain == "test-service.com");
        Assert.IsNotNull(service);
        Assert.AreEqual(ServiceStatus.Running, service.Status);
    }

    [TestMethod]
    public async Task TestFrpsServiceRequiresAndStoresBothServers()
    {
        await LoginAsAdmin();

        var db = GetService<EmployeeCenterDbContext>();
        var runningServer = new Server { Hostname = "running-server", ServerIp = "192.168.50.178" };
        var frpsServer = new Server
        {
            Hostname = "frps-server",
            ServerIp = "124.160.101.12",
            Ipv6Address = "240e:f7:a020:203::9:de"
        };
        db.Servers.AddRange(runningServer, frpsServer);
        await db.SaveChangesAsync();

        var invalidResponse = await PostForm("/Services/Create", new Dictionary<string, string>
        {
            { "Domain", "invalid-frps-service.com" },
            { "Status", "1" },
            { "Purpose", "1" },
            { "IsViaFrps", "true" },
            { "ServerId", runningServer.Id.ToString() }
        });
        Assert.AreEqual(HttpStatusCode.OK, invalidResponse.StatusCode);
        Assert.IsNull(await db.Services.FirstOrDefaultAsync(service => service.Domain == "invalid-frps-service.com"));

        var validResponse = await PostForm("/Services/Create", new Dictionary<string, string>
        {
            { "Domain", "valid-frps-service.com" },
            { "Status", "1" },
            { "Purpose", "1" },
            { "IsViaFrps", "true" },
            { "ServerId", runningServer.Id.ToString() },
            { "FrpsServerId", frpsServer.Id.ToString() }
        });
        Assert.AreEqual(HttpStatusCode.Redirect, validResponse.StatusCode);

        db.ChangeTracker.Clear();
        var service = await db.Services.SingleAsync(item => item.Domain == "valid-frps-service.com");
        Assert.AreEqual(runningServer.Id, service.ServerId);
        Assert.AreEqual(frpsServer.Id, service.FrpsServerId);
        Assert.IsTrue(service.IsViaFrps);
    }

    [TestMethod]
    public async Task ServiceTargetedByDomainAliasCannotBeDeleted()
    {
        await LoginAsAdmin();
        var db = GetService<EmployeeCenterDbContext>();
        var target = new Service { Domain = "target-for-alias.example.com" };
        db.DomainAliases.Add(new DomainAlias
        {
            Domain = "alias-for-target.example.com",
            TargetService = target,
            TargetUrl = "https://target-for-alias.example.com/"
        });
        await db.SaveChangesAsync();

        var response = await PostForm(
            $"/Services/Delete/{target.Id}",
            new Dictionary<string, string>(),
            tokenUrl: "/Services/List");

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.IsNotNull(await db.Services.FindAsync(target.Id));
    }

    [TestMethod]
    public async Task ServiceListWarnsAboutMissingFrpsAssignmentWithoutShowingFrpsName()
    {
        await LoginAsAdmin();

        var db = GetService<EmployeeCenterDbContext>();
        var runningServer = new Server { Hostname = "list-running-server", ServerIp = "192.168.50.178" };
        var frpsServer = new Server { Hostname = "list-hidden-frps-server", ServerIp = "124.160.101.12" };
        db.Services.AddRange(
            new Service
            {
                Domain = "valid-list-frps-service.example.com",
                Server = runningServer,
                FrpsServer = frpsServer,
                IsViaFrps = true,
                Status = ServiceStatus.Running
            },
            new Service
            {
                Domain = "missing-list-frps-service.example.com",
                Server = runningServer,
                IsViaFrps = true,
                Status = ServiceStatus.Running
            });
        await db.SaveChangesAsync();

        var response = await Http.GetAsync("/Services/List");

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Please associate an FRPS server.", content);
        Assert.DoesNotContain("list-hidden-frps-server", content);
    }

    [TestMethod]
    public async Task TestProviders()
    {
        await LoginAsAdmin();

        // Create Provider
        var response = await PostForm("/Services/CreateProvider", new Dictionary<string, string>
        {
            { "NewName", "TestProvider" }
        });
        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);

        var db = GetService<EmployeeCenterDbContext>();
        var provider = await db.Providers.FirstOrDefaultAsync(p => p.Name == "TestProvider");
        Assert.IsNotNull(provider);

        // Delete Provider
        var deleteResponse = await PostForm($"/Services/DeleteProvider/{provider.Id}", new Dictionary<string, string>());
        Assert.AreEqual(HttpStatusCode.Redirect, deleteResponse.StatusCode);

        db = GetService<EmployeeCenterDbContext>();
        db.ChangeTracker.Clear();
        var deletedProvider = await db.Providers.FindAsync(provider.Id);
        Assert.IsNull(deletedProvider);
    }
}
