using Microsoft.Extensions.Localization;
using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.QualificationsViewModels;
using Aiursoft.EmployeeCenter.Services;
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
    QualificationFileService files,
    QualificationLinkService links,
    IAuthorizationService authorization,
    IStringLocalizer<QualificationsController> localizer,
    ILogger<QualificationsController> logger) : Controller
{
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
    public async Task<IActionResult> Apply(ApplyViewModel model)
    {
        var qualification = await FindActive(model.Id);
        var applicant = await users.GetUserAsync(User);
        if (qualification is null) return NotFound();
        if (applicant is null) return Challenge();
        model.Qualification = qualification;
        model.ApplicantName = ApplicantName(applicant);
        if (!ModelState.IsValid) return this.StackView(model);

        QualificationDownload record;
        try
        {
            record = await files.GenerateAsync(qualification, applicant, model.ApplicantName,
                model.Purpose, model.Recipient, model.WatermarkLanguage, HttpContext.RequestAborted);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException or InvalidOperationException)
        {
            logger.LogWarning(exception, "Failed to watermark qualification {QualificationId}", qualification.Id);
            ModelState.AddModelError(string.Empty, localizer["Unable to generate the watermark. Ask an administrator to check the original and watermark font configuration."]);
            return this.StackView(model);
        }

        try
        {
            db.QualificationDownloads.Add(record);
            await db.SaveChangesAsync(HttpContext.RequestAborted);
        }
        catch
        {
            files.DeleteCopy(record);
            throw;
        }
        return RedirectToAction(nameof(Preview), new { id = record.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Preview(Guid id)
    {
        var record = await FindAccessibleRecord(id);
        if (record is null) return NotFound();
        return this.StackView(new PreviewViewModel { Record = record });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Download(Guid id)
    {
        var url = await links.IssueAsync(id, User, HttpContext, HttpContext.RequestAborted);
        return url == null ? NotFound() : Redirect(url);
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

}
