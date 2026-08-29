using Aiursoft.Canon.BackgroundJobs;
using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Models.DnsAuditViewModels;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.EmployeeCenter.Services.BackgroundJobs;
using Aiursoft.EmployeeCenter.Services.DnsAudit;
using Aiursoft.WebTools.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aiursoft.EmployeeCenter.Controllers;

[Authorize(Policy = AppPermissionNames.CanAuditDns)]
[LimitPerMin]
public sealed class DnsAuditController(
    DnsAuditSnapshotCache snapshotCache,
    BackgroundJobRegistry jobRegistry) : Controller
{
    public IActionResult Index()
    {
        var snapshot = snapshotCache.Current;
        return this.StackView(new DnsAuditIndexViewModel
        {
            IsInitialized = snapshot.IsInitialized,
            IsConfigured = snapshot.IsConfigured,
            ErrorMessage = snapshot.ErrorMessage,
            Report = snapshot.Report,
            LastAttemptedAt = snapshot.LastAttemptedAt,
            LastSuccessfulAt = snapshot.LastSuccessfulAt
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Refresh()
    {
        jobRegistry.TriggerNow(nameof(DnsAuditJob));
        return RedirectToAction(nameof(Index));
    }
}
