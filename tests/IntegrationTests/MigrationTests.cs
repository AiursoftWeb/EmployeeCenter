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
    public async Task SecureAudioUploadsMigrationPreservesSharedLegacyFilePaths()
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
}
