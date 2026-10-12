using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.PasswordsViewModels;
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
public class PasswordsController(
    UserManager<User> userManager,
    RoleManager<IdentityRole> roleManager,
    IAuthorizationService authorizationService,
    StorageService storageService,
    PasswordSecretService passwordSecretService,
    ILogger<PasswordsController> logger,
    EmployeeCenterDbContext context)
    : Controller
{
    [RenderInNavBar(
        NavGroupName = "Career",
        NavGroupOrder = 1,
        CascadedLinksGroupName = "Resources",
        CascadedLinksIcon = "briefcase",
        CascadedLinksOrder = 6,
        LinkText = "Shared Passwords",
        LinkOrder = 1)]
    public async Task<IActionResult> Index()
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var myPasswords = await context.Passwords
            .Where(p => p.CreatorId == user.Id)
            .OrderByDescending(p => p.CreationTime)
            .ToListAsync();

        var userRoles = await userManager.GetRolesAsync(user);
        var userRoleIds = await roleManager.Roles
            .Where(r => userRoles.Contains(r.Name!))
            .Select(r => r.Id)
            .ToListAsync();

        var sharedWithMe = await context.PasswordShares
            .Include(s => s.Password)
                .ThenInclude(p => p.Creator)
            .Where(s => s.SharedWithUserId == user.Id || (s.SharedWithRoleId != null && userRoleIds.Contains(s.SharedWithRoleId)))
            .OrderByDescending(s => s.CreationTime)
            .ToListAsync();

        return this.StackView(new IndexViewModel
        {
            MyPasswords = myPasswords,
            SharedWithMe = sharedWithMe
        });
    }

    [Authorize(Policy = AppPermissionNames.CanAddGlobalPassword)]
    public IActionResult Create()
    {
        return this.StackView(new CreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanAddGlobalPassword)]
    public async Task<IActionResult> Create(CreateViewModel model)
    {
        if (ModelState.IsValid)
        {
            if (!string.IsNullOrWhiteSpace(model.FilePath))
            {
                try
                {
                    var physicalPath = storageService.GetFilePhysicalPath(model.FilePath, isVault: true);
                    if (!System.IO.File.Exists(physicalPath))
                    {
                        ModelState.AddModelError(nameof(model.FilePath), "The file upload failed or the file is missing. Please re-upload.");
                        return this.StackView(model);
                    }
                }
                catch (ArgumentException)
                {
                    return BadRequest();
                }
            }

            var user = await userManager.GetUserAsync(User);
            var password = new Password
            {
                Title = model.Title!,
                Account = model.Account,
                Secret = passwordSecretService.ProtectPlaintext(model.Secret!),
                Note = passwordSecretService.ProtectNote(model.Note),
                FilePath = model.FilePath,
                CreatorId = user!.Id
            };
            context.Passwords.Add(password);
            await context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return this.StackView(model);
    }

    public async Task<IActionResult> Details(Guid id)
    {
        var password = await context.Passwords
            .Include(p => p.Creator)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (password == null) return NotFound();

        var userId = userManager.GetUserId(User);
        var permission = await GetUserPermission(password, userId!);

        if (permission == null) return Unauthorized();

        password.Secret = passwordSecretService.UnprotectStoredValue(password.Secret);
        password.Note = passwordSecretService.UnprotectStoredNote(password.Note);

        return this.StackView(new DetailsViewModel
        {
            Password = password,
            Permission = permission.Value
        });
    }

    public async Task<IActionResult> Edit(Guid id)
    {
        var password = await context.Passwords.FindAsync(id);
        if (password == null) return NotFound();

        var userId = userManager.GetUserId(User);
        var permission = await GetUserPermission(password, userId!);

        if (permission != SharePermission.Editable) return Unauthorized();

        return this.StackView(new EditViewModel
        {
            Id = password.Id,
            Title = password.Title,
            Account = password.Account,
            Secret = passwordSecretService.UnprotectStoredValue(password.Secret),
            Note = passwordSecretService.UnprotectStoredNote(password.Note),
            FilePath = password.FilePath
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditViewModel model)
    {
        if (ModelState.IsValid)
        {
            var password = await context.Passwords.FindAsync(model.Id);
            if (password == null) return NotFound();

            var userId = userManager.GetUserId(User);
            var permission = await GetUserPermission(password, userId!);

            if (permission != SharePermission.Editable) return Unauthorized();

            if (!string.IsNullOrWhiteSpace(model.FilePath) && model.FilePath != password.FilePath)
            {
                try
                {
                    var physicalPath = storageService.GetFilePhysicalPath(model.FilePath, isVault: true);
                    if (!System.IO.File.Exists(physicalPath))
                    {
                        ModelState.AddModelError(nameof(model.FilePath), "The file upload failed or the file is missing. Please re-upload.");
                        return this.StackView(model);
                    }
                }
                catch (ArgumentException)
                {
                    return BadRequest();
                }
            }

            // Cleanup old file if changed or removed
            if (!string.IsNullOrWhiteSpace(password.FilePath) && password.FilePath != model.FilePath)
            {
                var oldPhysicalPath = string.Empty;
                try
                {
                    oldPhysicalPath = storageService.GetFilePhysicalPath(password.FilePath, isVault: true);
                    if (System.IO.File.Exists(oldPhysicalPath))
                    {
                        System.IO.File.Delete(oldPhysicalPath);
                    }
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Failed to cleanup old password attachment at {Path}", oldPhysicalPath);
                }
            }

            password.Title = model.Title!;
            password.Account = model.Account;
            password.Secret = passwordSecretService.ProtectPlaintext(model.Secret!);
            password.Note = passwordSecretService.ProtectNote(model.Note);
            password.FilePath = model.FilePath;

            await context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id = password.Id });
        }
        return this.StackView(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var password = await context.Passwords.FindAsync(id);
        if (password == null) return NotFound();

        var userId = userManager.GetUserId(User);
        var isOwner = password.CreatorId == userId;
        var canManageAny = (await authorizationService.AuthorizeAsync(User, AppPermissionNames.CanManageAnyPassword)).Succeeded;

        if (!isOwner && !canManageAny) return Unauthorized();

        // Cleanup attachment file
        if (!string.IsNullOrWhiteSpace(password.FilePath))
        {
            var physicalPath = string.Empty;
            try
            {
                physicalPath = storageService.GetFilePhysicalPath(password.FilePath, isVault: true);
                if (System.IO.File.Exists(physicalPath))
                {
                    System.IO.File.Delete(physicalPath);
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to cleanup password attachment at {Path}", physicalPath);
            }
        }

        context.Passwords.Remove(password);
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> ManageShares(Guid id)
    {
        var password = await context.Passwords
            .Include(p => p.PasswordShares)
                .ThenInclude(s => s.SharedWithUser)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (password == null) return NotFound();

        var userId = userManager.GetUserId(User);
        var isOwner = password.CreatorId == userId;
        var canManageAny = (await authorizationService.AuthorizeAsync(User, AppPermissionNames.CanManageAnyPassword)).Succeeded;

        if (!isOwner && !canManageAny) return Unauthorized();

        var allRoles = await roleManager.Roles.ToListAsync();

        return this.StackView(new ManageSharesViewModel
        {
            PasswordId = password.Id,
            PasswordTitle = password.Title,
            ExistingShares = password.PasswordShares.ToList(),
            AvailableRoles = allRoles
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddShare(Guid id, AddShareViewModel model)
    {
        var password = await context.Passwords.FindAsync(id);
        if (password == null) return NotFound();

        var userId = userManager.GetUserId(User);
        var isOwner = password.CreatorId == userId;
        var canManageAny = (await authorizationService.AuthorizeAsync(User, AppPermissionNames.CanManageAnyPassword)).Succeeded;

        if (!isOwner && !canManageAny) return Unauthorized();

        if (string.IsNullOrWhiteSpace(model.TargetUserId) && string.IsNullOrWhiteSpace(model.TargetRoleId))
        {
            return RedirectToAction(nameof(ManageShares), new { id, error = "invalid" });
        }

        var exists = await context.PasswordShares
            .AnyAsync(s => s.PasswordId == id &&
                           ((model.TargetUserId != null && s.SharedWithUserId == model.TargetUserId) ||
                            (model.TargetRoleId != null && s.SharedWithRoleId == model.TargetRoleId)));

        if (exists)
        {
            return RedirectToAction(nameof(ManageShares), new { id, error = "duplicate" });
        }

        var share = new PasswordShare
        {
            PasswordId = id,
            SharedWithUserId = model.TargetUserId,
            SharedWithRoleId = model.TargetRoleId,
            Permission = model.Permission
        };

        context.PasswordShares.Add(share);
        await context.SaveChangesAsync();

        return RedirectToAction(nameof(ManageShares), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveShare(Guid id)
    {
        var share = await context.PasswordShares
            .Include(s => s.Password)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (share == null) return NotFound();

        var userId = userManager.GetUserId(User);
        var isOwner = share.Password.CreatorId == userId;
        var canManageAny = (await authorizationService.AuthorizeAsync(User, AppPermissionNames.CanManageAnyPassword)).Succeeded;

        if (!isOwner && !canManageAny) return Unauthorized();

        var passwordId = share.PasswordId;
        context.PasswordShares.Remove(share);
        await context.SaveChangesAsync();

        return RedirectToAction(nameof(ManageShares), new { id = passwordId });
    }

    private async Task<SharePermission?> GetUserPermission(Password password, string userId)
    {
        if (password.CreatorId == userId) return SharePermission.Editable;

        var canManageAny = (await authorizationService.AuthorizeAsync(User, AppPermissionNames.CanManageAnyPassword)).Succeeded;
        if (canManageAny) return SharePermission.Editable;

        var user = await userManager.FindByIdAsync(userId);
        if (user == null) return null;

        var userRoles = await userManager.GetRolesAsync(user);
        var userRoleIds = await roleManager.Roles
            .Where(r => userRoles.Contains(r.Name!))
            .Select(r => r.Id)
            .ToListAsync();

        var share = await context.PasswordShares
            .Where(s => s.PasswordId == password.Id)
            .Where(s => s.SharedWithUserId == userId || (s.SharedWithRoleId != null && userRoleIds.Contains(s.SharedWithRoleId)))
            .OrderByDescending(s => s.Permission) // Editable (1) > ReadOnly (0)
            .FirstOrDefaultAsync();

        return share?.Permission;
    }
}
