using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.CollectionRecordsViewModels;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.EmployeeCenter.Services.FileStorage;
using Aiursoft.WebTools.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.EmployeeCenter.Controllers;

[Authorize(Policy = AppPermissionNames.CanViewCollectionChannels)]
[LimitPerMin]
public class CollectionRecordsController(
    EmployeeCenterDbContext context,
    StorageService storageService) : Controller
{
    [Authorize(Policy = AppPermissionNames.CanManageCollectionChannels)]
    public async Task<IActionResult> Create(int channelId)
    {
        var channel = await context.CollectionChannels
            .Include(c => c.Records)
            .FirstOrDefaultAsync(c => c.Id == channelId);

        if (channel == null)
        {
            return NotFound();
        }

        if (!channel.IsRecurring && channel.Records.Count > 0)
        {
            return BadRequest("Non-recurring channel can only have one record.");
        }

        return this.StackView(new CreateViewModel
        {
            ChannelId = channelId,
            Channel = channel,
            ExpectedAmount = channel.ReferenceAmount / 100.0m
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanManageCollectionChannels)]
    public async Task<IActionResult> Create(CreateViewModel model)
    {
        var channel = await context.CollectionChannels
            .Include(c => c.Records)
            .FirstOrDefaultAsync(c => c.Id == model.ChannelId);

        if (channel == null)
        {
            return NotFound();
        }

        if (!channel.IsRecurring && channel.Records.Count > 0)
        {
            ModelState.AddModelError(string.Empty, "Non-recurring channel can only have one record.");
        }

        ValidateFile(model.ReceiptPath, nameof(model.ReceiptPath));
        ValidateFile(model.InvoicePath, nameof(model.InvoicePath));
        ValidateFile(model.SwiftReceiptPath, nameof(model.SwiftReceiptPath));

        if (ModelState.IsValid)
        {
            var record = new CollectionRecord
            {
                ChannelId = model.ChannelId,
                ExpectedAmount = (long)(model.ExpectedAmount * 100),
                ActualAmount = (long)(model.ActualAmount * 100),
                DueDate = model.DueDate,
                PaidDate = model.PaidDate,
                ReceiptPath = model.ReceiptPath,
                InvoicePath = model.InvoicePath,
                TransactionId = model.TransactionId,
                SwiftReceiptPath = model.SwiftReceiptPath,
                Remark = model.Remark,
                Status = model.Status,
                CreateTime = DateTime.UtcNow
            };
            context.CollectionRecords.Add(record);
            await context.SaveChangesAsync();
            return RedirectToAction("Details", "CollectionChannels", new { id = model.ChannelId });
        }

        model.Channel = channel;
        return this.StackView(model);
    }

    [Authorize(Policy = AppPermissionNames.CanManageCollectionChannels)]
    public async Task<IActionResult> Edit(int id)
    {
        var record = await context.CollectionRecords
            .Include(r => r.Channel)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (record == null)
        {
            return NotFound();
        }

        return this.StackView(new EditViewModel
        {
            Id = record.Id,
            ChannelId = record.ChannelId,
            Channel = record.Channel,
            ExpectedAmount = record.ExpectedAmount / 100.0m,
            ActualAmount = record.ActualAmount / 100.0m,
            DueDate = record.DueDate,
            PaidDate = record.PaidDate,
            ReceiptPath = record.ReceiptPath,
            InvoicePath = record.InvoicePath,
            TransactionId = record.TransactionId,
            SwiftReceiptPath = record.SwiftReceiptPath,
            Remark = record.Remark,
            Status = record.Status
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanManageCollectionChannels)]
    public async Task<IActionResult> Edit(EditViewModel model)
    {
        ValidateFile(model.ReceiptPath, nameof(model.ReceiptPath));
        ValidateFile(model.InvoicePath, nameof(model.InvoicePath));
        ValidateFile(model.SwiftReceiptPath, nameof(model.SwiftReceiptPath));

        if (ModelState.IsValid)
        {
            var record = await context.CollectionRecords.FindAsync(model.Id);
            if (record == null)
            {
                return NotFound();
            }

            record.ExpectedAmount = (long)(model.ExpectedAmount * 100);
            record.ActualAmount = (long)(model.ActualAmount * 100);
            record.DueDate = model.DueDate;
            record.PaidDate = model.PaidDate;
            record.ReceiptPath = model.ReceiptPath;
            record.InvoicePath = model.InvoicePath;
            record.TransactionId = model.TransactionId;
            record.SwiftReceiptPath = model.SwiftReceiptPath;
            record.Remark = model.Remark;
            record.Status = model.Status;

            await context.SaveChangesAsync();
            return RedirectToAction("Details", "CollectionChannels", new { id = record.ChannelId });
        }

        model.Channel = await context.CollectionChannels.FindAsync(model.ChannelId);
        return this.StackView(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanManageCollectionChannels)]
    public async Task<IActionResult> Delete(int id)
    {
        var record = await context.CollectionRecords.FindAsync(id);
        if (record == null)
        {
            return NotFound();
        }

        var channelId = record.ChannelId;
        context.CollectionRecords.Remove(record);
        await context.SaveChangesAsync();
        return RedirectToAction("Details", "CollectionChannels", new { id = channelId });
    }

    private void ValidateFile(string? path, string propertyName)
    {
        if (string.IsNullOrEmpty(path)) return;
        try 
        {
            var physicalPath = storageService.GetFilePhysicalPath(path, isVault: true);
            if (!System.IO.File.Exists(physicalPath))
            {
                 ModelState.AddModelError(propertyName, "File upload failed or missing. Please re-upload.");
            }
        }
        catch (ArgumentException)
        {
            ModelState.AddModelError(propertyName, "Invalid file path.");
        }
    }
}
