using System.Security.Claims;
using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.Scanner.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.EmployeeCenter.Services.FileStorage;

public sealed class QualificationDeliveryService(
    EmployeeCenterDbContext db,
    UserManager<User> users,
    IAuthorizationService authorization,
    QualificationFileService files) : IScopedDependency
{
    public async Task<string?> ResolveCopyAsync(Guid id, ClaimsPrincipal user, bool download, CancellationToken cancellationToken)
    {
        var query = db.QualificationDownloads.Include(d => d.BusinessQualification).Where(d => d.Id == id);
        if (!(await authorization.AuthorizeAsync(user, AppPermissionNames.CanManageQualifications)).Succeeded)
            query = query.Where(d => d.ApplicantId == users.GetUserId(user));
        var record = await query.FirstOrDefaultAsync(cancellationToken);
        if (record?.BusinessQualification.IsActive != true) return null;
        var path = files.CopyPhysicalPath(record);
        if (path is null || !download) return path;
        var now = DateTime.UtcNow;
        if (db.Database.IsRelational())
        {
            await db.QualificationDownloads.Where(d => d.Id == id).ExecuteUpdateAsync(update => update
                .SetProperty(d => d.DownloadCount, d => d.DownloadCount + 1)
                .SetProperty(d => d.LastDownloadedAt, now), cancellationToken);
        }
        else
        {
            record.DownloadCount++;
            record.LastDownloadedAt = now;
            await db.SaveChangesAsync(cancellationToken);
        }
        return path;
    }
}
