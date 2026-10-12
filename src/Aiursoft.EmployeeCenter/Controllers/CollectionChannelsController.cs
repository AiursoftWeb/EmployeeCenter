using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.CollectionChannelsViewModels;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.UiStack.Navigation;
using Aiursoft.WebTools.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.EmployeeCenter.Controllers;

[Authorize(Policy = AppPermissionNames.CanViewCollectionChannels)]
[LimitPerMin]
public class CollectionChannelsController(EmployeeCenterDbContext context) : Controller
{
    [RenderInNavBar(
        NavGroupName = "Administration",
        NavGroupOrder = 3,
        CascadedLinksGroupName = "Finance",
        CascadedLinksIcon = "dollar-sign",
        CascadedLinksOrder = 5,
        LinkText = "Collection Channels",
        LinkOrder = 3)]
    public async Task<IActionResult> Index()
    {
        var channels = await context.CollectionChannels
            .Include(c => c.Payer)
            .Include(c => c.Payee)
            .Include(c => c.Contract)
            .OrderByDescending(c => c.CreateTime)
            .ToListAsync();

        return this.StackView(new IndexViewModel
        {
            Channels = channels
        });
    }

    [Authorize(Policy = AppPermissionNames.CanManageCollectionChannels)]
    public async Task<IActionResult> Create(int? contractId = null)
    {
        return this.StackView(new CreateViewModel
        {
            ContractId = contractId,
            AllCompanyEntities = await context.CompanyEntities.ToListAsync(),
            AllContracts = await context.Contracts.ToListAsync()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanManageCollectionChannels)]
    public async Task<IActionResult> Create(CreateViewModel model)
    {
        if (ModelState.IsValid)
        {
            var channel = new CollectionChannel
            {
                PayerId = model.PayerId,
                PayeeId = model.PayeeId,
                ContractId = model.ContractId,
                ReferenceAmount = (long)(model.ReferenceAmount * 100),
                Currency = model.Currency,
                PaymentMethod = model.PaymentMethod,
                StartBillingDate = model.StartBillingDate,
                FirstPaymentDate = model.FirstPaymentDate,
                IsRecurring = model.IsRecurring,
                RecurringPeriod = model.RecurringPeriod,
                Status = CollectionChannelStatus.Active,
                CreateTime = DateTime.UtcNow
            };
            context.CollectionChannels.Add(channel);
            await context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        model.AllCompanyEntities = await context.CompanyEntities.ToListAsync();
        model.AllContracts = await context.Contracts.ToListAsync();
        return this.StackView(model);
    }

    public async Task<IActionResult> Details(int id)
    {
        var channel = await context.CollectionChannels
            .Include(c => c.Payer)
            .Include(c => c.Payee)
            .Include(c => c.Contract)
            .Include(c => c.Records)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (channel == null)
        {
            return NotFound();
        }

        return this.StackView(new DetailsViewModel
        {
            Channel = channel
        });
    }

    [Authorize(Policy = AppPermissionNames.CanManageCollectionChannels)]
    public async Task<IActionResult> Edit(int id)
    {
        var channel = await context.CollectionChannels.FindAsync(id);
        if (channel == null)
        {
            return NotFound();
        }

        return this.StackView(new EditViewModel
        {
            Id = channel.Id,
            PayerId = channel.PayerId,
            PayeeId = channel.PayeeId,
            ContractId = channel.ContractId,
            ReferenceAmount = channel.ReferenceAmount / 100.0m,
            Currency = channel.Currency,
            PaymentMethod = channel.PaymentMethod,
            StartBillingDate = channel.StartBillingDate,
            FirstPaymentDate = channel.FirstPaymentDate,
            IsRecurring = channel.IsRecurring,
            RecurringPeriod = channel.RecurringPeriod,
            Status = channel.Status,
            AllCompanyEntities = await context.CompanyEntities.ToListAsync(),
            AllContracts = await context.Contracts.ToListAsync()
        });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AppPermissionNames.CanManageCollectionChannels)]
        public async Task<IActionResult> Edit(EditViewModel model)
        {
        if (ModelState.IsValid)
        {
            var channel = await context.CollectionChannels.FindAsync(model.Id);
            if (channel == null)
            {
                return NotFound();
            }

            channel.PayerId = model.PayerId;
            channel.PayeeId = model.PayeeId;
            channel.ContractId = model.ContractId;
            channel.ReferenceAmount = (long)(model.ReferenceAmount * 100);
            channel.Currency = model.Currency;
            channel.PaymentMethod = model.PaymentMethod;
            channel.StartBillingDate = model.StartBillingDate;
            channel.FirstPaymentDate = model.FirstPaymentDate;
            channel.IsRecurring = model.IsRecurring;
            channel.RecurringPeriod = model.RecurringPeriod;
            channel.Status = model.Status;

            await context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id = channel.Id });
        }

        model.AllCompanyEntities = await context.CompanyEntities.ToListAsync();
        model.AllContracts = await context.Contracts.ToListAsync();
        return this.StackView(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanManageCollectionChannels)]
    public async Task<IActionResult> Terminate(int id)
    {
        var channel = await context.CollectionChannels.FindAsync(id);
        if (channel == null)
        {
            return NotFound();
        }

        channel.Status = CollectionChannelStatus.Terminated;
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
