using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.DashboardViewModels;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.UiStack.Navigation;
using Aiursoft.WebTools.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Microsoft.Extensions.Localization;

namespace Aiursoft.EmployeeCenter.Controllers;

[LimitPerMin]
[Authorize]
public class DashboardController(
    EmployeeCenterDbContext context,
    UserManager<User> userManager,
    IStringLocalizer<DashboardController> localizer) : Controller
{
    [RenderInNavBar(
        NavGroupName = "Career",
        NavGroupOrder = 1,
        CascadedLinksGroupName = "Onboarding",
        CascadedLinksIcon = "home",
        CascadedLinksOrder = 1,
        LinkText = "Missions",
        LinkOrder = 1)]
    public async Task<IActionResult> Index()
    {
        var user = await userManager.Users
            .Include(u => u.SigningEntity)
            .FirstOrDefaultAsync(u => u.Id == userManager.GetUserId(User));
        var tasks = await context.OnboardingTasks
            .OrderBy(t => t.Order)
            .ToListAsync();

        var logs = await context.OnboardingTaskLogs
            .Where(l => l.UserId == user!.Id)
            .ToListAsync();

        return this.StackView(new IndexViewModel
        {
            Tasks = tasks,
            Logs = logs,
            User = user!,
            PageTitle = localizer["Onboarding Wizard"]
        });
    }
}
