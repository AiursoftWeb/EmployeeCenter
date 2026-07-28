using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.UsersViewModels;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.EmployeeCenter.Services.FileStorage;
using Aiursoft.UiStack.Navigation;
using Aiursoft.WebTools.Attributes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace Aiursoft.EmployeeCenter.Controllers;

/// <summary>
/// This controller is used to handle users related actions like create, edit, delete, etc.
/// </summary>
[Authorize]
[LimitPerMin]
public class UsersController(
    RoleManager<IdentityRole> roleManager,
    UserManager<User> userManager,
    StorageService storageService,
    EmployeeCenterDbContext context)
    : Controller
{
    [HttpGet]
    [Route("api/users/search")]
    public async Task<IActionResult> Search(string query, int page = 1, int pageSize = 10)
    {
        var usersQuery = context.Users
            .Where(u => u.DisplayName.Contains(query) || u.UserName!.Contains(query) || u.Email!.Contains(query));

        var totalCount = await usersQuery.CountAsync();
        var users = await usersQuery
            .OrderBy(u => u.DisplayName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new
        {
            users = users.Select(u => new
            {
                id = u.Id,
                displayName = u.DisplayName,
                userName = u.UserName,
                avatarUrl = $"{storageService.RelativePathToInternetUrl(u.AvatarRelativePath)}?w=100&square=true"
            }),
            totalCount,
        });
    }

    [HttpGet]
    [Route("api/users/details/{id}")]
    public async Task<IActionResult> DetailsApi(string id)
    {
        var user = await context.Users.FindAsync(id);
        if (user == null) return NotFound();

        return Ok(new
        {
            id = user.Id,
            displayName = user.DisplayName,
            jobLevel = user.JobLevel,
            title = user.Title
        });
    }

    [Authorize(Policy = AppPermissionNames.CanReadUsers)]
    [RenderInNavBar(
        NavGroupName = "Administration",
        NavGroupOrder = 3,
        CascadedLinksGroupName = "Directory",
        CascadedLinksIcon = "users",
        CascadedLinksOrder = 3,
        LinkText = "Users",
        LinkOrder = 1)]
    public async Task<IActionResult> Index()
    {
        var allUsers = await context.Users.ToListAsync();
        var usersWithRoles = new List<UserWithRolesViewModel>();
        foreach (var user in allUsers)
        {
            usersWithRoles.Add(new UserWithRolesViewModel
            {
                User = user,
                Roles = await userManager.GetRolesAsync(user)
            });
        }

        return this.StackView(new IndexViewModel
        {
            Users = usersWithRoles
        });
    }

    [Authorize(Policy = AppPermissionNames.CanReadUsers)]
    public async Task<IActionResult> Details(string? id)
    {
        if (id == null) return NotFound();
        var user = await context.Users
            .Include(u => u.Manager)
            .Include(u => u.SigningEntity)
            .FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        var roleNames = await userManager.GetRolesAsync(user);
        var roles = await roleManager.Roles
            .Where(r => roleNames.Contains(r.Name!))
            .ToListAsync();

        var allPermissionValues = new HashSet<string>();
        foreach (var role in roles)
        {
            var claims = await roleManager.GetClaimsAsync(role);
            foreach (var claim in claims.Where(c => c.Type == AppPermissions.Type))
            {
                allPermissionValues.Add(claim.Value);
            }
        }

        var permissions = AppPermissions.GetAllPermissions()
            .Where(p => allPermissionValues.Contains(p.Key))
            .OrderBy(p => p.Name)
            .ToList();

        var changeLogs = await context.BankCardChangeLogs
            .Include(l => l.ChangedByUser)
            .Where(l => l.UserId == id)
            .OrderByDescending(l => l.ChangeTime)
            .ToListAsync();

        var assignedAssets = await context.Assets
            .Include(a => a.Model)
            .ThenInclude(m => m.Category)
            .Include(a => a.Location)
            .Where(a => a.AssigneeId == id)
            .ToListAsync();

        return this.StackView(new DetailsViewModel
        {
            User = user,
            Roles = roles,
            Permissions = permissions,
            BankCardChangeLogs = changeLogs,
            AssignedAssets = assignedAssets
        });
    }

    [Authorize(Policy = AppPermissionNames.CanAddUsers)]
    public async Task<IActionResult> Create()
    {
        return this.StackView(new CreateViewModel
        {
            AllCompanyEntities = await context.CompanyEntities.ToListAsync()
        });
    }

    [Authorize(Policy = AppPermissionNames.CanAddUsers)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateViewModel newUser)
    {
        if (ModelState.IsValid)
        {
            var user = new User
            {
                UserName = newUser.UserName,
                DisplayName = newUser.DisplayName,
                Email = newUser.Email,
                JobLevel = newUser.JobLevel,
                Title = newUser.Title,
                LegalName = newUser.LegalName,
                PhoneNumber = newUser.PhoneNumber,
                BaseSalary = newUser.BaseSalary,
                BankAccount = newUser.BankAccount,
                BankName = newUser.BankName,
                BankAccountName = newUser.BankAccountName,
                ManagerId = newUser.ManagerId,
                SigningEntityId = newUser.SigningEntityId,
                CreationTime = newUser.CreationTime,
                AvatarRelativePath = Aiursoft.EmployeeCenter.Entities.User.DefaultAvatarPath
            };
            var result = await userManager.CreateAsync(user, newUser.Password!);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                newUser.AllCompanyEntities = await context.CompanyEntities.ToListAsync();
                return this.StackView(newUser);
            }

            return RedirectToAction(nameof(Details), new { id = user.Id });
        }
        newUser.AllCompanyEntities = await context.CompanyEntities.ToListAsync();
        return this.StackView(newUser);
    }

    // GET: Users/Edit/5
    [Authorize(Policy = AppPermissionNames.CanEditAndViewDetailsOfUsers)]
    public async Task<IActionResult> Edit(string? id)
    {
        if (id == null) return NotFound();
        var user = await context.Users
            .Include(u => u.Manager)
            .FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        var userRoles = await userManager.GetRolesAsync(user);
        var allRoles = await roleManager.Roles.ToListAsync();

        var model = new EditViewModel
        {
            Id = id,
            Email = user.Email!,
            UserName = user.UserName!,
            DisplayName = user.DisplayName,
            Password = "you-cant-read-it",
            AvatarUrl = user.AvatarRelativePath,
            CreationTime = user.CreationTime,
            JobLevel = user.JobLevel,
            Title = user.Title,
            LegalName = user.LegalName,
            PhoneNumber = user.PhoneNumber,
            BaseSalary = user.BaseSalary,
            BankAccount = user.BankAccount,
            BankName = user.BankName,
            BankAccountName = user.BankAccountName,
            ManagerId = user.ManagerId,
            ManagerDisplayName = user.Manager?.DisplayName,
            SigningEntityId = user.SigningEntityId,
            AllCompanyEntities = await context.CompanyEntities.ToListAsync(),
            AllRoles = allRoles.Select(role => new UserRoleViewModel
            {
                RoleName = role.Name!,
                // 如果用户拥有该角色，则 IsSelected 为 true
                IsSelected = userRoles.Contains(role.Name!)
            }).ToList()
        };

        return this.StackView(model);
    }

    // POST: /Users/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanEditAndViewDetailsOfUsers)]
    public async Task<IActionResult> Edit(EditViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.AllCompanyEntities = await context.CompanyEntities.ToListAsync();
            return this.StackView(model);
        }
        var userInDb = await userManager.FindByIdAsync(model.Id);
        if (userInDb == null) return NotFound();

        userInDb.Email = model.Email;
        userInDb.UserName = model.UserName;
        userInDb.DisplayName = model.DisplayName;
        userInDb.AvatarRelativePath = model.AvatarUrl;
        userInDb.LegalName = model.LegalName;
        userInDb.PhoneNumber = model.PhoneNumber;
        userInDb.BaseSalary = model.BaseSalary;
        userInDb.CreationTime = model.CreationTime;

        if (userInDb.BankAccount != model.BankAccount ||
            userInDb.BankName != model.BankName ||
            userInDb.BankAccountName != model.BankAccountName)
        {
            var currentUser = await userManager.GetUserAsync(User);
            var log = new BankCardChangeLog
            {
                UserId = userInDb.Id,
                OldBankCardNumber = userInDb.BankAccount,
                NewBankCardNumber = model.BankAccount,
                OldBankName = userInDb.BankName,
                NewBankName = model.BankName,
                OldBankAccountName = userInDb.BankAccountName,
                NewBankAccountName = model.BankAccountName,
                ChangedByUserId = currentUser?.Id,
                ChangeTime = DateTime.UtcNow
            };
            context.BankCardChangeLogs.Add(log);

            userInDb.BankAccount = model.BankAccount;
            userInDb.BankName = model.BankName;
            userInDb.BankAccountName = model.BankAccountName;
        }

        if (userInDb.JobLevel != model.JobLevel || userInDb.Title != model.Title)
        {
            var currentUser = await userManager.GetUserAsync(User);
            var history = new PromotionHistory
            {
                UserId = userInDb.Id,
                OldJobLevel = userInDb.JobLevel,
                NewJobLevel = model.JobLevel,
                OldTitle = userInDb.Title,
                NewTitle = model.Title,
                ChangeTime = DateTime.UtcNow,
                PromoterId = currentUser?.Id
            };
            context.PromotionHistories.Add(history);

            userInDb.JobLevel = model.JobLevel;
            userInDb.Title = model.Title;
        }

        userInDb.ManagerId = model.ManagerId;
        userInDb.SigningEntityId = model.SigningEntityId;
        await userManager.UpdateAsync(userInDb);
        await context.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(model.Password) && model.Password != "you-cant-read-it")
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(userInDb);
            await userManager.ResetPasswordAsync(userInDb, token, model.Password);
        }

        return RedirectToAction(nameof(Details), new { id = userInDb.Id });
    }

    // POST: /Users/ManageRoles/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanAssignRoleToUser)]
    public async Task<IActionResult> ManageRoles(string id, EditViewModel model)
    {
        var userInDb = await userManager.FindByIdAsync(id);
        if (userInDb == null) return NotFound();

        var userCurrentRoles = await userManager.GetRolesAsync(userInDb);
        var selectedRoles = model
            .AllRoles
            .Where(r => r.IsSelected)
            .Select(r => r.RoleName)
            .ToArray();

        var rolesToAdd = selectedRoles.Except(userCurrentRoles);
        await userManager.AddToRolesAsync(userInDb, rolesToAdd);

        var rolesToRemove = userCurrentRoles.Except(selectedRoles);
        await userManager.RemoveFromRolesAsync(userInDb, rolesToRemove);

        return RedirectToAction(nameof(Details), new { id = userInDb.Id });
    }

    // GET: Users/Delete/5
    [Authorize(Policy = AppPermissionNames.CanDeleteUsers)]
    public async Task<IActionResult> Delete(string? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var user = await context.Users
            .FirstOrDefaultAsync(m => m.Id == id);
        if (user == null)
        {
            return NotFound();
        }

        return this.StackView(new DeleteViewModel
        {
            User = user,
        });
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanDeleteUsers)]
    public async Task<IActionResult> DeleteConfirmed(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }
        await userManager.DeleteAsync(user);
        return RedirectToAction(nameof(Index));
    }
}
