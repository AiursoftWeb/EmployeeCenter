using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.ReportLineViewModels;
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
public class ReportLineController(
    UserManager<User> userManager,
    IAuthorizationService authorizationService,
    EmployeeCenterDbContext context)
    : Controller
{
    [RenderInNavBar(
        NavGroupName = "Career",
        NavGroupOrder = 1,
        CascadedLinksGroupName = "Report Line",
        CascadedLinksIcon = "network",
        CascadedLinksOrder = 4,
        LinkText = "My Report Line",
        LinkOrder = 1)]
    public async Task<IActionResult> Index(string? id)
    {
        var currentUser = await userManager.GetUserAsync(User);
        if (currentUser == null) return Unauthorized();

        var targetUserId = id ?? currentUser.Id;
        var isViewingSelf = targetUserId == currentUser.Id;

        if (!isViewingSelf)
        {
            var canViewOthers = (await authorizationService.AuthorizeAsync(User, AppPermissionNames.CanViewReportLine)).Succeeded;
            if (!canViewOthers)
            {
                return Unauthorized();
            }
        }

        var targetUser = await context.Users
            .Include(u => u.Manager)
            .FirstOrDefaultAsync(u => u.Id == targetUserId);

        if (targetUser == null) return NotFound();

        // Build manager chain
        var managerChain = new List<User>();
        var current = targetUser;
        var visited = new HashSet<string> { targetUser.Id };
        string? error = null;

        while (current.ManagerId != null)
        {
            if (visited.Contains(current.ManagerId))
            {
                // Circular dependency detected
                error = "Circular dependency detected in report line!";
                break;
            }
            visited.Add(current.ManagerId);
            var manager = await context.Users
                .Include(u => u.Manager)
                .FirstOrDefaultAsync(u => u.Id == current.ManagerId);

            if (manager == null) break;
            managerChain.Add(manager);
            current = manager;
        }

        var model = new IndexViewModel
        {
            TargetUser = targetUser,
            ManagerChain = managerChain,
            IsViewingSelf = isViewingSelf,
            Error = error
        };

        if (!isViewingSelf || (await authorizationService.AuthorizeAsync(User, AppPermissionNames.CanViewReportLine)).Succeeded)
        {
            model.Subordinates = await context.Users
                .Where(u => u.ManagerId == targetUser.Id)
                .ToListAsync();

            if (targetUser.ManagerId != null)
            {
                model.Peers = await context.Users
                    .Where(u => u.ManagerId == targetUser.ManagerId && u.Id != targetUser.Id)
                    .ToListAsync();
            }
        }

        return this.StackView(model);
    }
}
