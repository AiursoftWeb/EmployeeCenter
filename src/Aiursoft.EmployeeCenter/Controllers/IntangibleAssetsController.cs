using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.IntangibleAssetsViewModels;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.UiStack.Navigation;
using Aiursoft.WebTools.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.EmployeeCenter.Controllers;

[Authorize(Policy = AppPermissionNames.CanManageAssets)]
[LimitPerMin]
public class IntangibleAssetsController(
    EmployeeCenterDbContext context)
    : Controller
{
    [RenderInNavBar(
        NavGroupName = "Administration",
        NavGroupOrder = 3,
        CascadedLinksGroupName = "Assets",
        CascadedLinksIcon = "monitor",
        CascadedLinksOrder = 4,
        LinkText = "Manage Intangible Assets",
        LinkOrder = 2)]
    public async Task<IActionResult> Index(int? companyEntityId)
    {
        var allCompanyEntities = await context.CompanyEntities
            .OrderBy(e => e.CompanyName)
            .ToListAsync();

        // Default to the company entity with the most intangible assets
        if (companyEntityId == null)
        {
            companyEntityId = await context.IntangibleAssets
                .Where(a => a.CompanyEntityId != null)
                .GroupBy(a => a.CompanyEntityId)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefaultAsync();
        }

        var query = context.IntangibleAssets
            .Include(a => a.Assignee)
            .Include(a => a.CompanyEntity)
            .AsQueryable();

        if (companyEntityId.HasValue)
        {
            query = query.Where(a => a.CompanyEntityId == companyEntityId.Value);
        }

        var assets = await query
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        return this.StackView(new IndexViewModel
        {
            Assets = assets,
            SelectedCompanyEntityId = companyEntityId,
            AllCompanyEntities = allCompanyEntities
        });
    }

    public async Task<IActionResult> Create()
    {
        return this.StackView(new CreateViewModel
        {
            AllUsers = await context.Users.ToListAsync(),
            AllCompanyEntities = await context.CompanyEntities.ToListAsync(),
            CurrencyOptions = Configuration.SettingsMap.Definitions.First(d => d.Key == Configuration.SettingsMap.DefaultPayrollCurrency).ChoiceOptions!
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateViewModel model)
    {
        if (ModelState.IsValid)
        {
            var asset = new IntangibleAsset
            {
                Id = Guid.NewGuid(),
                Name = model.Name,
                Type = model.Type,
                Supplier = model.Supplier,
                Account = model.Account,
                Password = model.Password,
                ManagementUrl = model.ManagementUrl,
                FilingNumber = model.FilingNumber,
                FilingSubject = model.FilingSubject,
                FilingQueryMethod = model.FilingQueryMethod,
                RegistrationDate = model.RegistrationDate,
                ExpirationDate = model.ExpirationDate,
                PurchasePrice = model.PurchasePrice,
                Currency = model.Currency,
                TrademarkImageUrl = model.TrademarkImageUrl,
                InvoiceFileUrl = model.InvoiceFileUrl,
                RegistrationCertificateUrl = model.RegistrationCertificateUrl,
                IsPublic = model.IsPublic,
                CompanyEntityId = model.CompanyEntityId,
                AssigneeId = model.AssigneeId,
                Status = model.Status,
                Description = model.Description,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.IntangibleAssets.Add(asset);
            await context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        model.AllUsers = await context.Users.ToListAsync();
        model.AllCompanyEntities = await context.CompanyEntities.ToListAsync();
        model.CurrencyOptions = Configuration.SettingsMap.Definitions.First(d => d.Key == Configuration.SettingsMap.DefaultPayrollCurrency).ChoiceOptions!;
        return this.StackView(model);
    }

    public async Task<IActionResult> Details(Guid id)
    {
        var asset = await context.IntangibleAssets
            .Include(a => a.Assignee)
            .Include(a => a.CompanyEntity)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (asset == null) return NotFound();

        return this.StackView(new DetailsViewModel
        {
            Asset = asset
        });
    }

    public async Task<IActionResult> Edit(Guid id)
    {
        var asset = await context.IntangibleAssets.FindAsync(id);
        if (asset == null) return NotFound();

        return this.StackView(new EditViewModel
        {
            Id = asset.Id,
            Name = asset.Name,
            Type = asset.Type,
            Supplier = asset.Supplier,
            Account = asset.Account,
            Password = asset.Password,
            ManagementUrl = asset.ManagementUrl,
            FilingNumber = asset.FilingNumber,
            FilingSubject = asset.FilingSubject,
            FilingQueryMethod = asset.FilingQueryMethod,
            RegistrationDate = asset.RegistrationDate,
            ExpirationDate = asset.ExpirationDate,
            PurchasePrice = asset.PurchasePrice,
            Currency = asset.Currency,
            TrademarkImageUrl = asset.TrademarkImageUrl,
            InvoiceFileUrl = asset.InvoiceFileUrl,
            RegistrationCertificateUrl = asset.RegistrationCertificateUrl,
            IsPublic = asset.IsPublic,
            CompanyEntityId = asset.CompanyEntityId,
            AssigneeId = asset.AssigneeId,
            Status = asset.Status,
            Description = asset.Description,
            AllUsers = await context.Users.ToListAsync(),
            AllCompanyEntities = await context.CompanyEntities.ToListAsync(),
            CurrencyOptions = Configuration.SettingsMap.Definitions.First(d => d.Key == Configuration.SettingsMap.DefaultPayrollCurrency).ChoiceOptions!
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditViewModel model)
    {
        var asset = await context.IntangibleAssets.FindAsync(model.Id);
        if (asset == null) return NotFound();

        if (ModelState.IsValid)
        {
            asset.Name = model.Name;
            asset.Type = model.Type;
            asset.Supplier = model.Supplier;
            asset.Account = model.Account;
            asset.Password = model.Password;
            asset.ManagementUrl = model.ManagementUrl;
            asset.FilingNumber = model.FilingNumber;
            asset.FilingSubject = model.FilingSubject;
            asset.FilingQueryMethod = model.FilingQueryMethod;
            asset.RegistrationDate = model.RegistrationDate;
            asset.ExpirationDate = model.ExpirationDate;
            asset.PurchasePrice = model.PurchasePrice;
            asset.Currency = model.Currency;
            asset.TrademarkImageUrl = model.TrademarkImageUrl;
            asset.InvoiceFileUrl = model.InvoiceFileUrl;
            asset.RegistrationCertificateUrl = model.RegistrationCertificateUrl;
            asset.IsPublic = model.IsPublic;
            asset.CompanyEntityId = model.CompanyEntityId;
            asset.AssigneeId = model.AssigneeId;
            asset.Status = model.Status;
            asset.Description = model.Description;
            asset.UpdatedAt = DateTime.UtcNow;

            await context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        model.AllUsers = await context.Users.ToListAsync();
        model.AllCompanyEntities = await context.CompanyEntities.ToListAsync();
        model.CurrencyOptions = Configuration.SettingsMap.Definitions.First(d => d.Key == Configuration.SettingsMap.DefaultPayrollCurrency).ChoiceOptions!;
        return this.StackView(model);
    }

    public async Task<IActionResult> Assign(Guid id)
    {
        var asset = await context.IntangibleAssets.FindAsync(id);
        if (asset == null) return NotFound();

        return this.StackView(new AssignViewModel
        {
            AssetId = asset.Id,
            Name = asset.Name,
            AssigneeId = asset.AssigneeId ?? string.Empty,
            AllUsers = await context.Users.ToListAsync()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(AssignViewModel model)
    {
        var asset = await context.IntangibleAssets.FindAsync(model.AssetId);
        if (asset == null) return NotFound();

        if (ModelState.IsValid)
        {
            asset.AssigneeId = model.AssigneeId;
            asset.UpdatedAt = DateTime.UtcNow;

            await context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        model.AllUsers = await context.Users.ToListAsync();
        return this.StackView(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var asset = await context.IntangibleAssets.FindAsync(id);
        if (asset == null) return NotFound();

        context.IntangibleAssets.Remove(asset);
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
