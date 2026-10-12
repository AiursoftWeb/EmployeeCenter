using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.PromotionHistoryViewModels;
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
public class PromotionHistoryController(
    UserManager<User> userManager,
    EmployeeCenterDbContext context)
    : Controller
{
    [RenderInNavBar(
        NavGroupName = "Career",
        NavGroupOrder = 1,
        CascadedLinksGroupName = "Report Line",
        CascadedLinksIcon = "network",
        CascadedLinksOrder = 4,
        LinkText = "Promotion History",
        LinkOrder = 2)]
    public async Task<IActionResult> Index()
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var now = DateTime.UtcNow;
        var startOfYear = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var totalPromotionsThisYear = await context.PromotionHistories
            .CountAsync(h => h.ChangeTime >= startOfYear);

        var myHistories = await context.PromotionHistories
            .Where(h => h.UserId == user.Id)
            .OrderByDescending(h => h.ChangeTime)
            .ToListAsync();

        var myPromotionCount = myHistories.Count;

        TimeSpan timeSinceLastPromotion;
        bool hasNeverBeenPromoted = false;

        if (myHistories.Count > 0)
        {
            timeSinceLastPromotion = now - myHistories.First().ChangeTime;
        }
        else
        {
            hasNeverBeenPromoted = true;
            timeSinceLastPromotion = now - user.CreationTime;
        }

        var allHistories = await context.PromotionHistories
            .Include(h => h.User)
            .Include(h => h.Promoter)
            .OrderByDescending(h => h.ChangeTime)
            .Take(100) // Limit to 100 for now to prevent performance issues
            .ToListAsync();

        var model = new IndexViewModel
        {
            TotalPromotionsThisYear = totalPromotionsThisYear,
            MyPromotionCount = myPromotionCount,
            TimeSinceLastPromotion = timeSinceLastPromotion,
            HasNeverBeenPromoted = hasNeverBeenPromoted,
            Histories = allHistories
        };

        return this.StackView(model);
    }

    [Authorize(Policy = AppPermissionNames.CanEditAndViewDetailsOfUsers)]
    public async Task<IActionResult> Create(string? userId)
    {
        var model = new CreateViewModel();
        if (!string.IsNullOrEmpty(userId))
        {
            var user = await context.Users.FindAsync(userId);
            if (user != null)
            {
                model.UserId = user.Id;
                model.UserDisplayName = user.DisplayName;
                model.OldJobLevel = user.JobLevel;
                model.OldTitle = user.Title;
            }
        }
        return this.StackView(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanEditAndViewDetailsOfUsers)]
    public async Task<IActionResult> Create(CreateViewModel model)
    {
        if (ModelState.IsValid)
        {
            var user = await context.Users.FindAsync(model.UserId);
            if (user == null)
            {
                return NotFound();
            }

            var promoter = await userManager.GetUserAsync(User);
            var history = new PromotionHistory
            {
                UserId = user.Id,
                OldJobLevel = user.JobLevel,
                NewJobLevel = model.NewJobLevel,
                OldTitle = user.Title,
                NewTitle = model.NewTitle,
                ChangeTime = DateTime.UtcNow,
                PromoterId = promoter?.Id
            };

            user.JobLevel = model.NewJobLevel;
            user.Title = model.NewTitle;

            context.PromotionHistories.Add(history);
            await context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        return this.StackView(model);
    }
}
