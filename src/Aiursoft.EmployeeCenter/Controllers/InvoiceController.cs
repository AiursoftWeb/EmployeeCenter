using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.InvoiceViewModels;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.UiStack.Navigation;
using Aiursoft.WebTools.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Microsoft.Extensions.Localization;

namespace Aiursoft.EmployeeCenter.Controllers;

[Authorize(Policy = AppPermissionNames.CanManageLedger)]
[LimitPerMin]
public class InvoiceController(
    EmployeeCenterDbContext dbContext,
    IStringLocalizer<InvoiceController> localizer) : Controller
{
    [HttpGet]
    [RenderInNavBar(
        NavGroupName = "Administration",
        NavGroupOrder = 3,
        CascadedLinksGroupName = "Finance",
        CascadedLinksIcon = "shopping-cart",
        CascadedLinksOrder = 4,
        LinkText = "Issue Invoice",
        LinkOrder = 3)]
    public async Task<IActionResult> Index()
    {
        var entities = await dbContext.CompanyEntities
            .OrderByDescending(t => t.CreationTime)
            .ToListAsync();
        var model = new IndexViewModel
        {
            Entities = entities,
            PageTitle = localizer["Issue Invoice"]
        };
        return this.StackView(model);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int sellerId, int? buyerId)
    {
        var seller = await dbContext.CompanyEntities.FindAsync(sellerId);
        if (seller == null)
        {
            return NotFound();
        }

        var buyer = buyerId.HasValue ? await dbContext.CompanyEntities.FindAsync(buyerId.Value) : null;

        var model = new CreateViewModel
        {
            SellerId = sellerId,
            SellerName = seller.CompanyNameEnglish ?? seller.CompanyName,
            SellerAddress = seller.OfficeAddress ?? seller.RegisteredAddress ?? string.Empty,
            SellerCode = seller.EntityCode,
            SellerContact = seller.LegalRepresentative,

            BuyerId = buyerId,
            BuyerName = buyer?.CompanyNameEnglish ?? buyer?.CompanyName ?? string.Empty,
            BuyerAddress = buyer?.OfficeAddress ?? buyer?.RegisteredAddress ?? string.Empty,
            BuyerCRNo = buyer?.EntityCode,
            BuyerAttn = buyer?.LegalRepresentative,

            InvoiceNo = $"INV-{DateTime.Now:yyyyMMdd}-01",
            Date = DateTime.Today,

            BankName = seller.BankName ?? string.Empty,
            BankAccount = seller.BankAccount ?? string.Empty,
            BeneficiaryName = seller.BankAccountName ?? seller.CompanyNameEnglish ?? seller.CompanyName,
            SwiftCode = seller.SwiftCode,
            BankAddress = seller.BankAddress,

            Currency = buyer?.BaseCurrency == "CNY" ? "CNY" : "HKD",
            Items = new List<InvoiceItemViewModel>
            {
                new()
            },
            PageTitle = localizer["Issue Invoice"]
        };

        return this.StackView(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Preview(CreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return this.StackView(model, nameof(Create));
        }

        return View(model);
    }
}
