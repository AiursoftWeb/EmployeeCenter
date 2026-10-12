using System.Security.Cryptography;
using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.QualificationsViewModels;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.EmployeeCenter.Services.FileStorage;
using Aiursoft.UiStack.Navigation;
using Aiursoft.WebTools.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.EmployeeCenter.Controllers;

[Authorize]
[LimitPerMin]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class QualificationsController(
    EmployeeCenterDbContext db,
    UserManager<User> users,
    StorageService storage,
    QualificationWatermarkService watermark,
    IAuthorizationService authorization,
    ILogger<QualificationsController> logger) : Controller
{
    private const long MaxFileBytes = 20 * 1024 * 1024;

    [HttpGet]
    [RenderInNavBar(NavGroupName = "Career", NavGroupOrder = 1,
        CascadedLinksGroupName = "Resources", CascadedLinksIcon = "briefcase", CascadedLinksOrder = 6,
        LinkText = "Business Qualifications", LinkOrder = 5)]
    public async Task<IActionResult> Index(int? companyEntityId)
    {
        var query = db.BusinessQualifications.AsNoTracking().Include(q => q.CompanyEntity)
            .Where(q => q.IsActive);
        if (companyEntityId.HasValue) query = query.Where(q => q.CompanyEntityId == companyEntityId);
        return this.StackView(new IndexViewModel
        {
            Qualifications = await query.OrderByDescending(q => q.CreatedAt).ToListAsync()
        });
    }

    [HttpGet]
    public async Task<IActionResult> Apply(int id)
    {
        var qualification = await FindActive(id);
        var applicant = await users.GetUserAsync(User);
        if (qualification is null) return NotFound();
        if (applicant is null) return Challenge();
        return this.StackView(new ApplyViewModel
        {
            Id = id,
            Qualification = qualification,
            ApplicantName = ApplicantName(applicant)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(64 * 1024)]
    public async Task<IActionResult> Apply(ApplyViewModel model)
    {
        var qualification = await FindActive(model.Id);
        var applicant = await users.GetUserAsync(User);
        if (qualification is null) return NotFound();
        if (applicant is null) return Challenge();
        model.Qualification = qualification;
        model.ApplicantName = ApplicantName(applicant);
        model.Purpose = model.Purpose?.Trim() ?? string.Empty;
        model.Recipient = model.Recipient?.Trim() ?? string.Empty;
        ValidateText(nameof(model.Purpose), model.Purpose);
        ValidateText(nameof(model.Recipient), model.Recipient);
        if (model.Purpose.Length < 4 || model.Recipient.Length < 2)
            ModelState.AddModelError(string.Empty, "请填写具体业务用途及接收单位，不能只填空格。");
        if (!ModelState.IsValid) return this.StackView(model);

        var traceId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;
        byte[] generated;
        try
        {
            var physicalPath = storage.GetVaultSubfolderFilePhysicalPath(qualification.OriginalPath, "qualifications/originals");
            await using var source = System.IO.File.OpenRead(physicalPath);
            if (source.Length > MaxFileBytes ||
                !string.Equals(Convert.ToHexString(SHA256.HashData(source)), qualification.OriginalSha256, StringComparison.Ordinal))
                throw new InvalidDataException("原件校验失败，请联系管理员重新上传。");
            source.Position = 0;
            generated = watermark.Generate(source, new QualificationWatermarkRequest(
                model.Purpose, model.Recipient, model.ApplicantName, createdAt, traceId));
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException or InvalidOperationException)
        {
            logger.LogWarning(exception, "Failed to watermark qualification {QualificationId}", qualification.Id);
            ModelState.AddModelError(string.Empty, "暂时无法生成水印，请联系管理员检查原件和中文字体配置。");
            return this.StackView(model);
        }

        await using var output = new MemoryStream(generated);
        var generatedPath = await storage.SaveFromStream($"qualifications/copies/{traceId:N}.png", output,
            isVault: true, cancellationToken: HttpContext.RequestAborted);
        var record = new QualificationDownload
        {
            Id = traceId,
            BusinessQualificationId = qualification.Id,
            ApplicantId = applicant.Id,
            ApplicantName = model.ApplicantName,
            QualificationName = qualification.Name,
            CompanyName = qualification.CompanyEntity!.CompanyName,
            Version = qualification.Version,
            OriginalSha256 = qualification.OriginalSha256,
            Purpose = model.Purpose,
            Recipient = model.Recipient,
            GeneratedPath = generatedPath,
            GeneratedSha256 = Convert.ToHexString(SHA256.HashData(generated)),
            CreatedAt = createdAt
        };
        try
        {
            db.QualificationDownloads.Add(record);
            await db.SaveChangesAsync(HttpContext.RequestAborted);
        }
        catch
        {
            DeleteSavedFile(generatedPath);
            throw;
        }
        return RedirectToAction(nameof(Preview), new { id = traceId });
    }

    [HttpGet]
    public async Task<IActionResult> Preview(Guid id)
    {
        var record = await FindAccessibleRecord(id);
        if (record is null) return NotFound();
        return this.StackView(new PreviewViewModel { Record = record });
    }

    [HttpGet]
    public Task<IActionResult> Image(Guid id) => Deliver(id, download: false);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Download(Guid id) => Deliver(id, download: true);

    private async Task<IActionResult> Deliver(Guid id, bool download)
    {
        var record = await FindAccessibleRecord(id);
        if (record?.BusinessQualification?.IsActive != true) return NotFound();
        var path = storage.GetVaultSubfolderFilePhysicalPath(record.GeneratedPath, "qualifications/copies");
        if (!System.IO.File.Exists(path)) return NotFound();
        if (download)
        {
            var now = DateTime.UtcNow;
            if (db.Database.IsRelational())
            {
                await db.QualificationDownloads.Where(d => d.Id == id).ExecuteUpdateAsync(update => update
                    .SetProperty(d => d.DownloadCount, d => d.DownloadCount + 1)
                    .SetProperty(d => d.LastDownloadedAt, now));
            }
            else
            {
                record.DownloadCount++;
                record.LastDownloadedAt = now;
                await db.SaveChangesAsync();
            }
        }
        Response.Headers.XContentTypeOptions = "nosniff";
        return download
            ? PhysicalFile(path, "image/png", $"qualification-{id:N}.png")
            : PhysicalFile(path, "image/png");
    }

    [HttpGet]
    public async Task<IActionResult> Records(int page = 1, string? query = null)
    {
        const int pageSize = 25;
        page = Math.Clamp(page, 1, 10000);
        query = query?.Trim();
        if (query?.Length > 120) return BadRequest();
        var records = db.QualificationDownloads.AsNoTracking()
            .Where(d => d.ApplicantId == users.GetUserId(User));
        if (!string.IsNullOrEmpty(query))
            records = Guid.TryParse(query, out var trace)
                ? records.Where(d => d.Id == trace)
                : records.Where(d => d.ApplicantName.Contains(query) || d.Purpose.Contains(query) || d.Recipient.Contains(query));
        var items = await records.OrderByDescending(d => d.CreatedAt).ThenBy(d => d.Id)
            .Skip((page - 1) * pageSize).Take(pageSize + 1).ToListAsync();
        return this.StackView(new RecordsViewModel
        {
            Page = page,
            Query = query,
            HasNextPage = items.Count > pageSize,
            Records = items.Take(pageSize).ToList()
        });
    }

    private Task<BusinessQualification?> FindActive(int id) => db.BusinessQualifications.AsNoTracking()
        .Include(q => q.CompanyEntity).FirstOrDefaultAsync(q => q.Id == id && q.IsActive);

    private async Task<QualificationDownload?> FindAccessibleRecord(Guid id)
    {
        var query = db.QualificationDownloads.Include(d => d.BusinessQualification).Where(d => d.Id == id);
        if (!await CanManage()) query = query.Where(d => d.ApplicantId == users.GetUserId(User));
        return await query.FirstOrDefaultAsync();
    }

    private async Task<bool> CanManage() =>
        (await authorization.AuthorizeAsync(User, AppPermissionNames.CanManageQualifications)).Succeeded;

    private static string ApplicantName(User user) =>
        string.IsNullOrWhiteSpace(user.LegalName) ? user.DisplayName : user.LegalName.Trim();

    private void ValidateText(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl) ||
            value.Any(c => char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.Format))
            ModelState.AddModelError(key, "请输入有效的单行文本，不允许控制字符。");
    }

    private void DeleteSavedFile(string path)
    {
        try { System.IO.File.Delete(storage.GetFilePhysicalPath(path, isVault: true)); }
        catch (IOException exception) { logger.LogError(exception, "Unable to clean up qualification file {Path}", path); }
    }
}
