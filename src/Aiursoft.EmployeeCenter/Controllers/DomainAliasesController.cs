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
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.EmployeeCenter.Controllers;

[Authorize]
[LimitPerMin]
public sealed class DomainAliasesController(
    EmployeeCenterDbContext context,
    DnsAuditSnapshotCache snapshotCache,
    ServiceAuditStore auditStore,
    IAuthorizationService authorizationService,
    InfrastructureChangeLogService changeLog,
    UserManager<User> userManager,
    BackgroundJobRegistry jobRegistry) : Controller
{
    [Authorize(Policy = AppPermissionNames.CanViewInfrastructure)]
    public async Task<IActionResult> Index()
    {
        var aliases = await context.DomainAliases
            .AsNoTracking()
            .Include(alias => alias.TargetService)
            .OrderBy(alias => alias.Domain)
            .ToListAsync();
        var canViewAudit = (await authorizationService.AuthorizeAsync(
            User,
            AppPermissionNames.CanViewServiceAudit)).Succeeded;
        var snapshot = canViewAudit
            ? await auditStore.LoadSnapshotAsync(snapshotCache.Current)
            : new DnsAuditSnapshot();
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
            CanViewAudit = canViewAudit,
            AvailableSourceDomains = canViewAudit ? await LoadAvailableSourceDomainsAsync() : [],
            HealthyAliasCount = auditedAliases.Count(alias => !failedAliasIds.Contains(alias.Id)),
            UnhealthyAliasCount = auditedAliases.Count(alias => failedAliasIds.Contains(alias.Id)),
            PendingAliasCount = aliases.Count - auditedAliases.Count,
            UnhealthyAliasIds = failedAliasIds,
            LastSuccessfulAuditAt = snapshot.LastSuccessfulAt
        });
    }

    [Authorize(Policy = AppPermissionNames.CanManageInfrastructure)]
    [Authorize(Policy = AppPermissionNames.CanViewServiceAudit)]
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
    [Authorize(Policy = AppPermissionNames.CanManageInfrastructure)]
    [Authorize(Policy = AppPermissionNames.CanViewServiceAudit)]
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
            .FirstOrDefaultAsync(service => service.Id == model.TargetServiceId && service.RetiredAt == null);
        ValidateTarget(model, domain, targetService);

        if (await context.DomainAliases.AnyAsync(alias => alias.Domain == domain))
        {
            ModelState.AddModelError(nameof(model.Domain), "This hostname is already registered as a domain alias.");
        }

        if (await context.Services.AnyAsync(service => service.PrimaryDomain == domain))
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

        var normalizedTargetUrl = NormalizeTargetUrl(model);
        var alias = new DomainAlias
        {
            Domain = domain,
            TargetServiceId = model.TargetServiceId,
            Type = model.Type,
            TargetUrl = normalizedTargetUrl,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync()
            : null;
        context.DomainAliases.Add(alias);
        await context.SaveChangesAsync();
        changeLog.Add(nameof(DomainAlias), alias.Id, "Created", null,
            Snapshot(alias), userManager.GetUserId(User));
        await context.SaveChangesAsync();
        if (transaction != null)
        {
            await transaction.CommitAsync();
        }
        TriggerAudit();
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = AppPermissionNames.CanManageInfrastructure)]
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
            Type = alias.Type,
            TargetUrl = alias.TargetUrl,
            AllServices = await LoadServicesAsync()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanManageInfrastructure)]
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
            .FirstOrDefaultAsync(service => service.Id == model.TargetServiceId && service.RetiredAt == null);
        ValidateTarget(model, alias.Domain, targetService);
        if (!ModelState.IsValid)
        {
            model.AllServices = await LoadServicesAsync();
            return this.StackView(model);
        }

        var normalizedTargetUrl = NormalizeTargetUrl(model);
        var before = Snapshot(alias);
        alias.TargetServiceId = model.TargetServiceId;
        alias.Type = model.Type;
        alias.TargetUrl = normalizedTargetUrl;
        alias.UpdatedAt = DateTime.UtcNow;
        changeLog.Add(nameof(DomainAlias), alias.Id, "Updated", before,
            Snapshot(alias), userManager.GetUserId(User));
        await context.SaveChangesAsync();
        TriggerAudit();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanManageInfrastructure)]
    public async Task<IActionResult> Delete(int id)
    {
        var alias = await context.DomainAliases.FindAsync(id);
        if (alias == null)
        {
            return NotFound();
        }

        changeLog.Add(nameof(DomainAlias), alias.Id, "Deleted", Snapshot(alias), null,
            userManager.GetUserId(User));
        context.DomainAliases.Remove(alias);
        await context.SaveChangesAsync();
        TriggerAudit();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanRunServiceAudit)]
    public async Task<IActionResult> RefreshAudit()
    {
        await auditStore.QueueAsync(userManager.GetUserId(User));
        TriggerAudit();
        return RedirectToAction(nameof(Index));
    }

    private async Task<List<string>> LoadAvailableSourceDomainsAsync()
    {
        var registeredDomains = (await context.Services
                .AsNoTracking()
                .Select(service => service.PrimaryDomain)
                .ToListAsync())
            .Concat(await context.DomainAliases
                .AsNoTracking()
                .Select(alias => alias.Domain)
                .ToListAsync())
            .Select(DnsAuditAnalyzer.NormalizeDomain)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var snapshot = await auditStore.LoadSnapshotAsync(snapshotCache.Current);
        return snapshot.Report?.Issues
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

        var serviceHost = DnsAuditAnalyzer.NormalizeDomain(targetService.PrimaryDomain);
        if (sourceDomain.Equals(serviceHost, StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.TargetServiceId), "A domain alias cannot target itself.");
            return;
        }

        if (model.Type == DomainAliasType.Cname)
        {
            return;
        }

        if (model.Type != DomainAliasType.HttpRedirect ||
            !DomainAliasRedirectEvaluator.TryNormalizeTargetUrl(model.TargetUrl, out var normalizedTargetUrl, out _))
        {
            return;
        }

        var targetHost = DnsAuditAnalyzer.NormalizeDomain(normalizedTargetUrl);
        if (!targetHost.Equals(serviceHost, StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.TargetUrl),
                $"The target URL hostname must match the selected service '{serviceHost}'.");
        }

    }

    private static string? NormalizeTargetUrl(DomainAliasFormViewModel model)
    {
        if (model.Type != DomainAliasType.HttpRedirect)
        {
            return null;
        }

        DomainAliasRedirectEvaluator.TryNormalizeTargetUrl(model.TargetUrl, out var normalizedTargetUrl, out _);
        return normalizedTargetUrl;
    }

    private Task<List<Service>> LoadServicesAsync() => context.Services
        .AsNoTracking()
        .Where(service => service.RetiredAt == null)
        .OrderBy(service => service.PrimaryDomain)
        .ToListAsync();

    private void TriggerAudit() => jobRegistry.TriggerNow(nameof(DnsAuditJob));

    private static object Snapshot(DomainAlias alias) => new
    {
        alias.Domain,
        alias.TargetServiceId,
        alias.Type,
        alias.TargetUrl,
        alias.UpdatedAt
    };
}
