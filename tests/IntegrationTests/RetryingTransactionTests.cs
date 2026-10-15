using Aiursoft.Canon.BackgroundJobs;
using Aiursoft.Canon.TaskQueue;
using Aiursoft.EmployeeCenter.Services.BackgroundJobs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Aiursoft.EmployeeCenter.Controllers;
using Aiursoft.EmployeeCenter.Models.ServicesViewModels;
using Aiursoft.EmployeeCenter.Models.ServersViewModels;
using Aiursoft.EmployeeCenter.Models.DnsAuditViewModels;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.EmployeeCenter.Services.DnsAudit;
using Aiursoft.EmployeeCenter.Services.FileStorage;
using Aiursoft.EmployeeCenter.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.AspNetCore.Mvc;

namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class RetryingTransactionTests
{
    [TestMethod]
    [DataRow("service", false)]
    [DataRow("server", false)]
    [DataRow("dns", false)]
    [DataRow("provider", false)]
    [DataRow("alias", false)]
    [DataRow("service", true)]
    [DataRow("server", true)]
    [DataRow("dns", true)]
    [DataRow("provider", true)]
    [DataRow("alias", true)]
    public async Task CreationAndAuditCommitExactlyOnceWithRetryingStrategy(string kind, bool failOnce)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var failure = new SaveFailure();
        var options = new DbContextOptionsBuilder<SqliteContext>().UseSqlite(connection)
            .ReplaceService<IExecutionStrategyFactory, RetryFactory>().AddInterceptors(failure).Options;
        await using var db = new SqliteContext(options);
        await db.Database.EnsureCreatedAsync();
        using var provider = CreateServices(db);
        var target = new Service { PrimaryDomain = "target.example.com", Name = "Target" };
        db.Services.Add(target);
        await db.SaveChangesAsync();
        failure.FailOnSave = failOnce ? 2 : 0;
        failure.SaveCount = 0;
        var manager = provider.GetRequiredService<UserManager<User>>();
        var log = new InfrastructureChangeLogService(db);
        var http = new DefaultHttpContext();
        var services = new ServicesController(db, null!, null!, null!, log, null!, manager, null!)
            { ControllerContext = new ControllerContext { HttpContext = http } };
        IActionResult result;
        switch (kind)
        {
            case "service":
                result = await services.Create(new CreateServiceViewModel { Name = "Created", PrimaryDomain = "created.example.com" });
                Assert.AreEqual(2, await db.Services.CountAsync());
                break;
            case "server":
                var servers = new ServersController(db, log, manager, null!)
                    { ControllerContext = new ControllerContext { HttpContext = http } };
                result = await servers.Create(new CreateServerViewModel { Hostname = "created.example.com" });
                Assert.AreEqual(1, await db.Servers.CountAsync());
                break;
            case "dns":
                result = await services.CreateDnsProvider(new ManageDnsProvidersViewModel { NewName = "Created" });
                Assert.AreEqual(1, await db.DnsProviders.CountAsync());
                break;
            case "provider":
                result = await services.CreateProvider(new ManageProvidersViewModel { NewName = "Created" });
                Assert.AreEqual(1, await db.Providers.CountAsync());
                break;
            default:
                var snapshot = new DnsAuditSnapshotCache();
                snapshot.SetSuccess(new DnsAuditReport { Issues = [new DnsAuditIssue
                    { Type = DnsAuditIssueType.UnknownDns, Domain = "created.example.com", Details = "Unknown", Severity = DnsAuditSeverity.Warning }] }, DateTime.UtcNow);
                var aliases = new DomainAliasesController(db, snapshot, new ServiceAuditStore(db), null!, log,
                    manager, provider.GetRequiredService<BackgroundJobRegistry>())
                    { ControllerContext = new ControllerContext { HttpContext = http } };
                result = await aliases.Create(new CreateDomainAliasViewModel
                    { Domain = "created.example.com", TargetServiceId = target.Id, Type = DomainAliasType.Cname });
                Assert.AreEqual(1, await db.DomainAliases.CountAsync());
                break;
        }
        Assert.IsInstanceOfType<RedirectToActionResult>(result);
        var audit = await db.InfrastructureChangeLogs.SingleAsync();
        Assert.AreEqual("Created", audit.Action);
        Assert.IsTrue(audit.ResourceId > 0);
        Assert.AreEqual(failOnce, failure.Thrown);
    }

    [TestMethod]
    public async Task FailedAuditRollsBackCreatedService()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var failure = new SaveFailure { Permanent = true };
        var options = new DbContextOptionsBuilder<SqliteContext>().UseSqlite(connection)
            .ReplaceService<IExecutionStrategyFactory, RetryFactory>().AddInterceptors(failure).Options;
        await using var db = new SqliteContext(options);
        await db.Database.EnsureCreatedAsync();
        using var provider = CreateServices(db);
        failure.FailOnSave = 2;
        var controller = new ServicesController(db, null!, null!, null!, new InfrastructureChangeLogService(db),
            null!, provider.GetRequiredService<UserManager<User>>(), null!)
            { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => controller.Create(
            new CreateServiceViewModel { Name = "Rollback", PrimaryDomain = "rollback.example.com" }));
        Assert.IsTrue(failure.Thrown);
        db.ChangeTracker.Clear();
        Assert.AreEqual(0, await db.Services.CountAsync());
        Assert.AreEqual(0, await db.InfrastructureChangeLogs.CountAsync());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task QualificationIssuanceCommitsOneCounterAndAuditWithRetryingStrategy(bool failOnce)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var failure = new SaveFailure();
        var options = new DbContextOptionsBuilder<SqliteContext>().UseSqlite(connection)
            .ReplaceService<IExecutionStrategyFactory, RetryFactory>().AddInterceptors(failure).Options;
        await using var db = new SqliteContext(options);
        await db.Database.EnsureCreatedAsync();
        using var provider = CreateServices(db);
        var manager = provider.GetRequiredService<UserManager<User>>();
        var user = new User { UserName = "admin", DisplayName = "Admin", AvatarRelativePath = User.DefaultAvatarPath };
        Assert.IsTrue((await manager.CreateAsync(user)).Succeeded);
        var record = new QualificationDownload
        {
            Id = Guid.NewGuid(), BusinessQualificationId = 0,
            BusinessQualification = new BusinessQualification
            {
                CompanyEntityId = 0, CompanyEntity = new CompanyEntity { CompanyName = "Test", EntityCode = "TEST" },
                Name = "Test", Version = "1", OriginalPath = "unused.png", OriginalSha256 = new string('a', 64), CreatedById = user.Id
            },
            ApplicantId = user.Id, ApplicantName = "Admin", QualificationName = "Test", CompanyName = "Test",
            Version = "1", OriginalSha256 = new string('a', 64), Purpose = "Test", Recipient = "Test",
            GeneratedPath = "unused.png", GeneratedSha256 = new string('a', 64)
        };
        var storageRoot = Path.Combine(Path.GetTempPath(), "qualification-retry-" + Guid.NewGuid().ToString("N"));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["Storage:Path"] = storageRoot }).Build();
        var storage = new StorageService(new FeatureFoldersProvider(new StorageRootPathProvider(configuration)),
            null!, new EphemeralDataProtectionProvider());
        record.ArchivePath = $"{QualificationFileService.CopiesFolder}/{record.Id:N}.zip";
        var path = storage.GetVaultSubfolderFilePhysicalPath(record.ArchivePath, QualificationFileService.CopiesFolder);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, []);
        try
        {
            db.QualificationDownloads.Add(record);
            await db.SaveChangesAsync();
            failure.SaveCount = 0;
            failure.FailOnSave = failOnce ? 1 : 0;
            var service = new QualificationLinkService(db, manager,
                new AuthorizeAll(), new QualificationFileService(storage, null!, NullLogger<QualificationFileService>.Instance), storage);
            var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id)], "Test"));
            var http = new DefaultHttpContext();
            http.Request.Scheme = "https";
            http.Request.Host = new HostString("example.com");
            Assert.IsNotNull(await service.IssueAsync(record.Id, principal, http, CancellationToken.None));
            db.ChangeTracker.Clear();
            Assert.AreEqual(1, (await db.QualificationDownloads.SingleAsync()).LinkIssuedCount);
            Assert.AreEqual(1, await db.QualificationLinkIssues.CountAsync());
            Assert.AreEqual(failOnce, failure.Thrown);
        }
        finally
        {
            Directory.Delete(storageRoot, recursive: true);
        }
    }

    private static ServiceProvider CreateServices(SqliteContext db)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(db);
        services.AddIdentityCore<User>().AddRoles<IdentityRole>().AddEntityFrameworkStores<SqliteContext>();
        // Queue the follow-up job without a hosted worker or an unrelated InMemory application database.
        services.AddSingleton<ServiceTaskQueue>();
        services.AddTransient<DnsAuditJob>(_ => new DnsAuditJob(null!, null!, null!, NullLogger<DnsAuditJob>.Instance));
        services.AddSingleton(new RegisteredJob { JobType = typeof(DnsAuditJob) });
        services.AddSingleton<BackgroundJobRegistry>();
        return services.BuildServiceProvider();
    }

    private sealed class AuthorizeAll : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource,
            IEnumerable<IAuthorizationRequirement> requirements) => Task.FromResult(AuthorizationResult.Success());
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource,
            string policyName) => Task.FromResult(AuthorizationResult.Success());
    }

    public sealed class RetryFactory(ExecutionStrategyDependencies dependencies) : IExecutionStrategyFactory
    {
        public IExecutionStrategy Create() => new RetryStrategy(dependencies);
    }

    private sealed class RetryStrategy(ExecutionStrategyDependencies dependencies)
        : ExecutionStrategy(dependencies, 1, TimeSpan.Zero)
    {
        protected override bool ShouldRetryOn(Exception exception) => exception is RetryFailure;
    }

    private sealed class RetryFailure : Exception;

    private sealed class SaveFailure : SaveChangesInterceptor
    {
        public int FailOnSave { get; set; }
        public int SaveCount { get; set; }
        public bool Thrown { get; private set; }
        public bool Permanent { get; init; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (++SaveCount != FailOnSave) return base.SavingChangesAsync(eventData, result, cancellationToken);
            Thrown = true;
            if (Permanent) throw new InvalidOperationException("Audit save failed.");
            throw new RetryFailure();
        }
    }
}
