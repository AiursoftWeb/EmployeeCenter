using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.IntangibleAssetsViewModels;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.UiStack.Navigation;
using Aiursoft.WebTools.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.EmployeeCenter.Controllers;

[Authorize]
[LimitPerMin]
public class CompanyIntangibleAssetsController(
    EmployeeCenterDbContext context)
    : Controller
{
    [RenderInNavBar(
        NavGroupName = "Career",
        NavGroupOrder = 1,
        CascadedLinksGroupName = "Resources",
        CascadedLinksIcon = "briefcase",
        CascadedLinksOrder = 6,
        LinkText = "Company Intangible Assets",
        LinkOrder = 2)]
    public async Task<IActionResult> Index()
    {
        var user = await context.Users
            .FirstOrDefaultAsync(u => u.UserName == User.Identity!.Name);
        
        var publicAssets = await context.IntangibleAssets
            .Include(a => a.Assignee)
            .Include(a => a.CompanyEntity)
            .Where(a => a.IsPublic)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        var myAssets = await context.IntangibleAssets
            .Include(a => a.Assignee)
            .Include(a => a.CompanyEntity)
            .Where(a => a.AssigneeId == user!.Id)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        return this.StackView(new IndexViewModel
        {
            Assets = publicAssets,
            MyAssets = myAssets
        });
    }

    public async Task<IActionResult> Details(Guid id)
    {
        var user = await context.Users
            .FirstOrDefaultAsync(u => u.UserName == User.Identity!.Name);
        
        var asset = await context.IntangibleAssets
            .Include(a => a.Assignee)
            .Include(a => a.CompanyEntity)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (asset == null) return NotFound();
        if (!asset.IsPublic && asset.AssigneeId != user?.Id) return NotFound();

        return this.StackView(new DetailsViewModel
        {
            Asset = asset
        });
    }
}
