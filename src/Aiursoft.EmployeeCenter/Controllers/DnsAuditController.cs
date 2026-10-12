using Aiursoft.Canon.BackgroundJobs;
using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.DnsAuditViewModels;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.EmployeeCenter.Services.BackgroundJobs;
using Aiursoft.EmployeeCenter.Services.DnsAudit;
using Aiursoft.WebTools.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Aiursoft.EmployeeCenter.Controllers;

[Authorize]
[LimitPerMin]
public sealed class DnsAuditController(
    DnsAuditSnapshotCache snapshotCache,
    ServiceAuditStore auditStore,
    UserManager<User> userManager,
    BackgroundJobRegistry jobRegistry) : Controller
{
    [HttpGet("/ServiceAudit", Name = "ServiceAuditIndex")]
    [HttpGet("/ServiceAudit/Index")]
    [Authorize(Policy = AppPermissionNames.CanViewServiceAudit)]
    public async Task<IActionResult> Index()
    {
        var snapshot = await auditStore.LoadSnapshotAsync(snapshotCache.Current);
        return this.StackView(new DnsAuditIndexViewModel
        {
            IsInitialized = snapshot.IsInitialized,
            IsConfigured = snapshot.IsConfigured,
            ErrorMessage = snapshot.ErrorMessage,
            Report = snapshot.Report,
            LastAttemptedAt = snapshot.LastAttemptedAt,
            LastSuccessfulAt = snapshot.LastSuccessfulAt,
            RecentRuns = await auditStore.LoadHistoryAsync()
        });
    }

    [HttpPost("/ServiceAudit/Refresh", Name = "ServiceAuditRefresh")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanRunServiceAudit)]
    public async Task<IActionResult> Refresh()
    {
        await auditStore.QueueAsync(userManager.GetUserId(User));
        jobRegistry.TriggerNow(nameof(DnsAuditJob));
        return RedirectToRoute("ServiceAuditIndex");
    }

    [HttpGet("/DnsAudit")]
    [HttpGet("/DnsAudit/Index")]
    [Authorize(Policy = AppPermissionNames.CanViewServiceAudit)]
    public IActionResult LegacyIndex()
    {
        return RedirectToRoutePermanent("ServiceAuditIndex");
    }

    [HttpPost("/DnsAudit/Refresh")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanRunServiceAudit)]
    public async Task<IActionResult> LegacyRefresh()
    {
        await auditStore.QueueAsync(userManager.GetUserId(User));
        jobRegistry.TriggerNow(nameof(DnsAuditJob));
        return RedirectToRoute("ServiceAuditIndex");
    }
}
