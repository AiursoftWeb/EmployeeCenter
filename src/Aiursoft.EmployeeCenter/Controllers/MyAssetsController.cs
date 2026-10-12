using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.MyAssetsViewModels;
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
public class MyAssetsController(
    EmployeeCenterDbContext context,
    UserManager<User> userManager)
    : Controller
{
    [RenderInNavBar(
        NavGroupName = "Personal",
        NavGroupOrder = 2,
        CascadedLinksGroupName = "My IT Assets",
        CascadedLinksIcon = "monitor",
        CascadedLinksOrder = 4,
        LinkText = "My Assets",
        LinkOrder = 1)]
    public async Task<IActionResult> Index()
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var assets = await context.Assets
            .Include(a => a.Model)
            .ThenInclude(m => m.Category)
            .Include(a => a.Location)
            .Where(a => a.AssigneeId == user.Id)
            .OrderByDescending(a => a.UpdatedAt)
            .ToListAsync();

        return this.StackView(new IndexViewModel
        {
            Assets = assets
        });
    }

    public async Task<IActionResult> Details(Guid id)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var asset = await context.Assets
            .Include(a => a.Model)
            .ThenInclude(m => m.Category)
            .Include(a => a.Location)
            .Include(a => a.Vendor)
            .Include(a => a.Histories)
            .ThenInclude(h => h.Operator)
            .FirstOrDefaultAsync(a => a.Id == id && a.AssigneeId == user.Id);

        if (asset == null) return NotFound();

        return this.StackView(new DetailsViewModel
        {
            Asset = asset
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(Guid id)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var asset = await context.Assets.FindAsync(id);
        if (asset == null || asset.AssigneeId != user.Id) return NotFound();

        if (asset.Status != AssetStatus.PendingAccept)
        {
            return BadRequest("Asset is not in pending accept status.");
        }

        asset.Status = AssetStatus.InUse;
        asset.UpdatedAt = DateTime.UtcNow;

        var history = new AssetHistory
        {
            AssetId = asset.Id,
            ActionType = "ACKNOWLEDGE",
            FieldName = "Status",
            OldValue = AssetStatus.PendingAccept.ToString(),
            NewValue = AssetStatus.InUse.ToString(),
            OperatorId = user.Id,
            Reason = "User confirmed receipt.",
            Timestamp = DateTime.UtcNow
        };
        context.AssetHistories.Add(history);

        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(Guid id, string reason)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var asset = await context.Assets.FindAsync(id);
        if (asset == null || asset.AssigneeId != user.Id) return NotFound();

        if (asset.Status != AssetStatus.PendingAccept)
        {
            return BadRequest("Asset is not in pending accept status.");
        }

        asset.Status = AssetStatus.Idle;
        asset.AssigneeId = null;
        asset.UpdatedAt = DateTime.UtcNow;

        var history = new AssetHistory
        {
            AssetId = asset.Id,
            ActionType = "REJECT",
            FieldName = "Status",
            OldValue = AssetStatus.PendingAccept.ToString(),
            NewValue = AssetStatus.Idle.ToString(),
            OperatorId = user.Id,
            Reason = $"User rejected receipt. Reason: {reason}",
            Timestamp = DateTime.UtcNow
        };
        context.AssetHistories.Add(history);

        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
