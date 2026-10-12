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

[Authorize(Policy = AppPermissionNames.CanManageQualifications)]
[LimitPerMin]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class ManageQualificationsController(
    EmployeeCenterDbContext db,
    UserManager<User> users,
    StorageService storage,
    QualificationWatermarkService watermark,
    ILogger<ManageQualificationsController> logger) : Controller
{
    private const long MaxFileBytes = 20 * 1024 * 1024;
    private const long MaxRequestBytes = MaxFileBytes + 64 * 1024;

    [HttpGet]
    [Authorize(Policy = AppPermissionNames.CanManageQualifications)]
    [RenderInNavBar(NavGroupName = "Administration", NavGroupOrder = 3,
        CascadedLinksGroupName = "Legal", CascadedLinksIcon = "scale", CascadedLinksOrder = 5,
        LinkText = "Manage Business Qualifications", LinkOrder = 5)]
    public async Task<IActionResult> Index()
    {
        return this.StackView(new ManageIndexViewModel
        {
            Qualifications = await db.BusinessQualifications.AsNoTracking().Include(q => q.CompanyEntity)
                .OrderByDescending(q => q.CreatedAt).ToListAsync()
        });
    }

    [HttpGet]
    [Authorize(Policy = AppPermissionNames.CanManageQualifications)]
    public async Task<IActionResult> Create()
    {
        return this.StackView(new CreateViewModel { Companies = await Companies() });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanManageQualifications)]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxFileBytes)]
    public async Task<IActionResult> Create(CreateViewModel model)
    {
        model.Name = model.Name?.Trim() ?? string.Empty;
        model.Version = model.Version?.Trim() ?? string.Empty;
        ValidateText(nameof(model.Name), model.Name);
        ValidateText(nameof(model.Version), model.Version);
        if (!await db.CompanyEntities.AnyAsync(c => c.Id == model.CompanyEntityId))
            ModelState.AddModelError(nameof(model.CompanyEntityId), "请选择有效的公司主体。");
        ValidateImage(nameof(model.Original), model.Original);
        if (!ModelState.IsValid)
            return this.StackView(await WithCompanies(model));

        await using var source = new MemoryStream();
        await model.Original!.CopyToAsync(source, HttpContext.RequestAborted);
        source.Position = 0;
        try
        {
            watermark.ValidateSource(source);
        }
        catch (InvalidDataException exception)
        {
            ModelState.AddModelError(nameof(model.Original), exception.Message);
            return this.StackView(await WithCompanies(model));
        }
        source.Position = 0;
        var originalHash = Convert.ToHexString(SHA256.HashData(source));
        source.Position = 0;
        var extension = Path.GetExtension(model.Original.FileName).ToLowerInvariant();
        var path = await storage.SaveFromStream($"qualifications/originals/{Guid.NewGuid():N}{extension}",
            source, isVault: true, cancellationToken: HttpContext.RequestAborted);
        var qualification = new BusinessQualification
        {
            CompanyEntityId = model.CompanyEntityId,
            Name = model.Name,
            Version = model.Version,
            OriginalPath = path,
            OriginalSha256 = originalHash,
            CreatedById = users.GetUserId(User)!
        };
        try
        {
            db.BusinessQualifications.Add(qualification);
            await db.SaveChangesAsync(HttpContext.RequestAborted);
        }
        catch
        {
            DeleteSavedFile(path);
            throw;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanManageQualifications)]
    public async Task<IActionResult> Retire(int id)
    {
        var qualification = await db.BusinessQualifications.FindAsync(id);
        if (qualification is null) return NotFound();
        qualification.IsActive = false;
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanManageQualifications)]
    public async Task<IActionResult> Enable(int id)
    {
        var qualification = await db.BusinessQualifications.FindAsync(id);
        if (qualification is null) return NotFound();
        qualification.IsActive = true;
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Records(int page = 1, string? query = null)
    {
        const int pageSize = 25;
        page = Math.Clamp(page, 1, 10000);
        query = query?.Trim();
        if (query?.Length > 120) return BadRequest();
        var records = db.QualificationDownloads.AsNoTracking();
        if (!string.IsNullOrEmpty(query))
            records = Guid.TryParse(query, out var trace)
                ? records.Where(d => d.Id == trace)
                : records.Where(d => d.ApplicantName.Contains(query) || d.Purpose.Contains(query) || d.Recipient.Contains(query));
        var items = await records.OrderByDescending(d => d.CreatedAt).ThenBy(d => d.Id)
            .Skip((page - 1) * pageSize).Take(pageSize + 1).ToListAsync();
        return this.StackView(new ManageRecordsViewModel
        {
            Page = page,
            Query = query,
            HasNextPage = items.Count > pageSize,
            Records = items.Take(pageSize).ToList()
        });
    }

    [HttpGet]
    public async Task<IActionResult> Record(Guid id)
    {
        var record = await db.QualificationDownloads.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);
        if (record is null) return NotFound();
        return this.StackView(new ManageRecordViewModel { Record = record });
    }

    [HttpGet]
    public IActionResult Detect() => this.StackView(new DetectViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanManageQualifications)]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxFileBytes)]
    public async Task<IActionResult> Detect(DetectViewModel model)
    {
        ValidateImage(nameof(model.Image), model.Image);
        if (!ModelState.IsValid) return this.StackView(model);
        try
        {
            using var stream = model.Image!.OpenReadStream();
            var trace = watermark.Detect(stream);
            model.Record = trace.HasValue ? await db.QualificationDownloads.AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == trace.Value) : null;
            model.Message = model.Record is not null
                ? "检测到追踪编号并找到对应生成记录。该结果不能单独证明申请人实施了滥用。"
                : "未找到可验证的对应记录。无水印、图像变换或水印损坏都可能导致检测失败，不能据此判断来源。";
        }
        catch (InvalidDataException exception)
        {
            ModelState.AddModelError(nameof(model.Image), exception.Message);
        }
        return this.StackView(model);
    }

    private Task<List<CompanyEntity>> Companies() => db.CompanyEntities.AsNoTracking().OrderBy(c => c.CompanyName).ToListAsync();

    private async Task<CreateViewModel> WithCompanies(CreateViewModel model)
    {
        model.Companies = await Companies();
        return model;
    }

    private void ValidateText(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl) ||
            value.Any(c => char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.Format))
            ModelState.AddModelError(key, "请输入有效的单行文本，不允许控制字符。");
    }

    private void ValidateImage(string key, IFormFile? file)
    {
        if (file is null || file.Length <= 0 || file.Length > MaxFileBytes)
            ModelState.AddModelError(key, "请选择不超过 20 MB 的 PNG 或 JPEG 图片。");
        else if (Path.GetExtension(file.FileName).ToLowerInvariant() is not (".png" or ".jpg" or ".jpeg"))
            ModelState.AddModelError(key, "首版仅支持 PNG 和 JPEG，请先将 PDF 转为图片。");
    }

    private void DeleteSavedFile(string path)
    {
        try { System.IO.File.Delete(storage.GetFilePhysicalPath(path, isVault: true)); }
        catch (IOException exception) { logger.LogError(exception, "Unable to clean up qualification file {Path}", path); }
    }
}
