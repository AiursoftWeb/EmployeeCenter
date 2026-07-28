using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.RequirementViewModels;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.UiStack.Navigation;
using Ganss.Xss;
using Markdig;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Microsoft.Extensions.Localization;

namespace Aiursoft.EmployeeCenter.Controllers;

[Authorize]
public class RequirementsController(
    EmployeeCenterDbContext dbContext,
    UserManager<User> userManager,
    MarkdownPipeline pipeline,
    HtmlSanitizer sanitizer,
    IStringLocalizer<RequirementsController> localizer) : Controller
{
    [HttpGet]
    [RenderInNavBar(
        NavGroupName = "Career",
        NavGroupOrder = 1,
        CascadedLinksGroupName = "Project Requirements",
        CascadedLinksIcon = "clipboard-list",
        CascadedLinksOrder = 3,
        LinkText = "Approved Projects",
        LinkOrder = 1)]
    public async Task<IActionResult> Index()
    {
        var requirements = await dbContext.Requirements
            .Include(t => t.Submitter)
            .Where(t => t.Status == RequirementStatus.Approved)
            .OrderByDescending(t => t.CreationTime)
            .ToListAsync();

        var model = new IndexViewModel
        {
            Requirements = requirements,
            PageTitle = localizer["Approved Projects"]
        };
        return this.StackView(model, "Index");
    }

    [HttpGet]
    [RenderInNavBar(
        NavGroupName = "Career",
        NavGroupOrder = 1,
        CascadedLinksGroupName = "Project Requirements",
        CascadedLinksIcon = "clipboard-list",
        CascadedLinksOrder = 3,
        LinkText = "My Requirements",
        LinkOrder = 2)]
    public async Task<IActionResult> My()
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var requirements = await dbContext.Requirements
            .Include(t => t.Submitter)
            .Where(t => t.SubmitterId == user.Id)
            .OrderByDescending(t => t.CreationTime)
            .ToListAsync();

        var model = new IndexViewModel
        {
            Requirements = requirements,
            PageTitle = localizer["My Requirements"]
        };
        return this.StackView(model, "Index");
    }

    [HttpGet]
    [RenderInNavBar(
        NavGroupName = "Career",
        NavGroupOrder = 1,
        CascadedLinksGroupName = "Project Requirements",
        CascadedLinksIcon = "clipboard-list",
        CascadedLinksOrder = 3,
        LinkText = "Approval History",
        LinkOrder = 3)]
    public async Task<IActionResult> History()
    {
        var requirements = await dbContext.Requirements
            .Include(t => t.Submitter)
            .OrderByDescending(t => t.UpdateTime)
            .ToListAsync();

        var model = new IndexViewModel
        {
            Requirements = requirements,
            PageTitle = localizer["Approval History"]
        };
        return this.StackView(model, "Index");
    }

    [HttpGet]
    [Authorize(Policy = AppPermissionNames.CanApproveProjectRequirements)]
    [RenderInNavBar(
        NavGroupName = "Career",
        NavGroupOrder = 1,
        CascadedLinksGroupName = "Project Requirements",
        CascadedLinksIcon = "clipboard-list",
        CascadedLinksOrder = 3,
        LinkText = "Manage Requirements",
        LinkOrder = 4)]
    public async Task<IActionResult> Manage()
    {
        var requirements = await dbContext.Requirements
            .Include(t => t.Submitter)
            .Where(t => t.Status == RequirementStatus.PendingApproval)
            .OrderByDescending(t => t.CreationTime)
            .ToListAsync();

        var model = new IndexViewModel
        {
            Requirements = requirements,
            PageTitle = localizer["Manage Requirements"]
        };
        return this.StackView(model, "Index");
    }

    [HttpGet]
    public IActionResult Create()
    {
        return this.StackView(new EditorViewModel
        {
            PageTitle = localizer["Submit Requirement"]
        }, "Editor");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EditorViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.PageTitle = localizer["Submit Requirement"];
            return this.StackView(model, "Editor");
        }

        var user = await userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var requirement = new Requirement
        {
            Title = model.Title,
            Content = model.InputMarkdown,
            SubmitterId = user.Id,
            Status = RequirementStatus.PendingApproval,
            CreationTime = DateTime.UtcNow,
            UpdateTime = DateTime.UtcNow
        };

        dbContext.Requirements.Add(requirement);
        await dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(View), new { id = requirement.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var requirement = await dbContext.Requirements.FindAsync(id);
        if (requirement == null) return NotFound();

        var user = await userManager.GetUserAsync(User);
        var canManage = User.HasClaim(AppPermissions.Type, AppPermissionNames.CanApproveProjectRequirements);
        var isCreator = requirement.SubmitterId == user?.Id;

        if (!canManage && !isCreator) return Unauthorized();

        if (!canManage && requirement.Status == RequirementStatus.Approved)
        {
            return BadRequest(localizer["Cannot edit a requirement that is already approved."]);
        }

        var model = new EditorViewModel
        {
            RequirementId = requirement.Id,
            Title = requirement.Title,
            InputMarkdown = requirement.Content,
            PageTitle = localizer["Edit Requirement"]
        };
        return this.StackView(model, "Editor");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditorViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.PageTitle = localizer["Edit Requirement"];
            return this.StackView(model, "Editor");
        }

        var requirement = await dbContext.Requirements.FindAsync(model.RequirementId);
        if (requirement == null) return NotFound();

        var user = await userManager.GetUserAsync(User);
        var canManage = User.HasClaim(AppPermissions.Type, AppPermissionNames.CanApproveProjectRequirements);
        var isCreator = requirement.SubmitterId == user?.Id;

        if (!canManage && !isCreator) return Unauthorized();

        if (!canManage && requirement.Status == RequirementStatus.Approved)
        {
            return BadRequest(localizer["Cannot edit a requirement that is already approved."]);
        }

        requirement.Title = model.Title;
        requirement.Content = model.InputMarkdown;
        if (!canManage)
        {
            requirement.Status = RequirementStatus.PendingApproval; // Re-submit for approval
        }
        requirement.UpdateTime = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(View), new { id = requirement.Id });
    }

    [HttpGet]
    public async Task<IActionResult> View(int id)
    {
        var requirement = await dbContext.Requirements
            .Include(t => t.Submitter)
            .Include(t => t.Comments)
                .ThenInclude(c => c.Author)
            .Include(t => t.Comments)
                .ThenInclude(c => c.Replies)
                    .ThenInclude(r => r.Author)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (requirement == null) return NotFound();

        var model = new ReaderViewModel
        {
            Requirement = requirement,
            PageTitle = requirement.Title
        };
        return this.StackView(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Comment(int id, string content, int? replyToId)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return RedirectToAction(nameof(View), new { id });
        }

        var user = await userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var comment = new RequirementComment
        {
            RequirementId = id,
            AuthorId = user.Id,
            Content = content,
            ParentCommentId = replyToId,
            CreateTime = DateTime.UtcNow
        };

        dbContext.RequirementComments.Add(comment);
        await dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(View), new { id });
    }

    [HttpPost]
    [Authorize(Policy = AppPermissionNames.CanApproveProjectRequirements)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id)
    {
        var requirement = await dbContext.Requirements.FindAsync(id);
        if (requirement == null) return NotFound();

        requirement.Status = RequirementStatus.Approved;
        requirement.UpdateTime = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(Manage));
    }

    [HttpPost]
    [Authorize(Policy = AppPermissionNames.CanApproveProjectRequirements)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id)
    {
        var requirement = await dbContext.Requirements.FindAsync(id);
        if (requirement == null) return NotFound();

        requirement.Status = RequirementStatus.Rejected;
        requirement.UpdateTime = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(Manage));
    }

    [HttpPost]
    [Authorize(Policy = AppPermissionNames.CanApproveProjectRequirements)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestChanges(int id)
    {
        var requirement = await dbContext.Requirements.FindAsync(id);
        if (requirement == null) return NotFound();

        requirement.Status = RequirementStatus.RequestChanges;
        requirement.UpdateTime = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(Manage));
    }
}
