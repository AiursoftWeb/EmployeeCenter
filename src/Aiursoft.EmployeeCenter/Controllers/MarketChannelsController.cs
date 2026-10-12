using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.MarketChannelsViewModels;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.UiStack.Navigation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Aiursoft.EmployeeCenter.Controllers;

[Authorize]
public class MarketChannelsController(
    EmployeeCenterDbContext dbContext,
    UserManager<User> userManager,
    IStringLocalizer<MarketChannelsController> localizer) : Controller
{
    [HttpGet]
    [RenderInNavBar(
        NavGroupName = "Administration",
        NavGroupOrder = 3,
        CascadedLinksGroupName = "Market Channels",
        CascadedLinksIcon = "megaphone",
        CascadedLinksOrder = 7,
        LinkText = "View Market Channels",
        LinkOrder = 1)]
    public async Task<IActionResult> Index()
    {
        var channels = await dbContext.MarketChannels
            .Include(t => t.Manager)
            .OrderByDescending(t => t.CreationTime)
            .ToListAsync();
        return this.StackView(new IndexViewModel
        {
            MarketChannels = channels,
            PageTitle = localizer["View Market Channels"]
        });
    }

    [HttpGet]
    [Authorize(Policy = AppPermissionNames.CanManageMarketChannels)]
    [RenderInNavBar(
        NavGroupName = "Administration",
        NavGroupOrder = 3,
        CascadedLinksGroupName = "Market Channels",
        CascadedLinksIcon = "megaphone",
        CascadedLinksOrder = 7,
        LinkText = "Manage Market Channels",
        LinkOrder = 2)]
    public async Task<IActionResult> Manage()
    {
        var channels = await dbContext.MarketChannels
            .Include(t => t.Manager)
            .OrderByDescending(t => t.CreationTime)
            .ToListAsync();
        return this.StackView(new IndexViewModel
        {
            MarketChannels = channels,
            PageTitle = localizer["Manage Market Channels"]
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var channel = await dbContext.MarketChannels
            .Include(t => t.Manager)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (channel == null) return NotFound();

        return this.StackView(new DetailsViewModel
        {
            MarketChannel = channel,
            PageTitle = channel.Name
        });
    }

    [HttpGet]
    [Authorize(Policy = AppPermissionNames.CanManageMarketChannels)]
    public async Task<IActionResult> Create()
    {
        return this.StackView(new EditorViewModel
        {
            Users = await userManager.Users.OrderBy(u => u.UserName).ToListAsync(),
            PageTitle = localizer["Create Market Channel"],
            Name = string.Empty,
            ManagerId = string.Empty,
            TargetAudience = string.Empty
        }, "Editor");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanManageMarketChannels)]
    public async Task<IActionResult> Create(EditorViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.Users = await userManager.Users.OrderBy(u => u.UserName).ToListAsync();
            model.PageTitle = localizer["Create Market Channel"];
            return this.StackView(model, "Editor");
        }

        var channel = new MarketChannel
        {
            Name = model.Name,
            ManagerId = model.ManagerId,
            TargetAudience = model.TargetAudience,
            Description = model.Description,
            CreationTime = DateTime.UtcNow,
            UpdateTime = DateTime.UtcNow
        };

        dbContext.MarketChannels.Add(channel);
        await dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(Manage));
    }

    [HttpGet]
    [Authorize(Policy = AppPermissionNames.CanManageMarketChannels)]
    public async Task<IActionResult> Edit(int id)
    {
        var channel = await dbContext.MarketChannels.FindAsync(id);
        if (channel == null) return NotFound();

        return this.StackView(new EditorViewModel
        {
            Id = channel.Id,
            Name = channel.Name,
            ManagerId = channel.ManagerId,
            TargetAudience = channel.TargetAudience,
            Description = channel.Description,
            Users = await userManager.Users.OrderBy(u => u.UserName).ToListAsync(),
            PageTitle = localizer["Edit Market Channel"]
        }, "Editor");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanManageMarketChannels)]
    public async Task<IActionResult> Edit(EditorViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.Users = await userManager.Users.OrderBy(u => u.UserName).ToListAsync();
            model.PageTitle = localizer["Edit Market Channel"];
            return this.StackView(model, "Editor");
        }

        var channel = await dbContext.MarketChannels.FindAsync(model.Id);
        if (channel == null) return NotFound();

        channel.Name = model.Name;
        channel.ManagerId = model.ManagerId;
        channel.TargetAudience = model.TargetAudience;
        channel.Description = model.Description;
        channel.UpdateTime = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(Manage));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanManageMarketChannels)]
    public async Task<IActionResult> Delete(int id)
    {
        var channel = await dbContext.MarketChannels.FindAsync(id);
        if (channel == null) return NotFound();

        dbContext.MarketChannels.Remove(channel);
        await dbContext.SaveChangesAsync();
        return RedirectToAction(nameof(Manage));
    }
}
