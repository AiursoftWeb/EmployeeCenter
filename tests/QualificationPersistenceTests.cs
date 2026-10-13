using Aiursoft.EmployeeCenter.Sqlite;
using Microsoft.Data.Sqlite;

namespace Aiursoft.EmployeeCenter.Tests;

[TestClass]
public class QualificationPersistenceTests
{
    [TestMethod]
    public async Task SqliteMigrationPersistsSnapshotsAndSupportsAtomicDownloadCounter()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SqliteContext>().UseSqlite(connection).Options;
        await using var db = new SqliteContext(options);
        await db.Database.MigrateAsync();
        var qualification = new BusinessQualification
        {
            CompanyEntity = new CompanyEntity { CompanyName = "测试主体", EntityCode = "persistence-test" },
            Name = "营业执照",
            Version = "v1",
            OriginalPath = "qualifications/originals/test.png",
            OriginalSha256 = new string('A', 64),
            CreatedById = "administrator-id"
        };
        var id = Guid.NewGuid();
        db.QualificationDownloads.Add(new QualificationDownload
        {
            Id = id,
            BusinessQualification = qualification,
            ApplicantId = "deleted-account-id",
            ApplicantName = "张三",
            QualificationName = "营业执照",
            CompanyName = "测试主体",
            Version = "v1",
            OriginalSha256 = new string('A', 64),
            GeneratedSha256 = new string('B', 64),
            GeneratedPath = "qualifications/copies/test.png",
            Purpose = "供应商入驻业务",
            Recipient = "接收公司"
        });
        await db.SaveChangesAsync();
        var downloadedAt = DateTime.UtcNow;
        for (var i = 0; i < 2; i++)
        {
            Assert.AreEqual(1, await db.QualificationDownloads.Where(d => d.Id == id).ExecuteUpdateAsync(update => update
                .SetProperty(d => d.DownloadCount, d => d.DownloadCount + 1)
                .SetProperty(d => d.LastDownloadedAt, downloadedAt)));
        }
        db.ChangeTracker.Clear();
        var stored = await db.QualificationDownloads.SingleAsync();
        Assert.AreEqual(2, stored.DownloadCount);
        Assert.AreEqual(downloadedAt, stored.LastDownloadedAt);
        Assert.AreEqual("张三", stored.ApplicantName);
        Assert.AreEqual("deleted-account-id", stored.ApplicantId);
        Assert.AreEqual(0, (await db.Database.GetPendingMigrationsAsync()).Count());
    }
}
