using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Models.DnsAuditViewModels;
using Aiursoft.EmployeeCenter.Services.DnsAudit;

namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class InfrastructurePermissionTests : TestBase
{
    [TestMethod]
    public async Task ViewInfrastructureDoesNotExposeAuditOrAllowMutation()
    {
        await RegisterWithPermissionsAsync(AppPermissionNames.CanViewInfrastructure);
        GetService<DnsAuditSnapshotCache>().SetSuccess(ReportWithFinding("private-audit.example.com"), DateTime.UtcNow);

        var services = await Http.GetAsync("/Services/Index");
        services.EnsureSuccessStatusCode();
        var html = await services.Content.ReadAsStringAsync();
        Assert.DoesNotContain("private-audit.example.com", html);
        Assert.DoesNotContain("/ServiceAudit", html, StringComparison.OrdinalIgnoreCase);

        (await Http.GetAsync("/Servers/Index")).EnsureSuccessStatusCode();
        AssertForbidden(await Http.GetAsync("/ServiceAudit"));

        var mutation = await PostForm("/Services/Create", new Dictionary<string, string>
        {
            ["Name"] = "Must not be created",
            ["PrimaryDomain"] = "forbidden.example.com",
            ["Status"] = "1",
            ["Purpose"] = "1"
        }, tokenUrl: "/");
        AssertForbidden(mutation);
        Assert.IsFalse(await GetService<EmployeeCenterDbContext>().Services
            .AnyAsync(service => service.PrimaryDomain == "forbidden.example.com"));
    }

    [TestMethod]
    public async Task AuditViewAndAuditRunAreIndependentPermissions()
    {
        await RegisterWithPermissionsAsync(AppPermissionNames.CanViewServiceAudit);
        var store = GetService<ServiceAuditStore>();
        var runId = await store.BeginRunAsync();
        await store.CompleteSuccessAsync(runId, ReportWithFinding("visible-audit.example.com"));

        var audit = await Http.GetAsync("/ServiceAudit");
        audit.EnsureSuccessStatusCode();
        Assert.Contains("visible-audit.example.com", await audit.Content.ReadAsStringAsync());
        AssertForbidden(await Http.GetAsync("/Services/Index"));

        var refreshWithoutRun = await PostForm(
            "/ServiceAudit/Refresh",
            new Dictionary<string, string>(),
            tokenUrl: "/");
        AssertForbidden(refreshWithoutRun);

        await RegisterWithPermissionsAsync(AppPermissionNames.CanRunServiceAudit);
        var refreshWithRun = await PostForm(
            "/ServiceAudit/Refresh",
            new Dictionary<string, string>(),
            tokenUrl: "/");
        Assert.AreEqual(HttpStatusCode.Found, refreshWithRun.StatusCode);
        Assert.Contains("/ServiceAudit", refreshWithRun.Headers.Location?.OriginalString ?? string.Empty);
        AssertForbidden(await Http.GetAsync("/ServiceAudit"));
    }

    [TestMethod]
    public async Task AuditViewerDoesNotSeeOrReachInfrastructureMutationControls()
    {
        await RegisterWithPermissionsAsync(
            AppPermissionNames.CanViewInfrastructure,
            AppPermissionNames.CanViewServiceAudit);
        var store = GetService<ServiceAuditStore>();
        var runId = await store.BeginRunAsync();
        await store.CompleteSuccessAsync(runId, ReportWithFinding("candidate.example.com"));

        var aliases = await Http.GetAsync("/DomainAliases");
        aliases.EnsureSuccessStatusCode();
        var html = await aliases.Content.ReadAsStringAsync();
        Assert.DoesNotContain("/DomainAliases/Create", html, StringComparison.OrdinalIgnoreCase);
        AssertForbidden(await Http.GetAsync("/DomainAliases/Create"));
    }

    [TestMethod]
    public async Task CompanyEntityDetailsDoNotBypassInfrastructureViewPermission()
    {
        var db = GetService<EmployeeCenterDbContext>();
        var entity = new CompanyEntity
        {
            CompanyName = "Infrastructure owner",
            EntityCode = "INFRA-OWNER",
            CreateLedger = true
        };
        db.Servers.Add(new Server
        {
            Hostname = "private-infrastructure-host",
            CompanyEntity = entity
        });
        await db.SaveChangesAsync();

        await RegisterWithPermissionsAsync();
        var withoutPermission = await Http.GetAsync($"/CompanyEntity/Details/{entity.Id}");
        withoutPermission.EnsureSuccessStatusCode();
        Assert.DoesNotContain("private-infrastructure-host", await withoutPermission.Content.ReadAsStringAsync());

        await RegisterWithPermissionsAsync(AppPermissionNames.CanViewInfrastructure);
        var withPermission = await Http.GetAsync($"/CompanyEntity/Details/{entity.Id}");
        withPermission.EnsureSuccessStatusCode();
        Assert.Contains("private-infrastructure-host", await withPermission.Content.ReadAsStringAsync());
    }

    [TestMethod]
    public async Task AliasCreatePageDoesNotOfferAuditRunWithoutRunPermission()
    {
        await RegisterWithPermissionsAsync(
            AppPermissionNames.CanManageInfrastructure,
            AppPermissionNames.CanViewServiceAudit);

        var response = await Http.GetAsync("/DomainAliases/Create");

        response.EnsureSuccessStatusCode();
        Assert.DoesNotContain("/DomainAliases/RefreshAudit", await response.Content.ReadAsStringAsync(),
            StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public async Task LegacyPermissionsDoNotImplicitlyGrantNewInfrastructureAccess()
    {
        await RegisterWithPermissionsAsync(
            AppPermissionNames.CanManageServices,
            AppPermissionNames.CanAuditDns);

        AssertForbidden(await Http.GetAsync("/Services/Index"));
        AssertForbidden(await Http.GetAsync("/Servers/Index"));
        AssertForbidden(await Http.GetAsync("/ServiceAudit"));
        AssertForbidden(await Http.GetAsync("/Services/Create"));
    }

    private async Task RegisterWithPermissionsAsync(params string[] permissions)
    {
        await Http.GetAsync("/Account/LogOff");
        var (email, password) = await RegisterAndLoginAsync();
        using (var scope = Server!.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var role = new IdentityRole($"Infrastructure-{Guid.NewGuid():N}");
            Assert.IsTrue((await roleManager.CreateAsync(role)).Succeeded);
            foreach (var permission in permissions)
            {
                Assert.IsTrue((await roleManager.AddClaimAsync(
                    role,
                    new Claim(AppPermissions.Type, permission))).Succeeded);
            }

            var user = await userManager.FindByEmailAsync(email);
            Assert.IsNotNull(user);
            Assert.IsTrue((await userManager.AddToRoleAsync(user, role.Name!)).Succeeded);
        }

        await Http.GetAsync("/Account/LogOff");
        await LoginAsAsync(email, password);
    }

    private static DnsAuditReport ReportWithFinding(string domain) => new()
    {
        AuditedHostnameCount = 1,
        Issues =
        [
            new DnsAuditIssue
            {
                Type = DnsAuditIssueType.UnknownDns,
                Severity = DnsAuditSeverity.Critical,
                Domain = domain,
                Details = "Sensitive audit finding"
            }
        ]
    };

    private static void AssertForbidden(HttpResponseMessage response)
    {
        Assert.AreEqual(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("/Error/Code403", response.Headers.Location?.OriginalString ?? string.Empty);
    }
}
