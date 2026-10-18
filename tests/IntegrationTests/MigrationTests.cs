using Aiursoft.EmployeeCenter.MySql;
using Aiursoft.EmployeeCenter.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

/// <summary>
/// This test class ensures that the Entity Framework migrations are up-to-date for all supported database providers.
/// If you change the database model (entities), you must create a new migration for both SQLite and MySQL.
/// </summary>
[TestClass]
public class MigrationTests
{
    [TestMethod]
    public async Task QualificationMigrationPreservesExistingCompanyAndManualMeetingMinutes()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SqliteContext>().UseSqlite(connection).Options;
        await using var context = new SqliteContext(options);
        var migrator = context.Database.GetService<IMigrator>();
        await migrator.MigrateAsync("20260901231224_InfrastructureRegistryValidatedConstraints");

        var company = new CompanyEntity
        {
            CompanyName = "Existing company",
            EntityCode = "EXISTING-COMPANY"
        };
        var audio = new Audio
        {
            Name = "Existing recording",
            FilePath = "audio/existing.mp3",
            AsrResult = new AudioAsrResult
            {
                PlainText = "Existing transcript",
                TranscriptRevision = 3,
                MeetingMinutesMarkdown = "# Manually edited minutes\n\nRetain this text.",
                MeetingMinutesTranscriptRevision = 3
            }
        };
        context.CompanyEntities.Add(company);
        context.Audios.Add(audio);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        await migrator.MigrateAsync();
        context.ChangeTracker.Clear();

        var retainedCompany = await context.CompanyEntities.SingleAsync();
        Assert.AreEqual(company.Id, retainedCompany.Id);
        Assert.AreEqual(company.CompanyName, retainedCompany.CompanyName);
        Assert.AreEqual(company.EntityCode, retainedCompany.EntityCode);
        var retainedAudio = await context.Audios.Include(a => a.AsrResult).SingleAsync();
        Assert.AreEqual(audio.FilePath, retainedAudio.FilePath);
        Assert.AreEqual(audio.AsrResult.PlainText, retainedAudio.AsrResult?.PlainText);
        Assert.AreEqual(audio.AsrResult.MeetingMinutesMarkdown, retainedAudio.AsrResult?.MeetingMinutesMarkdown);
        Assert.AreEqual(3, retainedAudio.AsrResult?.TranscriptRevision);
        Assert.AreEqual(3, retainedAudio.AsrResult?.MeetingMinutesTranscriptRevision);
        Assert.AreEqual(0, await context.BusinessQualifications.CountAsync());
        Assert.AreEqual(0, await context.QualificationDownloads.CountAsync());
        Assert.IsTrue(context.Model.FindEntityType(typeof(AudioAsrResult))!
            .FindProperty(nameof(AudioAsrResult.MeetingMinutesMarkdown))!.IsConcurrencyToken);
        Assert.IsFalse(context.Database.HasPendingModelChanges());
    }

    [TestMethod]
    public async Task RemovingEmployeeSignalsDropsLegacyDataAndOnlyObsoleteRolePermissions()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SqliteContext>().UseSqlite(connection).Options;
        await using var context = new SqliteContext(options);
        var migrator = context.Database.GetService<IMigrator>();
        const string previousMigration = "20261014082833_AddQualificationZipLinks";
        await migrator.MigrateAsync(previousMigration);

        var user = new User
        {
            Id = "legacy-survey-user",
            UserName = "legacy-survey-user",
            DisplayName = "Legacy survey user",
            AvatarRelativePath = User.DefaultAvatarPath
        };
        context.Users.Add(user);
        context.Roles.AddRange(
            new IdentityRole { Id = "legacy-role", Name = "Legacy role" },
            new IdentityRole { Id = "another-role", Name = "Another role" });
        context.RoleClaims.AddRange(
            new IdentityRoleClaim<string> { RoleId = "legacy-role", ClaimType = "Permission", ClaimValue = "CanSubmitFeedback" },
            new IdentityRoleClaim<string> { RoleId = "legacy-role", ClaimType = "Permission", ClaimValue = "CanManageFeedback" },
            new IdentityRoleClaim<string> { RoleId = "another-role", ClaimType = "Permission", ClaimValue = "CanSubmitFeedback" },
            new IdentityRoleClaim<string> { RoleId = "another-role", ClaimType = "Permission", ClaimValue = "CanManageFeedback" },
            new IdentityRoleClaim<string> { RoleId = "legacy-role", ClaimType = "Permission", ClaimValue = "RetainedPermission" },
            new IdentityRoleClaim<string> { RoleId = "legacy-role", ClaimType = "Other", ClaimValue = "CanSubmitFeedback" });
        await context.SaveChangesAsync();

        var now = DateTime.UtcNow;
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO SignalQuestions (Id, Title, Type, CreationTime)
            VALUES (1, 'Legacy question', 2, {now});
            INSERT INTO SignalQuestionnaires (Id, Title, IsActive, CreationTime)
            VALUES (1, 'Legacy questionnaire', 1, {now});
            INSERT INTO SignalQuestionnaireQuestions (Id, QuestionnaireId, QuestionId, "Order")
            VALUES (1, 1, 1, 0);
            INSERT INTO SignalResponses (Id, QuestionnaireId, UserId, SubmitTime)
            VALUES (1, 1, {user.Id}, {now});
            INSERT INTO SignalQuestionResponses (Id, SignalResponseId, QuestionId, Answer)
            VALUES (1, 1, 1, 'Legacy answer');
            """);

        string[] removedTables =
        [
            "SignalQuestionResponses", "SignalQuestionnaireQuestions", "SignalResponses", "SignalQuestions",
            "SignalQuestionnaires"
        ];
        foreach (var table in removedTables)
        {
            Assert.AreEqual(1, await context.Database.SqlQuery<int>($"""
                SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'table' AND name = {table}
                """).SingleAsync());
        }

        context.ChangeTracker.Clear();
        await migrator.MigrateAsync();
        Assert.IsFalse(context.Database.HasPendingModelChanges());
        foreach (var table in removedTables)
        {
            Assert.AreEqual(0, await context.Database.SqlQuery<int>($"""
                SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'table' AND name = {table}
                """).SingleAsync(), $"{table} must be removed.");
        }
        Assert.AreEqual(user.Id, (await context.Users.SingleAsync()).Id);
        Assert.AreEqual(2, await context.Roles.CountAsync());
        Assert.AreEqual(2, await context.RoleClaims.CountAsync());
        Assert.IsTrue(await context.RoleClaims.AnyAsync(claim =>
            claim.ClaimType == "Permission" && claim.ClaimValue == "RetainedPermission"));
        Assert.IsTrue(await context.RoleClaims.AnyAsync(claim =>
            claim.ClaimType == "Other" && claim.ClaimValue == "CanSubmitFeedback"));

        await migrator.MigrateAsync(previousMigration);
        foreach (var table in removedTables)
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM \"{table}\"";
            Assert.AreEqual(0L, await command.ExecuteScalarAsync(), $"Rollback must recreate {table} without its deleted data.");
            var expectedForeignKeys = table is "SignalQuestions" or "SignalQuestionnaires" ? 0 : 2;
            Assert.AreEqual(expectedForeignKeys, await context.Database.SqlQuery<int>($"""
                SELECT COUNT(*) AS Value FROM pragma_foreign_key_list({table})
                """).SingleAsync());
            Assert.AreEqual(expectedForeignKeys, await context.Database.SqlQuery<int>($"""
                SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'index' AND tbl_name = {table}
                """).SingleAsync());
        }
        Assert.AreEqual(2, await context.RoleClaims.CountAsync());
        await migrator.MigrateAsync();
    }

    [TestMethod]
    public void TestSqliteMigrations()
    {
        var options = new DbContextOptionsBuilder<SqliteContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        using var context = new SqliteContext(options);
        var hasPendingChanges = context.Database.HasPendingModelChanges();
        Assert.IsFalse(hasPendingChanges, "There are pending model changes for Sqlite. Please run 'dotnet ef migrations add' for Sqlite.");
    }

    [TestMethod]
    public void TestMySqlMigrations()
    {
        var options = new DbContextOptionsBuilder<MySqlContext>()
            .UseMySql("Server=localhost;Database=test;Uid=root;Pwd=password;", new MySqlServerVersion(new Version(8, 0, 31)))
            .Options;
        using var context = new MySqlContext(options);
        var hasPendingChanges = context.Database.HasPendingModelChanges();
        Assert.IsFalse(hasPendingChanges, "There are pending model changes for MySql. Please run 'dotnet ef migrations add' for MySql.");
    }

    [TestMethod]
    public void MySqlValidatedConstraintsOmitUnsupportedIdentityColumnCheck()
    {
        var options = new DbContextOptionsBuilder<MySqlContext>()
            .UseMySql("Server=localhost;Database=test;Uid=root;Pwd=password;", new MySqlServerVersion(new Version(8, 0, 31)))
            .Options;
        using var context = new MySqlContext(options);
        var migrator = context.Database.GetService<IMigrator>();

        var script = migrator.GenerateScript(
            "20260901223755_InfrastructureRegistryV2",
            "20260901231252_InfrastructureRegistryValidatedConstraints");

        Assert.DoesNotContain("CK_Services_NoSelfAlternative", script);
        StringAssert.Contains(script, "CK_Services_ValidatedFrps");
        StringAssert.Contains(script, "CK_Servers_ValidatedIdentifier");
    }

    [TestMethod]
    public void SqliteRetainsSupportedSelfAlternativeCheck()
    {
        var options = new DbContextOptionsBuilder<SqliteContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        using var context = new SqliteContext(options);
        var migrator = context.Database.GetService<IMigrator>();

        var script = migrator.GenerateScript(
            "20260901223744_InfrastructureRegistryV2",
            "20260901231224_InfrastructureRegistryValidatedConstraints");

        StringAssert.Contains(script, "CK_Services_NoSelfAlternative");
    }

    [TestMethod]
    public async Task RemovingAudioViewScopesPreservesAudioDataAndExplicitShares()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SqliteContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new SqliteContext(options);
        var migrator = context.Database.GetService<IMigrator>();
        await migrator.MigrateAsync("20260728071918_DropRenderedHtml");

        var owner = new User
        {
            Id = "audio-owner",
            UserName = "audio-owner",
            DisplayName = "Audio Owner",
            AvatarRelativePath = User.DefaultAvatarPath
        };
        var sharedUser = new User
        {
            Id = "shared-user",
            UserName = "shared-user",
            DisplayName = "Shared User",
            AvatarRelativePath = User.DefaultAvatarPath
        };
        context.Users.AddRange(owner, sharedUser);
        await context.SaveChangesAsync();

        var createTime = DateTime.UtcNow;
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO Audios
                (Id, Name, FilePath, AsrAttemptCount, EmptyResultCount, LastAsrAttemptTime, CreateTime, OwnerId, AudienceDepartment, ViewScope)
            VALUES
                (1001, 'Legacy public audio', 'audio/public.mp3', 0, 0, NULL, {createTime}, {owner.Id}, NULL, 2),
                (1002, 'Legacy department audio', 'audio/department.mp3', 0, 0, NULL, {createTime}, {owner.Id}, 'Engineering', 1)
            """);

        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO AudioAsrResults (AudioId, PlainText, CreateTime)
            VALUES
                (1001, 'Public transcript', {createTime}),
                (1002, 'Department transcript', {createTime})
            """);
        context.AudioShares.Add(new AudioShare
        {
            AudioId = 1002,
            SharedWithUserId = sharedUser.Id,
            Permission = SharePermission.ReadOnly
        });
        await context.SaveChangesAsync();

        context.ChangeTracker.Clear();
        await migrator.MigrateAsync();
        context.ChangeTracker.Clear();

        var audios = await context.Audios
            .Where(audio => audio.Id == 1001 || audio.Id == 1002)
            .Include(audio => audio.AsrResult)
            .Include(audio => audio.AudioShares)
            .OrderBy(audio => audio.Id)
            .ToListAsync();

        Assert.HasCount(2, audios);
        Assert.AreEqual("Public transcript", audios[0].AsrResult?.PlainText);
        Assert.IsNull(audios[0].AsrResult?.MeetingMinutesMarkdown);
        Assert.AreEqual(0, audios[0].AsrResult?.MeetingMinutesAttemptCount);
        Assert.IsEmpty(audios[0].AudioShares);
        Assert.AreEqual("Department transcript", audios[1].AsrResult?.PlainText);
        Assert.IsNull(audios[1].AsrResult?.MeetingMinutesMarkdown);
        Assert.AreEqual(0, audios[1].AsrResult?.MeetingMinutesAttemptCount);
        Assert.HasCount(1, audios[1].AudioShares);
        Assert.AreEqual(sharedUser.Id, audios[1].AudioShares[0].SharedWithUserId);
        Assert.AreEqual(SharePermission.ReadOnly, audios[1].AudioShares[0].Permission);
    }

    [TestMethod]
    public async Task AudioMediaProcessingMigrationPreservesSharedLegacyFilePaths()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SqliteContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new SqliteContext(options);
        var migrator = context.Database.GetService<IMigrator>();
        await migrator.MigrateAsync("20260804114043_AddTrademarkImageToIntangibleAssets");

        var createTime = DateTime.UtcNow;
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO Audios
                (Id, Name, FilePath, AsrAttemptCount, EmptyResultCount, CreateTime)
            VALUES
                (2001, 'Shared legacy audio one', 'audio/shared-legacy.mp3', 0, 0, {createTime}),
                (2002, 'Shared legacy audio two', 'audio/shared-legacy.mp3', 0, 0, {createTime})
            """);

        await migrator.MigrateAsync();
        context.ChangeTracker.Clear();

        var sharedPaths = await context.Audios
            .Where(audio => audio.Id == 2001 || audio.Id == 2002)
            .Select(audio => audio.FilePath)
            .ToListAsync();
        Assert.HasCount(2, sharedPaths);
        Assert.IsTrue(sharedPaths.All(path => path == "audio/shared-legacy.mp3"));
    }

    [TestMethod]
    public async Task InfrastructureRegistryMigrationPreservesDirtyLegacyRows()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SqliteContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new SqliteContext(options);
        var migrator = context.Database.GetService<IMigrator>();
        await migrator.MigrateAsync("20260901181117_EncryptPasswordVaultNotes");

        var now = DateTime.UtcNow;
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO Services
                (AuthentikIntegrated, CreatedAt, Domain, IsCloudflareProxied, IsSelfDeveloped,
                 IsViaFrps, IsAvailabilityAuditEnabled, Purpose, Status, UpdatedAt)
            VALUES
                (0, {now}, 'Example.com.', 0, 0, 0, 1, 1, 4, {now}),
                (0, {now}, 'example.com', 0, 0, 0, 1, 1, 1, {now})
            """);
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO Servers (CreatedAt, UpdatedAt) VALUES ({now}, {now})
            """);

        await migrator.MigrateAsync("20260901231224_InfrastructureRegistryValidatedConstraints");
        context.ChangeTracker.Clear();

        var services = await context.Services.OrderBy(service => service.Id).ToListAsync();
        Assert.HasCount(2, services);
        Assert.AreEqual("Example.com.", services[0].PrimaryDomain);
        Assert.IsNull(services[0].NormalizedPrimaryDomain);
        Assert.IsFalse(services[0].IsRegistryValidated);
        Assert.AreEqual("example.com", services[1].PrimaryDomain);
        Assert.IsNull(services[1].NormalizedPrimaryDomain);
        Assert.IsFalse(services[1].IsRegistryValidated);

        var server = await context.Servers.SingleAsync();
        Assert.IsNull(server.Hostname);
        Assert.IsNull(server.NormalizedHostname);
        Assert.IsFalse(server.IsRegistryValidated);
        Assert.AreEqual(0, await context.ServiceAuditRuns.CountAsync());
        Assert.AreEqual(0, await context.InfrastructureChangeLogs.CountAsync());

        context.Servers.Add(new Server { IsRegistryValidated = true });
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        context.ChangeTracker.Clear();

        context.Services.Add(new Service
        {
            PrimaryDomain = "invalid-new-frps.example.com",
            IsRegistryValidated = true,
            IsViaFrps = true
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }
}
