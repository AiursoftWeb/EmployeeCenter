using System.Text.Json;
using Aiursoft.EmployeeCenter.Configuration;
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
    BackgroundJobRegistry jobRegistry,
    GlobalSettingsService settingsService) : Controller
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

    [HttpGet("/ServiceAudit/Settings", Name = "ServiceAuditSettings")]
    [Authorize(Policy = AppPermissionNames.CanViewServiceAudit)]
    [Authorize(Policy = AppPermissionNames.CanManageInfrastructure)]
    public async Task<IActionResult> Settings(bool saved = false)
    {
        var scope = ServiceAuditScope.Deserialize(await settingsService.GetSettingValueAsync(SettingsMap.ServiceAuditScope));
        return this.StackView(new AuditSettingsViewModel
        {
            AuditAllZones = scope.AuditAllZones,
            IncludedZones = string.Join('\n', scope.IncludedZones),
            ExcludedDomains = string.Join('\n', scope.ExcludedDomains),
            IsOverriddenByConfig = settingsService.IsOverriddenByConfig(SettingsMap.ServiceAuditScope),
            Saved = saved
        });
    }

    [HttpPost("/ServiceAudit/Settings")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanViewServiceAudit)]
    [Authorize(Policy = AppPermissionNames.CanManageInfrastructure)]
    public async Task<IActionResult> Settings(AuditSettingsViewModel model)
    {
        model.IsOverriddenByConfig = settingsService.IsOverriddenByConfig(SettingsMap.ServiceAuditScope);
        model.Saved = false;
        if (model.IsOverriddenByConfig) return Conflict();
        if (!ModelState.IsValid) return this.StackView(model);
        await settingsService.UpdateSettingAsync(SettingsMap.ServiceAuditScope, JsonSerializer.Serialize(model.ToScope()));
        return RedirectToRoute("ServiceAuditSettings", new { saved = true });
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
