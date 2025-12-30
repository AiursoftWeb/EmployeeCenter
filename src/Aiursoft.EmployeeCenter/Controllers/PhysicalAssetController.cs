using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.WebTools;
using Aiursoft.WebTools.Attributes;
using Aiursoft.UiStack.Navigation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.EmployeeCenter.Controllers;

[Authorize]
public class PhysicalAssetController : Controller
{
    private readonly PhysicalAssetService _physicalAssetService;
    private readonly TemplateDbContext _dbContext;
    private readonly UserManager<User> _userManager;

    public PhysicalAssetController(
        PhysicalAssetService physicalAssetService,
        TemplateDbContext dbContext,
        UserManager<User> userManager)
    {
        _physicalAssetService = physicalAssetService;
        _dbContext = dbContext;
        _userManager = userManager;
    }

    [RenderInNavBar(
        NavGroupName = "Features",
        NavGroupOrder = 2,
        CascadedLinksGroupName = "Shared Info",
        CascadedLinksIcon = "box",
        CascadedLinksOrder = 3,
        LinkText = "Physical Assets",
        LinkOrder = 2)]
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var assets = await _dbContext.PhysicalAssets
            .Include(t => t.Usages)
            .OrderBy(t => t.Name)
            .ToListAsync();

        var myUsages = await _dbContext.PhysicalAssetUsages
            .Include(u => u.Asset)
            .Where(u => u.UserId == user.Id)
            .OrderByDescending(u => u.ApplyTime)
            .ToListAsync();

        return this.StackView(new Aiursoft.EmployeeCenter.Models.PhysicalAssetViewModels.IndexViewModel
        {
            Assets = assets,
            MyUsages = myUsages
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Apply(Guid assetId, string remark)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        try
        {
            await _physicalAssetService.ApplyAsync(user.Id, assetId, remark);
            TempData["Message"] = "Application submitted successfully.";
        }
        catch (Exception e)
        {
            TempData["Error"] = e.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // [Authorize(Roles = "Admin")] // TODO: Add Role check
    public async Task<IActionResult> Approve(Guid usageId, string assignedSerialNumber)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        try
        {
            await _physicalAssetService.ApproveAsync(user.Id, usageId, assignedSerialNumber);
            TempData["Message"] = "Approved successfully.";
        }
        catch (Exception e)
        {
            TempData["Error"] = e.Message;
        }

        return RedirectToAction(nameof(Index)); // Or a Manage view
    }

    [Authorize(Policy = AppPermissionNames.CanViewSystemContext)]
    public IActionResult Create()
    {
        return this.StackView(new Aiursoft.EmployeeCenter.Models.PhysicalAssetViewModels.CreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanViewSystemContext)]
    public async Task<IActionResult> Create(Aiursoft.EmployeeCenter.Models.PhysicalAssetViewModels.CreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return this.StackView(model);
        }

        var asset = new PhysicalAsset
        {
            Name = model.Name!,
            TotalStock = model.TotalStock,
            FrozenStock = 0,
            UsedStock = 0
        };

        _dbContext.PhysicalAssets.Add(asset);
        await _dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Return(Guid usageId, string remark)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        try
        {
            await _physicalAssetService.ReturnAsync(user.Id, usageId, remark);
            TempData["Message"] = "Returned successfully. Pending inspection.";
        }
        catch (Exception e)
        {
            TempData["Error"] = e.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Print(Guid id)
    {
        var asset = await _dbContext.PhysicalAssets.FindAsync(id);
        if (asset == null) return NotFound();
        return View(asset);
    }
}
