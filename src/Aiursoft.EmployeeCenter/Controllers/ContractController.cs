using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.ContractViewModels;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.UiStack.Navigation;
using Aiursoft.WebTools.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.EmployeeCenter.Controllers;

[Authorize]
[LimitPerMin]
public class ContractController(
    IAuthorizationService authorizationService,
    EmployeeCenterDbContext context)
    : Controller
{
    [RenderInNavBar(
        NavGroupName = "Career",
        NavGroupOrder = 1,
        CascadedLinksGroupName = "Resources",
        CascadedLinksIcon = "briefcase",
        CascadedLinksOrder = 6,
        LinkText = "Company Public Contracts",
        LinkOrder = 1)]
    public async Task<IActionResult> Index(int? id)
    {
        var canViewHistory = (await authorizationService.AuthorizeAsync(User, AppPermissionNames.CanViewContractHistory)).Succeeded;

        var currentFolder = id.HasValue
            ? await context.ContractFolders.FindAsync(id.Value)
            : null;

        var contracts = await context.Contracts
            .Where(c => c.FolderId == id)
            .Where(c => canViewHistory || c.IsPublic)
            .OrderByDescending(c => c.CreateTime)
            .ToListAsync();

        var subFolders = await context.ContractFolders
            .Where(f => f.ParentFolderId == id)
            .OrderBy(f => f.Name)
            .ToListAsync();

        return this.StackView(new IndexViewModel
        {
            FolderId = id,
            CurrentFolder = currentFolder,
            SubFolders = subFolders,
            Contracts = contracts
        });
    }

    public async Task<IActionResult> Preview(int id)
    {
        var canViewHistory = (await authorizationService.AuthorizeAsync(User, AppPermissionNames.CanViewContractHistory)).Succeeded;

        var contract = await context.Contracts
            .Include(c => c.CollectionChannels)
            .ThenInclude(cc => cc.Payer)
            .Include(c => c.CollectionChannels)
            .ThenInclude(cc => cc.Payee)
            .FirstOrDefaultAsync(c => c.Id == id && (c.IsPublic || canViewHistory));

        if (contract == null)
        {
            return NotFound();
        }

        var ocrResult = await context.ContractOcrResults
            .FirstOrDefaultAsync(r => r.ContractId == id);

        return this.StackView(new OcrPreviewViewModel
        {
            Contract = contract,
            PlainText = ocrResult?.PlainText,
            JsonResult = ocrResult?.JsonResult
        });
    }
}
