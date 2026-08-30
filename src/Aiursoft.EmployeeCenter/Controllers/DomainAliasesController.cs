using Aiursoft.Canon.BackgroundJobs;
using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.DnsAuditViewModels;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.EmployeeCenter.Services.BackgroundJobs;
using Aiursoft.EmployeeCenter.Services.DnsAudit;
using Aiursoft.WebTools.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.EmployeeCenter.Controllers;

[Authorize(Policy = AppPermissionNames.CanAuditDns)]
[Authorize(Policy = AppPermissionNames.CanManageServices)]
[LimitPerMin]
public sealed class DomainAliasesController(
    EmployeeCenterDbContext context,
    DnsAuditSnapshotCache snapshotCache,
    BackgroundJobRegistry jobRegistry) : Controller
{
    public async Task<IActionResult> Index()
    {
        var aliases = await context.DomainAliases
            .AsNoTracking()
            .Include(alias => alias.TargetService)
            .OrderBy(alias => alias.Domain)
            .ToListAsync();
        var snapshot = snapshotCache.Current;
        var failedAliasIds = snapshot.Report?.Issues
            .Where(issue => issue.Type == DnsAuditIssueType.DomainAliasRedirectMismatch)
            .Select(issue => issue.DomainAliasId)
            .OfType<int>()
            .ToHashSet() ?? [];
        var auditedAliases = snapshot.LastSuccessfulAt.HasValue
            ? aliases.Where(alias => alias.UpdatedAt <= snapshot.LastSuccessfulAt.Value).ToList()
            : [];

        return this.StackView(new DomainAliasIndexViewModel
        {
            DomainAliases = aliases,
            AvailableSourceDomains = await LoadAvailableSourceDomainsAsync(),
            HealthyAliasCount = auditedAliases.Count(alias => !failedAliasIds.Contains(alias.Id)),
            UnhealthyAliasCount = auditedAliases.Count(alias => failedAliasIds.Contains(alias.Id)),
            PendingAliasCount = aliases.Count - auditedAliases.Count,
            UnhealthyAliasIds = failedAliasIds,
            LastSuccessfulAuditAt = snapshot.LastSuccessfulAt
        });
    }

    public async Task<IActionResult> Create(string? sourceDomain = null)
    {
        var availableDomains = await LoadAvailableSourceDomainsAsync();
        var domain = DnsAuditAnalyzer.NormalizeDomain(sourceDomain ?? string.Empty);
        if (domain.Length > 0 && !availableDomains.Contains(domain, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest("A domain alias can only be registered from a current Unknown DNS finding.");
        }

        return this.StackView(new CreateDomainAliasViewModel
        {
            Domain = domain,
            AllServices = await LoadServicesAsync(),
            AvailableSourceDomains = availableDomains
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateDomainAliasViewModel model)
    {
        var domain = DnsAuditAnalyzer.NormalizeDomain(model.Domain);
        var availableDomains = await LoadAvailableSourceDomainsAsync();
        if (!availableDomains.Contains(domain, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.Domain),
                "This hostname is no longer a current Unknown DNS finding. Run the audit again before registering it.");
        }

        var targetService = await context.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(service => service.Id == model.TargetServiceId);
        ValidateTarget(model, domain, targetService);

        if (await context.DomainAliases.AnyAsync(alias => alias.Domain == domain))
        {
            ModelState.AddModelError(nameof(model.Domain), "This hostname is already registered as a domain alias.");
        }

        if (await context.Services.AnyAsync(service => service.Domain == domain))
        {
            ModelState.AddModelError(nameof(model.Domain), "This hostname is already registered as a service.");
        }

        if (!ModelState.IsValid)
        {
            model.Domain = domain;
            model.AllServices = await LoadServicesAsync();
            model.AvailableSourceDomains = availableDomains;
            return this.StackView(model);
        }

        DomainAliasRedirectEvaluator.TryNormalizeTargetUrl(model.TargetUrl, out var normalizedTargetUrl, out _);
        context.DomainAliases.Add(new DomainAlias
        {
            Domain = domain,
            TargetServiceId = model.TargetServiceId,
            TargetUrl = normalizedTargetUrl,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        TriggerAudit();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var alias = await context.DomainAliases.FindAsync(id);
        if (alias == null)
        {
            return NotFound();
        }

        return this.StackView(new EditDomainAliasViewModel
        {
            Id = alias.Id,
            Domain = alias.Domain,
            TargetServiceId = alias.TargetServiceId,
            TargetUrl = alias.TargetUrl,
            AllServices = await LoadServicesAsync()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditDomainAliasViewModel model)
    {
        var alias = await context.DomainAliases.FindAsync(model.Id);
        if (alias == null)
        {
            return NotFound();
        }

        model.Domain = alias.Domain;
        var targetService = await context.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(service => service.Id == model.TargetServiceId);
        ValidateTarget(model, alias.Domain, targetService);
        if (!ModelState.IsValid)
        {
            model.AllServices = await LoadServicesAsync();
            return this.StackView(model);
        }

        DomainAliasRedirectEvaluator.TryNormalizeTargetUrl(model.TargetUrl, out var normalizedTargetUrl, out _);
        alias.TargetServiceId = model.TargetServiceId;
        alias.TargetUrl = normalizedTargetUrl;
        alias.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        TriggerAudit();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var alias = await context.DomainAliases.FindAsync(id);
        if (alias == null)
        {
            return NotFound();
        }

        context.DomainAliases.Remove(alias);
        await context.SaveChangesAsync();
        TriggerAudit();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RefreshAudit()
    {
        TriggerAudit();
        return RedirectToAction(nameof(Index));
    }

    private async Task<List<string>> LoadAvailableSourceDomainsAsync()
    {
        var registeredDomains = (await context.Services
                .AsNoTracking()
                .Select(service => service.Domain)
                .ToListAsync())
            .Concat(await context.DomainAliases
                .AsNoTracking()
                .Select(alias => alias.Domain)
                .ToListAsync())
            .Select(DnsAuditAnalyzer.NormalizeDomain)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return snapshotCache.Current.Report?.Issues
            .Where(issue => issue.Type == DnsAuditIssueType.UnknownDns)
            .Select(issue => DnsAuditAnalyzer.NormalizeDomain(issue.Domain))
            .Where(domain => domain.Length > 0 && !registeredDomains.Contains(domain))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(domain => domain)
            .ToList() ?? [];
    }

    private void ValidateTarget(DomainAliasFormViewModel model, string sourceDomain, Service? targetService)
    {
        if (targetService == null)
        {
            ModelState.AddModelError(nameof(model.TargetServiceId), "Select an existing target service.");
            return;
        }

        if (!DomainAliasRedirectEvaluator.TryNormalizeTargetUrl(model.TargetUrl, out var normalizedTargetUrl, out _))
        {
            return;
        }

        var targetHost = DnsAuditAnalyzer.NormalizeDomain(normalizedTargetUrl);
        var serviceHost = DnsAuditAnalyzer.NormalizeDomain(targetService.Domain);
        if (!targetHost.Equals(serviceHost, StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.TargetUrl),
                $"The target URL hostname must match the selected service '{serviceHost}'.");
        }

        if (sourceDomain.Equals(targetHost, StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.TargetUrl), "A domain alias cannot redirect to itself.");
        }
    }

    private Task<List<Service>> LoadServicesAsync() => context.Services
        .AsNoTracking()
        .OrderBy(service => service.Domain)
        .ToListAsync();

    private void TriggerAudit() => jobRegistry.TriggerNow(nameof(DnsAuditJob));
}
