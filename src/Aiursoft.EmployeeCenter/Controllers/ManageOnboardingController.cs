using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.ManageOnboardingViewModels;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.UiStack.Navigation;
using Aiursoft.WebTools.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.EmployeeCenter.Controllers;

[Authorize(Policy = AppPermissionNames.CanManageOnboarding)]
[LimitPerMin]
public class ManageOnboardingController(
    EmployeeCenterDbContext context,
    IAuthorizationService authorizationService) : Controller
{
    [RenderInNavBar(
        NavGroupName = "Administration",
        NavGroupOrder = 3,
        CascadedLinksGroupName = "Legal",
        CascadedLinksIcon = "scale",
        CascadedLinksOrder = 5,
        LinkText = "Onboarding Process",
        LinkOrder = 3)]
    public async Task<IActionResult> Index()
    {
        var tasks = await context.OnboardingTasks
            .OrderBy(t => t.Order)
            .ToListAsync();

        var model = new IndexViewModel { Tasks = tasks };
        var canReadUsers = await authorizationService.AuthorizeAsync(User, AppPermissionNames.CanReadUsers);
        if (canReadUsers.Succeeded)
        {
            var users = await context.Users
                .Include(u => u.OnboardingTaskLogs)
                .ToListAsync();

            model.EmployeeProgresses = users.Select(u => new EmployeeProgress
            {
                User = u,
                TotalTasksCount = tasks.Count,
                CompletedTasksCount = u.OnboardingTaskLogs
                    .Where(l => l.CompletionTime != null)
                    .Select(l => l.TaskId)
                    .Distinct()
                    .Count()
            })
            .OrderByDescending(p => p.ProgressPercentage)
            .ThenBy(p => p.User.DisplayName)
            .ToList();
        }

        return this.StackView(model);
    }

    public IActionResult Create()
    {
        return this.StackView(new CreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateViewModel model)
    {
        if (ModelState.IsValid)
        {
            var task = new OnboardingTask
            {
                Order = model.Order,
                ExpectedDurationSeconds = model.ExpectedDurationSeconds,
                Title = model.Title,
                Description = model.Description,
                StartLink = model.StartLink
            };
            context.OnboardingTasks.Add(task);
            await context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return this.StackView(model);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var task = await context.OnboardingTasks.FindAsync(id);
        if (task == null) return NotFound();
        return this.StackView(new EditViewModel
        {
            Id = task.Id,
            Order = task.Order,
            ExpectedDurationSeconds = task.ExpectedDurationSeconds,
            Title = task.Title,
            Description = task.Description,
            StartLink = task.StartLink
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditViewModel model)
    {
        if (ModelState.IsValid)
        {
            var task = await context.OnboardingTasks.FindAsync(model.Id);
            if (task == null) return NotFound();
            task.Order = model.Order;
            task.ExpectedDurationSeconds = model.ExpectedDurationSeconds;
            task.Title = model.Title;
            task.Description = model.Description;
            task.StartLink = model.StartLink;
            await context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return this.StackView(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var task = await context.OnboardingTasks.FindAsync(id);
        if (task == null) return NotFound();
        context.OnboardingTasks.Remove(task);
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
