namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class ServiceTests : TestBase
{
    [TestMethod]
    public async Task DnsAuditDashboardLinkRequiresAuditPermission()
    {
        await RegisterAndLoginAsync();

        var response = await Http.GetAsync("/Services/Index");

        Assert.AreEqual(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("/Error/Code403", response.Headers.Location?.OriginalString ?? string.Empty);
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
            PrimaryDomain = "dashboard.example.com",
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
            { "Name", "Test Service" },
            { "PrimaryDomain", "test-service.com" },
            { "Status", "1" },
            { "Purpose", "1" },
            { "IsSelfDeveloped", "false" }
        });

        Assert.AreEqual(HttpStatusCode.Redirect, postResponse.StatusCode);

        var db = GetService<EmployeeCenterDbContext>();
        var service = await db.Services.FirstOrDefaultAsync(s => s.PrimaryDomain == "test-service.com");
        Assert.IsNotNull(service);
        Assert.AreEqual(ServiceStatus.Running, service.Status);
        Assert.IsTrue(service.IsAvailabilityAuditEnabled);
    }

    [TestMethod]
    public async Task ServiceCreateNormalizesIdnAndRejectsLegacyDuplicate()
    {
        await LoginAsAdmin();
        var db = GetService<EmployeeCenterDbContext>();
        db.Services.Add(new Service { PrimaryDomain = "EXAMPLE.com." });
        await db.SaveChangesAsync();

        var duplicate = await PostForm("/Services/Create", new Dictionary<string, string>
        {
            ["Name"] = "Duplicate",
            ["PrimaryDomain"] = "example.com",
            ["Status"] = "1",
            ["Purpose"] = "1"
        });
        Assert.AreEqual(HttpStatusCode.OK, duplicate.StatusCode);
        Assert.AreEqual(1, await db.Services.CountAsync(service =>
            service.PrimaryDomain == "EXAMPLE.com." || service.PrimaryDomain == "example.com"));

        var normalized = await PostForm("/Services/Create", new Dictionary<string, string>
        {
            ["Name"] = "IDN service",
            ["PrimaryDomain"] = "BÜCHER.Example.",
            ["Status"] = "1",
            ["Purpose"] = "1"
        });
        Assert.AreEqual(HttpStatusCode.Found, normalized.StatusCode);
        db.ChangeTracker.Clear();
        var service = await db.Services.SingleAsync(item => item.Name == "IDN service");
        Assert.AreEqual("xn--bcher-kva.example", service.PrimaryDomain);
        Assert.AreEqual(service.PrimaryDomain, service.NormalizedPrimaryDomain);
        Assert.IsTrue(service.IsRegistryValidated);
        Assert.IsFalse(string.IsNullOrWhiteSpace(service.ConcurrencyToken));
    }

    [TestMethod]
    public async Task StaleServiceEditIsRejectedAndChangeIsLogged()
    {
        await LoginAsAdmin();
        var create = await PostForm("/Services/Create", new Dictionary<string, string>
        {
            ["Name"] = "Concurrent service",
            ["PrimaryDomain"] = "concurrent.example.com",
            ["Status"] = "1",
            ["Purpose"] = "1"
        });
        Assert.AreEqual(HttpStatusCode.Found, create.StatusCode);

        var db = GetService<EmployeeCenterDbContext>();
        var service = await db.Services.SingleAsync(item => item.PrimaryDomain == "concurrent.example.com");
        var staleToken = service.ConcurrencyToken;
        service.Name = "Changed elsewhere";
        service.ConcurrencyToken = Guid.NewGuid().ToString();
        await db.SaveChangesAsync();

        var staleEdit = await PostForm($"/Services/Edit/{service.Id}", new Dictionary<string, string>
        {
            ["Id"] = service.Id.ToString(),
            ["Name"] = "Overwrite attempt",
            ["PrimaryDomain"] = service.PrimaryDomain,
            ["ConcurrencyToken"] = staleToken!,
            ["Status"] = "1",
            ["Purpose"] = "1"
        });
        Assert.AreEqual(HttpStatusCode.OK, staleEdit.StatusCode);
        Assert.Contains("changed by another user", await staleEdit.Content.ReadAsStringAsync());
        db.ChangeTracker.Clear();
        Assert.AreEqual("Changed elsewhere", (await db.Services.FindAsync(service.Id))?.Name);
        Assert.IsTrue(await db.InfrastructureChangeLogs.AnyAsync(log =>
            log.ResourceType == nameof(Service) && log.ResourceId == service.Id && log.Action == "Created"));
    }

    [TestMethod]
    public async Task DataQualityReportFindsLegacyRegistryProblems()
    {
        await LoginAsAdmin();
        var db = GetService<EmployeeCenterDbContext>();
        db.Services.AddRange(
            new Service { PrimaryDomain = "Duplicate.example.com." },
            new Service { PrimaryDomain = "duplicate.example.com", IsViaFrps = true });
        db.Servers.Add(new Server());
        await db.SaveChangesAsync();

        var response = await Http.GetAsync("/Services/DataQuality");
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("DuplicatePrimaryDomain", html);
        Assert.Contains("InvalidFrpsAssignment", html);
        Assert.Contains("MissingIdentifier", html);
        Assert.Contains("LegacyRow", html);
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
            { "Name", "Invalid FRPS Service" },
            { "PrimaryDomain", "invalid-frps-service.com" },
            { "Status", "1" },
            { "Purpose", "1" },
            { "IsViaFrps", "true" },
            { "ServerId", runningServer.Id.ToString() }
        });
        Assert.AreEqual(HttpStatusCode.OK, invalidResponse.StatusCode);
        Assert.IsNull(await db.Services.FirstOrDefaultAsync(service => service.PrimaryDomain == "invalid-frps-service.com"));

        var validResponse = await PostForm("/Services/Create", new Dictionary<string, string>
        {
            { "Name", "Valid FRPS Service" },
            { "PrimaryDomain", "valid-frps-service.com" },
            { "Status", "1" },
            { "Purpose", "1" },
            { "IsViaFrps", "true" },
            { "ServerId", runningServer.Id.ToString() },
            { "FrpsServerId", frpsServer.Id.ToString() }
        });
        Assert.AreEqual(HttpStatusCode.Redirect, validResponse.StatusCode);

        db.ChangeTracker.Clear();
        var service = await db.Services.SingleAsync(item => item.PrimaryDomain == "valid-frps-service.com");
        Assert.AreEqual(runningServer.Id, service.ServerId);
        Assert.AreEqual(frpsServer.Id, service.FrpsServerId);
        Assert.IsTrue(service.IsViaFrps);
    }

    [TestMethod]
    public async Task ServiceCannotReferenceRetiredServerThroughForgedPost()
    {
        await LoginAsAdmin();
        var db = GetService<EmployeeCenterDbContext>();
        var retiredServer = new Server
        {
            Hostname = "retired-for-service",
            RetiredAt = DateTime.UtcNow
        };
        db.Servers.Add(retiredServer);
        await db.SaveChangesAsync();

        var response = await PostForm("/Services/Create", new Dictionary<string, string>
        {
            ["Name"] = "Forged assignment",
            ["PrimaryDomain"] = "forged-retired-server.example.com",
            ["ServerId"] = retiredServer.Id.ToString(),
            ["Status"] = "1",
            ["Purpose"] = "1"
        });

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("does not exist or is retired", await response.Content.ReadAsStringAsync());
        Assert.IsFalse(await db.Services.AnyAsync(service =>
            service.PrimaryDomain == "forged-retired-server.example.com"));
    }

    [TestMethod]
    public async Task ServiceAvailabilityAuditCanBeDisabled()
    {
        await LoginAsAdmin();

        var db = GetService<EmployeeCenterDbContext>();
        var service = new Service
        {
            PrimaryDomain = "restricted-service.example.com",
            Protocols = "HTTPS",
            Status = ServiceStatus.Running
        };
        db.Services.Add(service);
        await db.SaveChangesAsync();

        var response = await PostForm($"/Services/Edit/{service.Id}", new Dictionary<string, string>
        {
            { "Id", service.Id.ToString() },
            { "Name", "Restricted Service" },
            { "PrimaryDomain", service.PrimaryDomain },
            { "Protocols", service.Protocols },
            { "Status", ((int)service.Status).ToString() },
            { "Purpose", ((int)service.Purpose).ToString() },
            { "IsAvailabilityAuditEnabled", "false" }
        });

        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
        db.ChangeTracker.Clear();
        var updated = await db.Services.FindAsync(service.Id);
        Assert.IsNotNull(updated);
        Assert.IsFalse(updated.IsAvailabilityAuditEnabled);
    }

    [TestMethod]
    public async Task ServiceTargetedByDomainAliasIsRetiredWithoutBreakingTheAlias()
    {
        await LoginAsAdmin();
        var db = GetService<EmployeeCenterDbContext>();
        var target = new Service { PrimaryDomain = "target-for-alias.example.com" };
        db.DomainAliases.Add(new DomainAlias
        {
            Domain = "alias-for-target.example.com",
            TargetService = target,
            TargetUrl = "https://target-for-alias.example.com/"
        });
        await db.SaveChangesAsync();

        var response = await PostForm(
            $"/Services/Delete/{target.Id}",
            new Dictionary<string, string> { ["concurrencyToken"] = string.Empty },
            tokenUrl: "/Services/List");

        Assert.AreEqual(HttpStatusCode.Found, response.StatusCode);
        db.ChangeTracker.Clear();
        var retired = await db.Services.FindAsync(target.Id);
        Assert.IsNotNull(retired);
        Assert.AreEqual(ServiceStatus.Retired, retired.Status);
        Assert.IsNotNull(retired.RetiredAt);
        Assert.IsTrue(await db.DomainAliases.AnyAsync(alias => alias.TargetServiceId == target.Id));
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
                PrimaryDomain = "valid-list-frps-service.example.com",
                Server = runningServer,
                FrpsServer = frpsServer,
                IsViaFrps = true,
                Status = ServiceStatus.Running
            },
            new Service
            {
                PrimaryDomain = "missing-list-frps-service.example.com",
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
