using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Configuration;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.WeeklyReportViewModels;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.UiStack.Navigation;
using Aiursoft.WebTools.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Aiursoft.EmployeeCenter.Controllers;

[Authorize]
[LimitPerMin]
public class WeeklyReportController(
    EmployeeCenterDbContext dbContext,
    UserManager<User> userManager,
    IAuthorizationService authorizationService,
    IStringLocalizer<WeeklyReportController> localizer) : Controller
{
    [RenderInNavBar(
        NavGroupName = "Career",
        NavGroupOrder = 1,
        CascadedLinksGroupName = "Development",
        CascadedLinksIcon = "git-branch",
        CascadedLinksOrder = 2,
        LinkText = "Weekly Report",
        LinkOrder = 3)]
    public async Task<IActionResult> Index(string? userId)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var canCreate = (await authorizationService.AuthorizeAsync(User, AppPermissionNames.CanCreateWeeklyReport)).Succeeded;
        var canManageAnyone = (await authorizationService.AuthorizeAsync(User, AppPermissionNames.CanManageAnyoneWeeklyReport)).Succeeded;

        var query = dbContext.WeeklyReports
            .Include(r => r.User)
            .Include(r => r.WeeklyReportRequirements)
            .AsNoTracking();

        if (!string.IsNullOrEmpty(userId))
        {
            query = query.Where(r => r.UserId == userId);
        }

        var reports = await query
            .OrderByDescending(r => r.WeekStartDate)
            .ThenByDescending(r => r.CreateTime)
            .Take(10)
            .ToListAsync();

        var notepad = await dbContext.Notepads.FirstOrDefaultAsync(n => n.UserId == user.Id);

        // Calculate available weeks for the current user (for default display)
        var availableWeeks = await CalculateAvailableWeeks(user.Id, canManageAnyone);

        // Calculate missing reports for the current user (for status display)
        var now = DateTime.UtcNow;
        var offset = (int)now.DayOfWeek;
        var thisWeekStart = now.AddDays(-offset).Date;

        var cutoffDate = thisWeekStart.AddDays(-49 * 7);
        var userReports = await dbContext.WeeklyReports
            .Where(r => r.UserId == user.Id && (r.WeekStartDate >= cutoffDate || r.CreateTime >= cutoffDate))
            .Select(r => new { r.WeekStartDate, r.CreateTime })
            .ToListAsync();

        var existingWeeks = userReports
            .Select(r => r.WeekStartDate != DateTime.MinValue
                ? r.WeekStartDate
                : r.CreateTime.AddDays(-(int)r.CreateTime.DayOfWeek).Date)
            .ToHashSet();

        var missingWeeksCount = 0;
        for (int i = 0; i < 4; i++)
        {
            var targetWeek = thisWeekStart.AddDays(-i * 7);
            if (!existingWeeks.Contains(targetWeek))
            {
                missingWeeksCount++;
            }
        }

        var submittedThisWeek = existingWeeks.Contains(thisWeekStart);

        var approvedProjects = await dbContext.Requirements
            .Where(r => r.Status == RequirementStatus.Approved)
            .OrderByDescending(r => r.CreationTime)
            .ToListAsync();

        var forceProjectAssociationStr = await dbContext.GlobalSettings
            .Where(s => s.Key == SettingsMap.ForceProjectAssociation)
            .Select(s => s.Value)
            .FirstOrDefaultAsync();

        var forceProjectAssociation = forceProjectAssociationStr == "True";

        var model = new IndexViewModel
        {
            Reports = reports,
            CanCreate = canCreate,
            CanManageAnyoneWeeklyReport = canManageAnyone,
            NotepadContent = notepad?.Content,
            CurrentWeekSubmitted = submittedThisWeek,
            AvailableWeeks = availableWeeks,
            HasRecentMissingReports = missingWeeksCount > 0,
            CriticalMissingReports = missingWeeksCount >= 4,
            MissingWeeksCount = missingWeeksCount,
            FilterUserId = userId,
            ApprovedProjects = approvedProjects,
            ForceProjectAssociation = forceProjectAssociation
        };

        if (canManageAnyone)
        {
            model.AllUsers = await dbContext.Users
                .OrderBy(u => u.DisplayName)
                .ToListAsync();
        }

        if (!string.IsNullOrEmpty(userId))
        {
            model.FilterUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
        }

        return this.StackView(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Content))
        {
            return RedirectToAction(nameof(Index));
        }

        var user = await userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var canCreate = (await authorizationService.AuthorizeAsync(User, AppPermissionNames.CanCreateWeeklyReport)).Succeeded;
        var canManageAnyone = (await authorizationService.AuthorizeAsync(User, AppPermissionNames.CanManageAnyoneWeeklyReport)).Succeeded;

        string targetUserId;
        if (!string.IsNullOrEmpty(model.OnBehalfOf) && canManageAnyone)
        {
            targetUserId = model.OnBehalfOf;
        }
        else if (canCreate)
        {
            targetUserId = user.Id;
        }
        else
        {
            return Unauthorized();
        }

        // Global Setting Check
        var forceProjectAssociationStr = await dbContext.GlobalSettings
            .Where(s => s.Key == SettingsMap.ForceProjectAssociation)
            .Select(s => s.Value)
            .FirstOrDefaultAsync();

        var forceProjectAssociation = forceProjectAssociationStr == "True";
        var modelRequirements = model.Requirements ?? [];

        if (forceProjectAssociation && (!modelRequirements.Any() || modelRequirements.All(r => r.Hours <= 0)))
        {
            // Ideally return error. But for now redirect to index. Maybe add error message?
            // Since we don't have a good way to show errors on index (it reloads page), just redirect.
            // Or maybe implement error via TempData? No, user might lose content.
            // Given constraint, I will return BadRequest for now if validation fails, or just ignore (which is bad).
            // Let's rely on frontend validation mostly, and here just reject if invalid.
            return BadRequest(localizer["Project association is required."]);
        }

        // Week validation
        var now = DateTime.UtcNow;
        var offset = (int)now.DayOfWeek;
        var thisWeekStart = now.AddDays(-offset).Date;
        var targetWeek = model.WeekStartDate ?? thisWeekStart;

        // Ensure targetWeek is a Sunday
        if (targetWeek.DayOfWeek != DayOfWeek.Sunday)
        {
            // Fallback or error. Let's align it just in case.
            targetWeek = targetWeek.AddDays(-(int)targetWeek.DayOfWeek).Date;
        }

        // Verify it is within allowed range (last 50 weeks)
        if (targetWeek < thisWeekStart.AddDays(-50 * 7) || targetWeek > thisWeekStart)
        {
            // Invalid date
            return RedirectToAction(nameof(Index));
        }

        // Check if report already exists for this week
        var existing = await dbContext.WeeklyReports
            .Include(t => t.WeeklyReportRequirements)
            .FirstOrDefaultAsync(r => r.UserId == targetUserId &&
                           (r.WeekStartDate == targetWeek ||
                           (r.WeekStartDate == DateTime.MinValue && r.CreateTime >= targetWeek && r.CreateTime < targetWeek.AddDays(7))));

        if (existing != null)
        {
            existing.Content += "\r\n\r\n" + model.Content;
            existing.WeekStartDate = targetWeek;

            // Append requirements
            foreach (var req in modelRequirements.Where(r => r.Hours > 0))
            {
                // Check if already exists? Maybe just add new entry even if duplicate project?
                // Or sum hours? The requirement says "add multiple".
                // I will add as new entries.
                dbContext.WeeklyReportRequirements.Add(new WeeklyReportRequirement
                {
                    WeeklyReportId = existing.Id,
                    RequirementId = req.RequirementId,
                    Hours = req.Hours
                });
            }

            await dbContext.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        var report = new WeeklyReport
        {
            UserId = targetUserId,
            Content = model.Content,
            CreateTime = DateTime.UtcNow,
            WeekStartDate = targetWeek
        };

        dbContext.WeeklyReports.Add(report);
        await dbContext.SaveChangesAsync();

        // Add requirements
        foreach (var req in modelRequirements.Where(r => r.Hours > 0))
        {
            dbContext.WeeklyReportRequirements.Add(new WeeklyReportRequirement
            {
                WeeklyReportId = report.Id,
                RequirementId = req.RequirementId,
                Hours = req.Hours
            });
        }
        await dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var report = await dbContext.WeeklyReports
            .Include(r => r.User)
            .Include(r => r.WeeklyReportRequirements)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (report == null) return NotFound();

        var canManageAnyone = (await authorizationService.AuthorizeAsync(User, AppPermissionNames.CanManageAnyoneWeeklyReport)).Succeeded;
        if (report.UserId != user.Id && !canManageAnyone)
        {
            return Unauthorized();
        }

        if (!canManageAnyone && report.CreateTime < DateTime.UtcNow.AddDays(-28))
        {
            return BadRequest(localizer["You can only edit reports published within 4 weeks."]);
        }

        var approvedProjects = await dbContext.Requirements
            .Where(r => r.Status == RequirementStatus.Approved)
            .OrderByDescending(r => r.CreationTime)
            .ToListAsync();

        var forceProjectAssociationStr = await dbContext.GlobalSettings
            .Where(s => s.Key == SettingsMap.ForceProjectAssociation)
            .Select(s => s.Value)
            .FirstOrDefaultAsync();

        var forceProjectAssociation = forceProjectAssociationStr == "True";

        var model = new EditViewModel
        {
            Id = report.Id,
            Content = report.Content,
            WeekStartDate = report.WeekStartDate,
            User = report.User,
            Requirements = report.WeeklyReportRequirements
                .Select(r => new WeeklyReportRequirementViewModel
                {
                    RequirementId = r.RequirementId,
                    Hours = r.Hours
                })
                .ToList(),
            ApprovedProjects = approvedProjects,
            ForceProjectAssociation = forceProjectAssociation
        };

        return this.StackView(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditViewModel model)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var report = await dbContext.WeeklyReports
            .Include(r => r.WeeklyReportRequirements)
            .FirstOrDefaultAsync(r => r.Id == model.Id);

        if (report == null) return NotFound();

        var canManageAnyone = (await authorizationService.AuthorizeAsync(User, AppPermissionNames.CanManageAnyoneWeeklyReport)).Succeeded;
        if (report.UserId != user.Id && !canManageAnyone)
        {
            return Unauthorized();
        }

        if (!canManageAnyone && report.CreateTime < DateTime.UtcNow.AddDays(-28))
        {
            return BadRequest(localizer["You can only edit reports published within 4 weeks."]);
        }

        var forceProjectAssociationStr = await dbContext.GlobalSettings
            .Where(s => s.Key == SettingsMap.ForceProjectAssociation)
            .Select(s => s.Value)
            .FirstOrDefaultAsync();

        if (forceProjectAssociationStr == "True" && (!model.Requirements.Any() || model.Requirements.All(r => r.Hours <= 0)))
        {
            return BadRequest(localizer["Project association is required."]);
        }

        report.Content = model.Content;

        // Update requirements: remove all and add new
        dbContext.WeeklyReportRequirements.RemoveRange(report.WeeklyReportRequirements);
        foreach (var req in model.Requirements.Where(r => r.Hours > 0))
        {
            dbContext.WeeklyReportRequirements.Add(new WeeklyReportRequirement
            {
                WeeklyReportId = report.Id,
                RequirementId = req.RequirementId,
                Hours = req.Hours
            });
        }

        await dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var report = await dbContext.WeeklyReports.FirstOrDefaultAsync(r => r.Id == id);
        if (report == null) return NotFound();

        var canManageAnyone = (await authorizationService.AuthorizeAsync(User, AppPermissionNames.CanManageAnyoneWeeklyReport)).Succeeded;
        if (report.UserId != user.Id && !canManageAnyone)
        {
            return Unauthorized();
        }

        dbContext.WeeklyReports.Remove(report);
        await dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateNotepad(string? content)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var notepad = await dbContext.Notepads.FirstOrDefaultAsync(n => n.UserId == user.Id);
        if (notepad == null)
        {
            notepad = new Notepad
            {
                UserId = user.Id,
                Content = content ?? string.Empty,
                UpdateTime = DateTime.UtcNow
            };
            dbContext.Notepads.Add(notepad);
        }
        else
        {
            notepad.Content = content ?? string.Empty;
            notepad.UpdateTime = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync();
        return Ok();
    }

    [HttpGet]
    public async Task<IActionResult> LoadMore(DateTime beforeWeek, DateTime beforeCreate, string? userId)
    {
        var query = dbContext.WeeklyReports
            .Include(r => r.User)
            .Include(r => r.WeeklyReportRequirements)
            .Where(r => r.WeekStartDate < beforeWeek.ToUniversalTime() ||
                       (r.WeekStartDate == beforeWeek.ToUniversalTime() && r.CreateTime < beforeCreate.ToUniversalTime()))
            .AsNoTracking();

        if (!string.IsNullOrEmpty(userId))
        {
            query = query.Where(r => r.UserId == userId);
        }

        var reports = await query
            .OrderByDescending(r => r.WeekStartDate)
            .ThenByDescending(r => r.CreateTime)
            .Take(10)
            .ToListAsync();

        return PartialView("_ReportCards", reports);
    }

    [HttpGet]
    public async Task<IActionResult> GetAvailableWeeks(string? userId)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var canManageAnyone = (await authorizationService.AuthorizeAsync(User, AppPermissionNames.CanManageAnyoneWeeklyReport)).Succeeded;

        // Determine target user ID
        string targetUserId;
        if (!string.IsNullOrEmpty(userId) && canManageAnyone)
        {
            targetUserId = userId;
        }
        else
        {
            targetUserId = user.Id;
        }

        var availableWeeks = await CalculateAvailableWeeks(targetUserId, canManageAnyone);
        return Json(availableWeeks);
    }

    private async Task<Dictionary<DateTime, string>> CalculateAvailableWeeks(string userId, bool showAll = false)
    {
        var now = DateTime.UtcNow;
        var offset = (int)now.DayOfWeek;
        var thisWeekStart = now.AddDays(-offset).Date;

        var existingWeeks = new HashSet<DateTime>();
        if (!showAll)
        {
            var cutoffDate = thisWeekStart.AddDays(-49 * 7);
            var userReports = await dbContext.WeeklyReports
                .Where(r => r.UserId == userId && (r.WeekStartDate >= cutoffDate || r.CreateTime >= cutoffDate))
                .Select(r => new { r.WeekStartDate, r.CreateTime })
                .ToListAsync();

            existingWeeks = userReports
                .Select(r => r.WeekStartDate != DateTime.MinValue
                    ? r.WeekStartDate
                    : r.CreateTime.AddDays(-(int)r.CreateTime.DayOfWeek).Date)
                .ToHashSet();
        }

        var availableWeeks = new Dictionary<DateTime, string>();
        for (int i = 0; i < 50; i++)
        {
            var weekStart = thisWeekStart.AddDays(-i * 7);
            if (showAll || !existingWeeks.Contains(weekStart) || weekStart == thisWeekStart)
            {
                var label = $"{weekStart:yyyy-MM-dd} ~ {weekStart.AddDays(6):yyyy-MM-dd}";
                if (weekStart == thisWeekStart)
                {
                    label += $" ({localizer["Current Week"]})";
                    if (existingWeeks.Contains(weekStart))
                    {
                        label += $" - ({localizer["Already submitted, click to supplement"]})";
                    }
                }
                availableWeeks.Add(weekStart, label);
            }
        }

        return availableWeeks;
    }
}
