using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.SshKeysViewModels;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.WebTools.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Aiursoft.EmployeeCenter.Controllers;

[Authorize]
[LimitPerMin]
public class SshKeysController(
    UserManager<User> userManager,
    EmployeeCenterDbContext context,
    IStringLocalizer<SshKeysController> localizer)
    : Controller
{
    private async Task<User?> GetCurrentUserAsync()
    {
        return await userManager.GetUserAsync(HttpContext.User);
    }

    private async Task<bool> CanManageUser(string userId)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null) return false;
        if (currentUser.Id == userId) return true;
        return User.HasClaim(AppPermissions.Type, AppPermissionNames.CanManageSshKeys);
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? userId)
    {
        var currentUser = await GetCurrentUserAsync();
        userId ??= currentUser?.Id;
        if (userId == null) return NotFound();

        if (!await CanManageUser(userId))
        {
            return Unauthorized();
        }

        var targetUser = await userManager.FindByIdAsync(userId);
        if (targetUser == null) return NotFound();

        var keys = await context.SshKeys
            .Where(k => k.OwnerId == userId)
            .OrderByDescending(k => k.CreationTime)
            .ToListAsync();

        var model = new IndexViewModel
        {
            SshKeys = keys,
            TargetUser = targetUser
        };
        return this.StackView(model);
    }

    [HttpGet]
    public async Task<IActionResult> Create(string? userId)
    {
        var currentUser = await GetCurrentUserAsync();
        userId ??= currentUser?.Id;
        if (userId == null) return NotFound();

        if (!await CanManageUser(userId))
        {
            return Unauthorized();
        }

        return this.StackView(new CreateViewModel { TargetUserId = userId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateViewModel model)
    {
        if (string.IsNullOrEmpty(model.TargetUserId)) return NotFound();

        if (!await CanManageUser(model.TargetUserId))
        {
            return Unauthorized();
        }

        if (!ModelState.IsValid)
        {
            return this.StackView(model);
        }

        var count = await context.SshKeys.CountAsync(k => k.OwnerId == model.TargetUserId);
        if (count >= 5)
        {
            ModelState.AddModelError(string.Empty, localizer["You can only have up to 5 SSH keys."]);
            return this.StackView(model);
        }

        var key = new SshKey
        {
            Name = model.Name!,
            PublicKey = model.PublicKey!,
            OwnerId = model.TargetUserId
        };

        context.SshKeys.Add(key);
        await context.SaveChangesAsync();

        return RedirectToAction(nameof(Index), new { userId = model.TargetUserId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var key = await context.SshKeys.FindAsync(id);
        if (key == null) return NotFound();

        if (!await CanManageUser(key.OwnerId))
        {
            return Unauthorized();
        }

        var model = new EditViewModel
        {
            Id = key.Id,
            TargetUserId = key.OwnerId,
            Name = key.Name,
            PublicKey = key.PublicKey
        };
        return this.StackView(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditViewModel model)
    {
        var key = await context.SshKeys.FindAsync(model.Id);
        if (key == null) return NotFound();

        if (!await CanManageUser(key.OwnerId))
        {
            return Unauthorized();
        }

        if (!ModelState.IsValid)
        {
            return this.StackView(model);
        }

        key.Name = model.Name!;
        key.PublicKey = model.PublicKey!;

        context.SshKeys.Update(key);
        await context.SaveChangesAsync();

        return RedirectToAction(nameof(Index), new { userId = key.OwnerId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var key = await context.SshKeys.FindAsync(id);
        if (key == null) return NotFound();

        if (!await CanManageUser(key.OwnerId))
        {
            return Unauthorized();
        }

        var userId = key.OwnerId;
        context.SshKeys.Remove(key);
        await context.SaveChangesAsync();

        return RedirectToAction(nameof(Index), new { userId });
    }
}
