using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.ContractViewModels;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.UiStack.Navigation;
using Aiursoft.WebTools.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.EmployeeCenter.Controllers;

[Authorize(Policy = AppPermissionNames.CanViewContractHistory)]
[LimitPerMin]
public class ManageContractController(
    EmployeeCenterDbContext context)
    : Controller
{
    private async Task<List<SelectListItem>> GetFolderSelectList(int? selectedId, int? excludeId = null)
    {
        var folders = await context.ContractFolders.ToListAsync();
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

    [RenderInNavBar(
        NavGroupName = "Administration",
        NavGroupOrder = 3,
        CascadedLinksGroupName = "Legal",
        CascadedLinksIcon = "scale",
        CascadedLinksOrder = 5,
        LinkText = "Manage Contracts",
        LinkOrder = 1)]
    public async Task<IActionResult> Index(int? id)
    {
        var currentFolder = id.HasValue
            ? await context.ContractFolders.FindAsync(id.Value)
            : null;

        var contracts = await context.Contracts
            .Include(c => c.CollectionChannels)
            .Where(c => c.FolderId == id)
            .OrderByDescending(c => c.CreateTime)
            .ToListAsync();

        var subFolders = await context.ContractFolders
            .Where(f => f.ParentFolderId == id)
            .OrderBy(f => f.Name)
            .ToListAsync();

        return this.StackView(new ManageViewModel
        {
            FolderId = id,
            CurrentFolder = currentFolder,
            SubFolders = subFolders,
            Contracts = contracts
        });
    }

    public async Task<IActionResult> Finance(int id)
    {
        var contract = await context.Contracts
            .Include(c => c.CollectionChannels)
            .ThenInclude(cc => cc.Payer)
            .Include(c => c.CollectionChannels)
            .ThenInclude(cc => cc.Payee)
            .Include(c => c.CollectionChannels)
            .ThenInclude(cc => cc.Records)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (contract == null)
        {
            return NotFound();
        }

        var model = new FinanceDetailsViewModel
        {
            Contract = contract,
            TotalExpectedIncome = contract.CollectionChannels
                .Where(cc => cc.Payee?.CreateLedger == true)
                .SelectMany(cc => cc.Records)
                .Where(r => r.Status != CollectionRecordStatus.Cancelled)
                .Sum(r => r.ExpectedAmount),
            TotalActualIncome = contract.CollectionChannels
                .Where(cc => cc.Payee?.CreateLedger == true)
                .SelectMany(cc => cc.Records)
                .Sum(r => r.ActualAmount),
            TotalExpectedExpense = contract.CollectionChannels
                .Where(cc => cc.Payer?.CreateLedger == true)
                .SelectMany(cc => cc.Records)
                .Where(r => r.Status != CollectionRecordStatus.Cancelled)
                .Sum(r => r.ExpectedAmount),
            TotalActualExpense = contract.CollectionChannels
                .Where(cc => cc.Payer?.CreateLedger == true)
                .SelectMany(cc => cc.Records)
                .Sum(r => r.ActualAmount),
            Currency = contract.CollectionChannels.FirstOrDefault()?.Currency ?? "CNY"
        };

        return this.StackView(model);
    }

    [RenderInNavBar(
        NavGroupName = "Administration",
        NavGroupOrder = 3,
        CascadedLinksGroupName = "Finance",
        CascadedLinksIcon = "dollar-sign",
        CascadedLinksOrder = 5,
        LinkText = "Contract Finance Statistics",
        LinkOrder = 1)]
    public async Task<IActionResult> FinanceStats()
    {
        var contracts = await context.Contracts
            .Include(c => c.CollectionChannels)
            .ThenInclude(cc => cc.Payer)
            .Include(c => c.CollectionChannels)
            .ThenInclude(cc => cc.Payee)
            .Include(c => c.CollectionChannels)
            .ThenInclude(cc => cc.Records)
            .ToListAsync();

        var model = new FinanceStatsViewModel
        {
            Summaries = contracts
                .Where(c => c.CollectionChannels.Any())
                .Select(c => new ContractFinanceSummary
                {
                    Contract = c,
                    TotalExpectedIncome = c.CollectionChannels
                        .Where(cc => cc.Payee?.CreateLedger == true)
                        .SelectMany(cc => cc.Records)
                        .Where(r => r.Status != CollectionRecordStatus.Cancelled)
                        .Sum(r => r.ExpectedAmount),
                    TotalActualIncome = c.CollectionChannels
                        .Where(cc => cc.Payee?.CreateLedger == true)
                        .SelectMany(cc => cc.Records)
                        .Sum(r => r.ActualAmount),
                    TotalExpectedExpense = c.CollectionChannels
                        .Where(cc => cc.Payer?.CreateLedger == true)
                        .SelectMany(cc => cc.Records)
                        .Where(r => r.Status != CollectionRecordStatus.Cancelled)
                        .Sum(r => r.ExpectedAmount),
                    TotalActualExpense = c.CollectionChannels
                        .Where(cc => cc.Payer?.CreateLedger == true)
                        .SelectMany(cc => cc.Records)
                        .Sum(r => r.ActualAmount),
                    Currency = c.CollectionChannels.FirstOrDefault()?.Currency ?? "CNY"
                })
                .ToList()
        };

        return this.StackView(model);
    }

    [Authorize(Policy = AppPermissionNames.CanCreateContract)]
    public async Task<IActionResult> ImportZip(int? folderId)
    {
        ViewData["Folders"] = await GetFolderSelectList(folderId);
        return this.StackView(new ImportZipViewModel { FolderId = folderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanCreateContract)]
    public async Task<IActionResult> ImportZip(ImportZipViewModel model)
    {
        if (model.ZipFile == null || model.ZipFile.Length == 0)
        {
            ModelState.AddModelError(nameof(model.ZipFile), "Please select a zip file.");
        }
        else if (!model.ZipFile.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.ZipFile), "Only .zip files are supported.");
        }

        if (ModelState.IsValid)
        {
            var importService = HttpContext.RequestServices.GetRequiredService<ContractImportService>();
            await using var stream = model.ZipFile!.OpenReadStream();
            var result = await importService.ImportFromZipAsync(stream, model.FolderId, model.IsPublic, model.Status);

            if (result.Errors.Any())
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", $"Failed to import: {error}");
                }
            }

            if (result.FilesImported > 0)
            {
                TempData["SuccessMessage"] = $"Imported {result.FilesImported} files into {result.FoldersCreated} folders.";
            }

            return RedirectToAction(nameof(Index), new { id = model.FolderId });
        }

        ViewData["Folders"] = await GetFolderSelectList(model.FolderId);
        return this.StackView(model);
    }

    [Authorize(Policy = AppPermissionNames.CanCreateContract)]
    public IActionResult Create(int? folderId)
    {
        return this.StackView(new CreateViewModel { FolderId = folderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanCreateContract)]
    public async Task<IActionResult> Create(CreateViewModel model)
    {
        if (ModelState.IsValid)
        {
            var contract = new Contract
            {
                Name = model.Name,
                FilePath = model.FilePath!,
                Status = model.Status,
                IsPublic = model.IsPublic,
                CreateTime = DateTime.UtcNow,
                FolderId = model.FolderId
            };
            context.Contracts.Add(contract);
            await context.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { id = model.FolderId });
        }

        return this.StackView(model);
    }

    [Authorize(Policy = AppPermissionNames.CanCreateContract)]
    public IActionResult CreateFolder(int? id)
    {
        return this.StackView(new CreateFolderViewModel { ParentFolderId = id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanCreateContract)]
    public async Task<IActionResult> CreateFolder(CreateFolderViewModel model)
    {
        if (ModelState.IsValid)
        {
            var folder = new ContractFolder
            {
                Name = model.Name,
                ParentFolderId = model.ParentFolderId
            };
            context.ContractFolders.Add(folder);
            await context.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { id = model.ParentFolderId });
        }
        return this.StackView(model);
    }

    [Authorize(Policy = AppPermissionNames.CanCreateContract)]
    public async Task<IActionResult> Edit(int id)
    {
        var contract = await context.Contracts
            .FirstOrDefaultAsync(c => c.Id == id);
        if (contract == null) return NotFound();

        ViewData["Folders"] = await GetFolderSelectList(contract.FolderId);
        return this.StackView(new EditViewModel
        {
            Id = contract.Id,
            Name = contract.Name,
            Status = contract.Status,
            IsPublic = contract.IsPublic,
            FolderId = contract.FolderId
        });
    }

    [Authorize(Policy = AppPermissionNames.CanCreateContract)]
    public async Task<IActionResult> EditFolder(int id)
    {
        var folder = await context.ContractFolders.FindAsync(id);
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
    [Authorize(Policy = AppPermissionNames.CanCreateContract)]
    public async Task<IActionResult> EditFolder(EditFolderViewModel model)
    {
        if (ModelState.IsValid)
        {
            var folder = await context.ContractFolders.FindAsync(model.Id);
            if (folder == null) return NotFound();

            if (await IsFolderChildOf(folder.Id, model.ParentFolderId))
            {
                ModelState.AddModelError(nameof(model.ParentFolderId), "Cannot move a folder to its own child!");
                ViewData["Folders"] = await GetFolderSelectList(model.ParentFolderId, excludeId: folder.Id);
                return this.StackView(model);
            }

            folder.Name = model.Name;
            folder.ParentFolderId = model.ParentFolderId;
            await context.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { id = folder.ParentFolderId });
        }
        ViewData["Folders"] = await GetFolderSelectList(model.ParentFolderId, excludeId: model.Id);
        return this.StackView(model);
    }

    private async Task<bool> IsFolderChildOf(int sourceFolderId, int? targetFolderId)
    {
        if (targetFolderId == null) return false;
        if (sourceFolderId == targetFolderId) return true;

        var targetFolder = await context.ContractFolders.FindAsync(targetFolderId.Value);
        if (targetFolder == null) return false;

        return await IsFolderChildOf(sourceFolderId, targetFolder.ParentFolderId);
    }

    [Authorize(Policy = AppPermissionNames.CanCreateContract)]
    public async Task<IActionResult> OcrResults(int id)
    {
        var contract = await context.Contracts
            .FirstOrDefaultAsync(c => c.Id == id);
        if (contract == null) return NotFound();

        var ocrResult = await context.ContractOcrResults
            .FirstOrDefaultAsync(r => r.ContractId == id);

        return this.StackView(new OcrPreviewViewModel
        {
            Contract = contract,
            PlainText = ocrResult?.PlainText,
            JsonResult = ocrResult?.JsonResult
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanCreateContract)]
    public async Task<IActionResult> Edit(EditViewModel model)
    {
        if (ModelState.IsValid)
        {
            var contract = await context.Contracts.FindAsync(model.Id);
            if (contract == null) return NotFound();

            contract.Name = model.Name;
            contract.Status = model.Status;
            contract.IsPublic = model.IsPublic;
            contract.FolderId = model.FolderId;

            if (!string.IsNullOrWhiteSpace(model.FilePath))
            {
                contract.FilePath = model.FilePath;
            }

            await context.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { id = contract.FolderId });
        }
        ViewData["Folders"] = await GetFolderSelectList(model.FolderId);
        return this.StackView(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanCreateContract)]
    public async Task<IActionResult> Delete(int id)
    {
        var contract = await context.Contracts.FindAsync(id);
        if (contract != null)
        {
            var folderId = contract.FolderId;
            context.Contracts.Remove(contract);
            await context.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { id = folderId });
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanCreateContract)]
    public async Task<IActionResult> DeleteFolder(int id)
    {
        var folder = await context.ContractFolders
            .Include(f => f.SubFolders)
            .Include(f => f.Contracts)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (folder != null)
        {
            if (folder.SubFolders.Any() || folder.Contracts.Any())
            {
                return BadRequest("Folder is not empty.");
            }
            var parentId = folder.ParentFolderId;
            context.ContractFolders.Remove(folder);
            await context.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { id = parentId });
        }
        return NotFound();
    }
}
