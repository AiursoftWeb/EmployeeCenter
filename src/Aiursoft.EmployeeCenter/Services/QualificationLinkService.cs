using System.Security.Claims;
using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Services.FileStorage;
using Aiursoft.Scanner.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.EmployeeCenter.Services;

/// <summary>Business authorization and ZIP link issuance; file delivery stays generic.</summary>
public sealed class QualificationLinkService(
    EmployeeCenterDbContext db,
    UserManager<User> users,
    IAuthorizationService authorization,
    QualificationFileService files,
    StorageService storage) : IScopedDependency
{
    public async Task<string?> IssueAsync(Guid id, ClaimsPrincipal principal, HttpContext http, CancellationToken cancellationToken)
    {
        var user = await users.GetUserAsync(principal);
        if (user == null) return null;
        var query = db.QualificationDownloads.Include(d => d.BusinessQualification)
            .Where(d => d.Id == id && d.BusinessQualification.IsActive);
        if (!(await authorization.AuthorizeAsync(principal, AppPermissionNames.CanManageQualifications)).Succeeded)
            query = query.Where(d => d.ApplicantId == user.Id);
        var record = await query.FirstOrDefaultAsync(cancellationToken);
        if (record == null) return null;
        await files.EnsureArchiveAsync(record, cancellationToken);
        var url = storage.RelativePathToInternetUrl(record.ArchivePath!, http, isVault: true);
        var issueId = Guid.NewGuid();
        var archivePath = record.ArchivePath;
        var archiveSha256 = record.ArchiveSha256;
        var attempt = 0;
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            if (attempt++ > 0)
            {
                db.ChangeTracker.Clear();
                // A commit may succeed even when its acknowledgement is lost.
                if (await db.QualificationLinkIssues.AnyAsync(i => i.Id == issueId, cancellationToken)) return;
                record = await query.FirstAsync(cancellationToken);
                record.ArchivePath = archivePath;
                record.ArchiveSha256 = archiveSha256;
            }
            var now = DateTime.UtcNow;
            await using var transaction = db.Database.IsRelational()
                ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
            if (db.Database.IsRelational())
                await db.QualificationDownloads.Where(d => d.Id == id).ExecuteUpdateAsync(update => update
                    .SetProperty(d => d.LinkIssuedCount, d => d.LinkIssuedCount + 1)
                    .SetProperty(d => d.LastLinkIssuedAt, now), cancellationToken);
            else
            {
                record.LinkIssuedCount++;
                record.LastLinkIssuedAt = now;
            }
            db.QualificationLinkIssues.Add(new QualificationLinkIssue
            {
                Id = issueId, QualificationDownloadId = id, IssuedToId = user.Id,
                IssuedToName = string.IsNullOrWhiteSpace(user.LegalName) ? user.DisplayName : user.LegalName.Trim(),
                IssuedAt = now
            });
            await db.SaveChangesAsync(cancellationToken);
            if (transaction != null) await transaction.CommitAsync(cancellationToken);
        });
        return url;
    }
}
