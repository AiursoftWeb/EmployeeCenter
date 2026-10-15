using Aiursoft.Canon.BackgroundJobs;
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
public class RetryingTransactionTests : TestBase
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
        var target = new Service { PrimaryDomain = "target.example.com", Name = "Target" };
        db.Services.Add(target);
        await db.SaveChangesAsync();
        failure.FailOnSave = failOnce ? 2 : 0;
        failure.SaveCount = 0;
        var manager = GetService<UserManager<User>>();
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
                    manager, GetService<BackgroundJobRegistry>())
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
        failure.FailOnSave = 2;
        var controller = new ServicesController(db, null!, null!, null!, new InfrastructureChangeLogService(db),
            null!, GetService<UserManager<User>>(), null!)
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
        var manager = GetService<UserManager<User>>();
        var user = (await manager.FindByEmailAsync("admin@default.com"))!;
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
        var storage = GetService<StorageService>();
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
                GetService<Microsoft.AspNetCore.Authorization.IAuthorizationService>(), GetService<QualificationFileService>(), storage);
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
            File.Delete(path);
        }
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
