using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.CustomerRelationshipViewModels;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.UiStack.Navigation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

using Microsoft.Extensions.Localization;

namespace Aiursoft.EmployeeCenter.Controllers;

[Authorize]
public class CustomerRelationshipsController(
    EmployeeCenterDbContext dbContext,
    IStringLocalizer<CustomerRelationshipsController> localizer) : Controller
{
    [HttpGet]
    [RenderInNavBar(
        NavGroupName = "Administration",
        NavGroupOrder = 3,
        CascadedLinksGroupName = "Customer Relations",
        CascadedLinksIcon = "users",
        CascadedLinksOrder = 6,
        LinkText = "View Customer Relations",
        LinkOrder = 1)]
    public async Task<IActionResult> Index()
    {
        var relationships = await dbContext.CustomerRelationships
            .Include(t => t.CompanyEntity)
            .OrderByDescending(t => t.CreationTime)
            .ToListAsync();
        return this.StackView(new IndexViewModel
        {
            CustomerRelationships = relationships,
            PageTitle = localizer["View Customer Relations"]
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var relationship = await dbContext.CustomerRelationships
            .Include(t => t.CompanyEntity)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (relationship == null) return NotFound();

        var model = new EditorViewModel
        {
            Id = relationship.Id,
            CompanyEntityId = relationship.CompanyEntityId,
            Name = relationship.Name,
            JobTitle = relationship.JobTitle,
            Email = relationship.Email,
            Phone = relationship.Phone,
            Address = relationship.Address,
            Remark = relationship.Remark,
            AvailableCompanyEntities = [], // Not needed for details view
            PageTitle = localizer["Customer Relationship Details"]
        };
        return this.StackView(model);
    }

    [HttpGet]
    [Authorize(Policy = AppPermissionNames.CanManageCustomerRelationships)]
    [RenderInNavBar(
        NavGroupName = "Administration",
        NavGroupOrder = 3,
        CascadedLinksGroupName = "Customer Relations",
        CascadedLinksIcon = "users",
        CascadedLinksOrder = 6,
        LinkText = "Manage Customer Relations",
        LinkOrder = 2)]
    public async Task<IActionResult> Manage()
    {
        var relationships = await dbContext.CustomerRelationships
            .Include(t => t.CompanyEntity)
            .OrderByDescending(t => t.CreationTime)
            .ToListAsync();
        return this.StackView(new IndexViewModel
        {
            CustomerRelationships = relationships,
            PageTitle = localizer["Manage Customer Relations"]
        });
    }

    [HttpGet]
    [Authorize(Policy = AppPermissionNames.CanManageCustomerRelationships)]
    public async Task<IActionResult> Create()
    {
        var model = new EditorViewModel
        {
            AvailableCompanyEntities = await GetCompanyEntities(),
            PageTitle = localizer["Create Customer Relationship"]
        };
        return this.StackView(model, "Editor");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanManageCustomerRelationships)]
    public async Task<IActionResult> Create(EditorViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.PageTitle = localizer["Create Customer Relationship"];
            model.AvailableCompanyEntities = await GetCompanyEntities();
            return this.StackView(model, "Editor");
        }

        var relationship = new CustomerRelationship
        {
            CompanyEntityId = model.CompanyEntityId,
            Name = model.Name,
            JobTitle = model.JobTitle,
            Email = model.Email,
            Phone = model.Phone,
            Address = model.Address,
            Remark = model.Remark, // Markdown
            CreationTime = DateTime.UtcNow,
            UpdateTime = DateTime.UtcNow
        };

        dbContext.CustomerRelationships.Add(relationship);
        await dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(Manage));
    }

    [HttpGet]
    [Authorize(Policy = AppPermissionNames.CanManageCustomerRelationships)]
    public async Task<IActionResult> Edit(int id)
    {
        var relationship = await dbContext.CustomerRelationships.FindAsync(id);
        if (relationship == null) return NotFound();

        var model = new EditorViewModel
        {
            Id = relationship.Id,
            CompanyEntityId = relationship.CompanyEntityId,
            Name = relationship.Name,
            JobTitle = relationship.JobTitle,
            Email = relationship.Email,
            Phone = relationship.Phone,
            Address = relationship.Address,
            Remark = relationship.Remark,
            AvailableCompanyEntities = await GetCompanyEntities(),
            PageTitle = localizer["Edit Customer Relationship"]
        };
        return this.StackView(model, "Editor");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanManageCustomerRelationships)]
    public async Task<IActionResult> Edit(EditorViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.PageTitle = localizer["Edit Customer Relationship"];
            model.AvailableCompanyEntities = await GetCompanyEntities();
            return this.StackView(model, "Editor");
        }

        var relationship = await dbContext.CustomerRelationships.FindAsync(model.Id);
        if (relationship == null) return NotFound();

        relationship.CompanyEntityId = model.CompanyEntityId;
        relationship.Name = model.Name;
        relationship.JobTitle = model.JobTitle;
        relationship.Email = model.Email;
        relationship.Phone = model.Phone;
        relationship.Address = model.Address;
        relationship.Remark = model.Remark;
        relationship.UpdateTime = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(Manage));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanManageCustomerRelationships)]
    public async Task<IActionResult> Delete(int id)
    {
        var relationship = await dbContext.CustomerRelationships.FindAsync(id);
        if (relationship == null) return NotFound();

        dbContext.CustomerRelationships.Remove(relationship);
        await dbContext.SaveChangesAsync();
        return RedirectToAction(nameof(Manage));
    }

    private async Task<IEnumerable<SelectListItem>> GetCompanyEntities()
    {
        var entities = await dbContext.CompanyEntities
            .OrderBy(c => c.CompanyName)
            .Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.CompanyName
            })
            .ToListAsync();
        return entities;
    }
}
