using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.BlueprintViewModels;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.UiStack.Navigation;
using Ganss.Xss;
using Markdig;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Aiursoft.EmployeeCenter.Controllers;

[Authorize]
public class BlueprintController(
    EmployeeCenterDbContext dbContext,
    UserManager<User> userManager,
    MarkdownPipeline pipeline,
    HtmlSanitizer sanitizer,
    IStringLocalizer<BlueprintController> localizer) : Controller
{
    private async Task<List<SelectListItem>> GetFolderSelectList(int? selectedId, int? excludeId = null)
    {
        var folders = await dbContext.BlueprintFolders.ToListAsync();
        return folders
            .Where(f => f.Id != excludeId)
            .Select(f => new SelectListItem
            {
                Text = f.Name,
                Value = f.Id.ToString(),
                Selected = f.Id == selectedId
            })
            .ToList();
    }

    [HttpGet]
    [RenderInNavBar(
        NavGroupName = "Career",
        NavGroupOrder = 1,
        CascadedLinksGroupName = "Blueprints",
        CascadedLinksIcon = "map",
        CascadedLinksOrder = 2,
        LinkText = "View Blueprints",
        LinkOrder = 1)]
    public async Task<IActionResult> Index(int? id)
    {
        var currentFolder = id.HasValue
            ? await dbContext.BlueprintFolders.FindAsync(id.Value)
            : null;

        var blueprints = await dbContext.Blueprints
            .Include(t => t.Author)
            .Where(t => t.FolderId == id)
            .OrderByDescending(t => t.CreationTime)
            .ToListAsync();

        var subFolders = await dbContext.BlueprintFolders
            .Where(f => f.ParentFolderId == id)
            .OrderBy(f => f.Name)
            .ToListAsync();

        return this.StackView(new IndexViewModel
        {
            FolderId = id,
            CurrentFolder = currentFolder,
            SubFolders = subFolders,
            Blueprints = blueprints,
            PageTitle = localizer["View Blueprints"]
        });
    }

    [HttpGet]
    [Authorize(Policy = AppPermissionNames.CanManageBlueprints)]
    [RenderInNavBar(
        NavGroupName = "Career",
        NavGroupOrder = 1,
        CascadedLinksGroupName = "Blueprints",
        CascadedLinksIcon = "map",
        CascadedLinksOrder = 2,
        LinkText = "Manage Blueprints",
        LinkOrder = 2)]
    public async Task<IActionResult> Manage(int? id)
    {
        var currentFolder = id.HasValue
            ? await dbContext.BlueprintFolders.FindAsync(id.Value)
            : null;

        var blueprints = await dbContext.Blueprints
            .Include(t => t.Author)
            .Where(t => t.FolderId == id)
            .OrderByDescending(t => t.CreationTime)
            .ToListAsync();

        var subFolders = await dbContext.BlueprintFolders
            .Where(f => f.ParentFolderId == id)
            .OrderBy(f => f.Name)
            .ToListAsync();

        return this.StackView(new IndexViewModel
        {
            FolderId = id,
            CurrentFolder = currentFolder,
            SubFolders = subFolders,
            Blueprints = blueprints,
            PageTitle = localizer["Manage Blueprints"]
        });
    }

    [HttpGet]
    [Authorize(Policy = AppPermissionNames.CanManageBlueprints)]
    public async Task<IActionResult> Create(int? folderId)
    {
        ViewData["Folders"] = await GetFolderSelectList(folderId);
        return this.StackView(new EditorViewModel
        {
            FolderId = folderId,
            PageTitle = localizer["Create Blueprint"]
        }, "Editor");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanManageBlueprints)]
    public async Task<IActionResult> Create(EditorViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.PageTitle = localizer["Create Blueprint"];
            return this.StackView(model, "Editor");
        }

        var user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        var blueprint = new Blueprint
        {
            Title = model.Title,
            Content = model.InputMarkdown,
            AuthorId = user.Id,
            CreationTime = DateTime.UtcNow,
            UpdateTime = DateTime.UtcNow,
            FolderId = model.FolderId
        };

        dbContext.Blueprints.Add(blueprint);
        await dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(Edit), new { id = blueprint.Id, saved = true });
    }

    [Authorize(Policy = AppPermissionNames.CanManageBlueprints)]
    public IActionResult CreateFolder(int? id)
    {
        return this.StackView(new CreateFolderViewModel { ParentFolderId = id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanManageBlueprints)]
    public async Task<IActionResult> CreateFolder(CreateFolderViewModel model)
    {
        if (ModelState.IsValid)
        {
            var folder = new BlueprintFolder
            {
                Name = model.Name,
                ParentFolderId = model.ParentFolderId
            };
            dbContext.BlueprintFolders.Add(folder);
            await dbContext.SaveChangesAsync();
            return RedirectToAction(nameof(Manage), new { id = model.ParentFolderId });
        }
        return this.StackView(model);
    }

    [HttpGet]
    [Authorize(Policy = AppPermissionNames.CanManageBlueprints)]
    public async Task<IActionResult> Edit(int id, bool saved = false)
    {
        var blueprint = await dbContext.Blueprints.FindAsync(id);
        if (blueprint == null) return NotFound();

        ViewData["Folders"] = await GetFolderSelectList(blueprint.FolderId);
        var model = new EditorViewModel
        {
            DocumentId = blueprint.Id,
            Title = blueprint.Title,
            InputMarkdown = blueprint.Content,
            SavedSuccessfully = saved,
            FolderId = blueprint.FolderId,
            PageTitle = localizer["Edit Blueprint"]
        };
        return this.StackView(model, "Editor");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanManageBlueprints)]
    public async Task<IActionResult> Edit(EditorViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.PageTitle = localizer["Edit Blueprint"];
            ViewData["Folders"] = await GetFolderSelectList(model.FolderId);
            return this.StackView(model, "Editor");
        }

        var blueprint = await dbContext.Blueprints.FindAsync(model.DocumentId);
        if (blueprint == null) return NotFound();

        blueprint.Title = model.Title;
        blueprint.Content = model.InputMarkdown;
        blueprint.UpdateTime = DateTime.UtcNow;
        blueprint.FolderId = model.FolderId;

        await dbContext.SaveChangesAsync();

        model.SavedSuccessfully = true;
        model.PageTitle = localizer["Edit Blueprint"];
        ViewData["Folders"] = await GetFolderSelectList(model.FolderId);

        return this.StackView(model, "Editor");
    }

    [Authorize(Policy = AppPermissionNames.CanManageBlueprints)]
    public async Task<IActionResult> EditFolder(int id)
    {
        var folder = await dbContext.BlueprintFolders.FindAsync(id);
        if (folder == null) return NotFound();

        ViewData["Folders"] = await GetFolderSelectList(folder.ParentFolderId, excludeId: folder.Id);
        return this.StackView(new EditFolderViewModel
        {
            Id = folder.Id,
            Name = folder.Name,
            ParentFolderId = folder.ParentFolderId
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanManageBlueprints)]
    public async Task<IActionResult> EditFolder(EditFolderViewModel model)
    {
        if (ModelState.IsValid)
        {
            var folder = await dbContext.BlueprintFolders.FindAsync(model.Id);
            if (folder == null) return NotFound();

            if (await IsFolderChildOf(folder.Id, model.ParentFolderId))
            {
                ModelState.AddModelError(nameof(model.ParentFolderId), "Cannot move a folder to its own child!");
                ViewData["Folders"] = await GetFolderSelectList(model.ParentFolderId, excludeId: folder.Id);
                return this.StackView(model);
            }

            folder.Name = model.Name;
            folder.ParentFolderId = model.ParentFolderId;
            await dbContext.SaveChangesAsync();
            return RedirectToAction(nameof(Manage), new { id = folder.ParentFolderId });
        }
        ViewData["Folders"] = await GetFolderSelectList(model.ParentFolderId, excludeId: model.Id);
        return this.StackView(model);
    }

    private async Task<bool> IsFolderChildOf(int sourceFolderId, int? targetFolderId)
    {
        if (targetFolderId == null) return false;
        if (sourceFolderId == targetFolderId) return true;

        var targetFolder = await dbContext.BlueprintFolders.FindAsync(targetFolderId.Value);
        if (targetFolder == null) return false;

        return await IsFolderChildOf(sourceFolderId, targetFolder.ParentFolderId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanManageBlueprints)]
    public async Task<IActionResult> Delete(int id)
    {
        var blueprint = await dbContext.Blueprints.FindAsync(id);
        if (blueprint == null) return NotFound();

        var folderId = blueprint.FolderId;
        dbContext.Blueprints.Remove(blueprint);
        await dbContext.SaveChangesAsync();
        return RedirectToAction(nameof(Manage), new { id = folderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanManageBlueprints)]
    public async Task<IActionResult> DeleteFolder(int id)
    {
        var folder = await dbContext.BlueprintFolders
            .Include(f => f.SubFolders)
            .Include(f => f.Blueprints)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (folder != null)
        {
            if (folder.SubFolders.Any() || folder.Blueprints.Any())
            {
                return BadRequest("Folder is not empty.");
            }
            var parentId = folder.ParentFolderId;
            dbContext.BlueprintFolders.Remove(folder);
            await dbContext.SaveChangesAsync();
            return RedirectToAction(nameof(Manage), new { id = parentId });
        }
        return NotFound();
    }

    [HttpGet]
    public async Task<IActionResult> View(int id)
    {
        var blueprint = await dbContext.Blueprints
            .Include(t => t.Author)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (blueprint == null) return NotFound();

        return this.StackView(new ReaderViewModel
        {
            Blueprint = blueprint,
            PageTitle = blueprint.Title
        });
    }
}
