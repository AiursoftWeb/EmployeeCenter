using Microsoft.Extensions.Localization;
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
    QualificationFileService files,
    IStringLocalizer<ManageQualificationsController> localizer) : Controller
{
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
        return this.StackView(await WithCompanies(new CreateViewModel()));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanManageQualifications)]
    public async Task<IActionResult> Create(CreateViewModel model)
    {
        if (!await db.CompanyEntities.AnyAsync(c => c.Id == model.CompanyEntityId))
            ModelState.AddModelError(nameof(model.CompanyEntityId), localizer["Please select a valid company entity."]);
        if (!ModelState.IsValid)
            return this.StackView(await WithCompanies(model));

        BusinessQualification qualification;
        try
        {
            qualification = await files.PrepareOriginalAsync(model.OriginalPath!, users.GetUserId(User)!,
                model.CompanyEntityId, model.Name, model.Version, HttpContext.RequestAborted);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException)
        {
            ModelState.AddModelError(nameof(model.OriginalPath), localizer["The uploaded original is invalid or missing. Please upload a PNG or JPEG image again."]);
            return this.StackView(await WithCompanies(model));
        }
        try
        {
            db.BusinessQualifications.Add(qualification);
            await db.SaveChangesAsync(HttpContext.RequestAborted);
        }
        catch
        {
            files.DeleteOriginal(qualification);
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
    public IActionResult Detect() => this.StackView(new DetectViewModel
    {
        UploadSubfolder = QualificationFileService.UploadSubfolder(users.GetUserId(User)!, detection: true)
    });

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanManageQualifications)]
    public async Task<IActionResult> Detect(DetectViewModel model)
    {
        model.UploadSubfolder = QualificationFileService.UploadSubfolder(users.GetUserId(User)!, detection: true);
        if (!ModelState.IsValid) return this.StackView(model);
        try
        {
            var trace = files.Detect(model.ImagePath!, users.GetUserId(User)!);
            model.Record = trace.HasValue ? await db.QualificationDownloads.AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == trace.Value) : null;
            model.Message = model.Record is not null
                ? "A tracking ID matches an issuance record. This result alone is not proof of misuse by the applicant."
                : "No verifiable issuance record was found. An unmarked image, image transformations or damage may prevent detection; no match does not establish its origin.";
            ModelState.Remove(nameof(model.ImagePath));
            model.ImagePath = null;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException)
        {
            ModelState.AddModelError(nameof(model.ImagePath), localizer["The image to inspect is invalid or missing. Please upload a PNG or JPEG image again."]);
        }
        return this.StackView(model);
    }

    private Task<List<CompanyEntity>> Companies() => db.CompanyEntities.AsNoTracking().OrderBy(c => c.CompanyName).ToListAsync();

    private async Task<CreateViewModel> WithCompanies(CreateViewModel model)
    {
        model.Companies = await Companies();
        model.UploadSubfolder = QualificationFileService.UploadSubfolder(users.GetUserId(User)!);
        return model;
    }
}
