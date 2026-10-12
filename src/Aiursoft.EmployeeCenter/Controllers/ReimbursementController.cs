using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Configuration;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.ReimbursementViewModels;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.EmployeeCenter.Services.FileStorage;
using Aiursoft.UiStack.Navigation;
using Aiursoft.WebTools.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.EmployeeCenter.Controllers;

[Authorize]
[LimitPerMin]
public class ReimbursementController(
    UserManager<User> userManager,
    EmployeeCenterDbContext context,
    StorageService storage,
    IAuthorizationService authorizationService)
    : Controller
{
    private Dictionary<string, string> GetCurrencyOptions()
    {
        return SettingsMap.Definitions
            .First(d => d.Key == SettingsMap.DefaultPayrollCurrency).ChoiceOptions!;
    }

    [RenderInNavBar(
        NavGroupName = "Career",
        NavGroupOrder = 1,
        CascadedLinksGroupName = "Reimbursement",
        CascadedLinksIcon = "hand-coins",
        CascadedLinksOrder = 6,
        LinkText = "My Reimbursements",
        LinkOrder = 1)]
    public async Task<IActionResult> Index()
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var myReimbursements = await context.Reimbursements
            .Where(r => r.SubmitterId == user.Id)
            .OrderByDescending(r => r.SubmittedAt)
            .ToListAsync();

        return this.StackView(new IndexViewModel { MyReimbursements = myReimbursements });
    }

    [Authorize(AppPermissionNames.CanSubmitReimbursement)]
    public IActionResult Create()
    {
        return this.StackView(new CreateViewModel
        {
            CurrencyOptions = GetCurrencyOptions()
        });
    }

    [HttpPost]
    [Authorize(AppPermissionNames.CanSubmitReimbursement)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateViewModel model)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        if (model.ExpenseTime > DateTime.UtcNow)
        {
            ModelState.AddModelError(nameof(model.ExpenseTime), "Expense time cannot be in the future.");
        }

        try 
        {
            if (!string.IsNullOrEmpty(model.InvoicePath))
            {
                var physicalPath = storage.GetFilePhysicalPath(model.InvoicePath, isVault: true);
                if (!System.IO.File.Exists(physicalPath))
                {
                    ModelState.AddModelError(nameof(model.InvoicePath), "File upload failed or missing. Please re-upload.");
                }
            }
        }
        catch (ArgumentException)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            model.CurrencyOptions = GetCurrencyOptions();
            return this.StackView(model);
        }

        var reimbursement = new Reimbursement
        {
            SubmitterId = user.Id,
            ExpenseTime = model.ExpenseTime,
            Purpose = model.Purpose,
            SupportingEmail = model.SupportingEmail,
            InvoicePath = model.InvoicePath,
            Amount = model.Amount,
            Currency = model.Currency,
            Category = model.Category,
            InvoiceSourceUrl = model.InvoiceSourceUrl,
            Status = model.SaveAsDraft ? ReimbursementStatus.Draft : ReimbursementStatus.Applying
        };

        context.Reimbursements.Add(reimbursement);
        await context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    [Authorize(AppPermissionNames.CanSubmitReimbursement)]
    public async Task<IActionResult> Edit(int id)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var reimbursement = await context.Reimbursements
            .FirstOrDefaultAsync(r => r.Id == id && r.SubmitterId == user.Id);

        if (reimbursement == null) return NotFound();

        if (reimbursement.Status != ReimbursementStatus.Draft)
        {
            return BadRequest("Only draft reimbursements can be edited.");
        }

        var model = new EditViewModel
        {
            Id = reimbursement.Id,
            ExpenseTime = reimbursement.ExpenseTime,
            Purpose = reimbursement.Purpose,
            SupportingEmail = reimbursement.SupportingEmail,
            Amount = reimbursement.Amount,
            Currency = reimbursement.Currency,
            CurrencyOptions = GetCurrencyOptions(),
            Category = reimbursement.Category,
            ExistingInvoicePath = reimbursement.InvoicePath,
            InvoicePath = reimbursement.InvoicePath,
            InvoiceSourceUrl = reimbursement.InvoiceSourceUrl
        };

        return this.StackView(model);
    }

    [HttpPost]
    [Authorize(AppPermissionNames.CanSubmitReimbursement)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditViewModel model)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var reimbursement = await context.Reimbursements
            .FirstOrDefaultAsync(r => r.Id == model.Id && r.SubmitterId == user.Id);

        if (reimbursement == null) return NotFound();

        if (reimbursement.Status != ReimbursementStatus.Draft)
        {
            return BadRequest("Only draft reimbursements can be edited.");
        }

        if (model.ExpenseTime > DateTime.UtcNow)
        {
            ModelState.AddModelError(nameof(model.ExpenseTime), "Expense time cannot be in the future.");
        }

        try 
        {
            if (!string.IsNullOrEmpty(model.InvoicePath))
            {
                var physicalPath = storage.GetFilePhysicalPath(model.InvoicePath, isVault: true);
                if (!System.IO.File.Exists(physicalPath))
                {
                    ModelState.AddModelError(nameof(model.InvoicePath), "File upload failed or missing. Please re-upload.");
                }
            }
        }
        catch (ArgumentException)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            model.CurrencyOptions = GetCurrencyOptions();
            return this.StackView(model);
        }

        if (!string.IsNullOrEmpty(model.InvoicePath) && reimbursement.InvoicePath != model.InvoicePath)
        {
            reimbursement.InvoicePath = model.InvoicePath;
        }

        reimbursement.ExpenseTime = model.ExpenseTime;
        reimbursement.Purpose = model.Purpose;
        reimbursement.SupportingEmail = model.SupportingEmail;
        reimbursement.Amount = model.Amount;
        reimbursement.Currency = model.Currency;
        reimbursement.Category = model.Category;
        reimbursement.InvoiceSourceUrl = model.InvoiceSourceUrl;
        reimbursement.Status = model.SaveAsDraft ? ReimbursementStatus.Draft : ReimbursementStatus.Applying;

        await context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(AppPermissionNames.CanSubmitReimbursement)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var reimbursement = await context.Reimbursements
            .FirstOrDefaultAsync(r => r.Id == id && r.SubmitterId == user.Id);

        if (reimbursement == null) return NotFound();

        if (reimbursement.Status != ReimbursementStatus.Draft)
        {
            return BadRequest("Only draft reimbursements can be deleted.");
        }

        context.Reimbursements.Remove(reimbursement);
        await context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(AppPermissionNames.CanSubmitReimbursement)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Revoke(int id)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var reimbursement = await context.Reimbursements
            .FirstOrDefaultAsync(r => r.Id == id && r.SubmitterId == user.Id);

        if (reimbursement == null) return NotFound();

        if (reimbursement.Status != ReimbursementStatus.Applying)
        {
            return BadRequest("Only reimbursements with status 'Applying' can be revoked.");
        }

        reimbursement.Status = ReimbursementStatus.Revoked;
        await context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(int id)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var reimbursement = await context.Reimbursements
            .Include(r => r.Submitter)
            .Include(r => r.ReviewedBy)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (reimbursement == null) return NotFound();

        // Access check: Submitter or Approver
        var canApprove = (await authorizationService.AuthorizeAsync(User, AppPermissionNames.CanApproveReimbursement)).Succeeded;
        if (reimbursement.SubmitterId != user.Id && !canApprove)
        {
            return Unauthorized();
        }

        var model = new DetailsViewModel
        {
            Reimbursement = reimbursement,
            CanApprove = canApprove && reimbursement.SubmitterId != user.Id,
            InvoiceUrl = reimbursement.InvoicePath != null ? storage.RelativePathToInternetUrl(reimbursement.InvoicePath, isVault: true) : null
        };

        return this.StackView(model);
    }

    [RenderInNavBar(
        NavGroupName = "Career",
        NavGroupOrder = 1,
        CascadedLinksGroupName = "Reimbursement",
        CascadedLinksIcon = "hand-coins",
        CascadedLinksOrder = 6,
        LinkText = "Approval Center",
        LinkOrder = 2)]
    [Authorize(AppPermissionNames.CanApproveReimbursement)]
    public async Task<IActionResult> Manage()
    {
        var pendingRequests = await context.Reimbursements
            .Include(r => r.Submitter)
            .Where(r => r.Status == ReimbursementStatus.Applying)
            .OrderBy(r => r.SubmittedAt)
            .ToListAsync();

        var acknowledgedRequests = await context.Reimbursements
            .Include(r => r.Submitter)
            .Where(r => r.Status == ReimbursementStatus.Acknowledged)
            .OrderBy(r => r.SubmittedAt)
            .ToListAsync();

        var historyRequests = await context.Reimbursements
            .Include(r => r.Submitter)
            .Include(r => r.ReviewedBy)
            .Where(r => r.Status == ReimbursementStatus.Reimbursed || r.Status == ReimbursementStatus.Rejected || r.Status == ReimbursementStatus.Revoked)
            .OrderByDescending(r => r.ReviewedAt ?? r.SubmittedAt)
            .Take(100)
            .ToListAsync();

        var model = new ManageIndexViewModel
        {
            PendingRequests = pendingRequests,
            AcknowledgedRequests = acknowledgedRequests,
            HistoryRequests = historyRequests
        };

        return this.StackView(model);
    }

    [HttpPost]
    [Authorize(AppPermissionNames.CanApproveReimbursement)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Acknowledge(int id)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var reimbursement = await context.Reimbursements.FindAsync(id);
        if (reimbursement == null) return NotFound();

        if (reimbursement.SubmitterId == user.Id)
        {
            return BadRequest("You cannot approve your own reimbursement request.");
        }

        if (reimbursement.Status != ReimbursementStatus.Applying)
        {
            return BadRequest("Only 'Applying' requests can be acknowledged.");
        }

        reimbursement.Status = ReimbursementStatus.Acknowledged;
        reimbursement.ReviewedById = user.Id;
        reimbursement.ReviewedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        return RedirectToAction(nameof(Manage));
    }

    [HttpPost]
    [Authorize(AppPermissionNames.CanApproveReimbursement)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reimburse(ActionViewModel model)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var reimbursement = await context.Reimbursements.FindAsync(model.Id);
        if (reimbursement == null) return NotFound();

        if (reimbursement.SubmitterId == user.Id)
        {
            return BadRequest("You cannot approve your own reimbursement request.");
        }

        if (reimbursement.Status != ReimbursementStatus.Acknowledged && reimbursement.Status != ReimbursementStatus.Applying)
        {
            return BadRequest("Invalid status for reimbursement.");
        }

        if (!ModelState.IsValid)
        {
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        reimbursement.Status = ReimbursementStatus.Reimbursed;
        reimbursement.Comment = model.Comment;
        reimbursement.ReviewedById = user.Id;
        reimbursement.ReviewedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        return RedirectToAction(nameof(Manage));
    }

    [HttpPost]
    [Authorize(AppPermissionNames.CanApproveReimbursement)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(ActionViewModel model)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var reimbursement = await context.Reimbursements.FindAsync(model.Id);
        if (reimbursement == null) return NotFound();

        if (reimbursement.SubmitterId == user.Id)
        {
            return BadRequest("You cannot approve your own reimbursement request.");
        }

        if (reimbursement.Status != ReimbursementStatus.Acknowledged && reimbursement.Status != ReimbursementStatus.Applying)
        {
            return BadRequest("Invalid status for rejection.");
        }

        if (!ModelState.IsValid)
        {
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        reimbursement.Status = ReimbursementStatus.Rejected;
        reimbursement.Comment = model.Comment;
        reimbursement.ReviewedById = user.Id;
        reimbursement.ReviewedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        return RedirectToAction(nameof(Manage));
    }
}
