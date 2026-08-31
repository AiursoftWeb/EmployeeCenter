using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.ServicesViewModels;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.EmployeeCenter.Services.DnsAudit;
using Aiursoft.UiStack.Navigation;
using Aiursoft.WebTools.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Microsoft.Extensions.Localization;

namespace Aiursoft.EmployeeCenter.Controllers;

[Authorize]
[LimitPerMin]
public class ServicesController(
    EmployeeCenterDbContext context,
    DnsAuditSnapshotCache dnsAuditSnapshotCache,
    IStringLocalizer<ServicesController> localizer)
    : Controller
{
    [RenderInNavBar(
        NavGroupName = "Career",
        NavGroupOrder = 1,
        CascadedLinksGroupName = "Development",
        CascadedLinksIcon = "git-branch",
        CascadedLinksOrder = 2,
        LinkText = "Services",
        LinkOrder = 2)]
    public async Task<IActionResult> Index(int? serverId = null)
    {
        if (serverId.HasValue)
        {
            return RedirectToAction(nameof(List), new { serverId });
        }

        var services = await context.Services
            .AsNoTracking()
            .Include(service => service.Server)
            .ThenInclude(server => server!.Location)
            .Include(service => service.DnsProvider)
            .ToListAsync();

        var assignedServices = services.Count(service => service.ServerId.HasValue);
        var dnsAssignedServices = services.Count(service => service.DnsProviderId.HasValue);
        var auditSnapshot = dnsAuditSnapshotCache.Current;
        var auditHealth = auditSnapshot.Report == null
            ? null
            : DnsAuditHealthCalculator.Calculate(
                services.Select(service => service.Id).ToHashSet(),
                auditSnapshot.Report);
        var serverDistribution = services
            .Where(service => service.ServerId.HasValue)
            .GroupBy(service => new
            {
                service.ServerId,
                Name = service.Server?.Hostname ?? service.Server?.ServerIp ?? $"Server #{service.ServerId}"
            })
            .Select(group => new ServiceDashboardDistributionItem
            {
                Name = group.Key.Name,
                Count = group.Count(),
                ServerId = group.Key.ServerId
            })
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Name)
            .ToList();

        return this.StackView(new ServicesDashboardViewModel
        {
            TotalServices = services.Count,
            RunningServices = services.Count(service => service.Status == ServiceStatus.Running),
            AssignedServices = assignedServices,
            CloudflareProxiedServices = services.Count(service => service.IsCloudflareProxied),
            FrpsServices = services.Count(service => service.IsViaFrps),
            AuthentikIntegratedServices = services.Count(service => service.AuthentikIntegrated),
            SelfDevelopedServices = services.Count(service => service.IsSelfDeveloped),
            ActiveServerCount = serverDistribution.Count,
            ActiveLocationCount = services
                .Where(service => service.Server?.LocationId != null)
                .Select(service => service.Server!.LocationId)
                .Distinct()
                .Count(),
            OperationalPercentage = auditHealth?.Percentage,
            OperationalHealthySubjectCount = auditHealth?.HealthySubjectCount,
            OperationalSubjectCount = auditHealth?.TotalSubjectCount,
            DnsAuditCriticalCount = auditSnapshot.Report?.CriticalCount,
            DnsAuditErrorCount = auditSnapshot.Report?.ErrorCount,
            DnsAuditWarningCount = auditSnapshot.Report?.WarningCount,
            DnsAuditGeneratedAt = auditSnapshot.LastSuccessfulAt,
            DnsProviderPercentage = services.Count == 0
                ? 0
                : Math.Round(dnsAssignedServices * 100.0 / services.Count, 1),
            ServerDistribution = serverDistribution,
            LocationDistribution = BuildDistribution(
                services,
                service => service.Server?.Location?.Name ?? localizer["Unassigned"].Value),
            DnsProviderDistribution = BuildDistribution(
                services,
                service => service.DnsProvider?.Name ?? localizer["Unassigned"].Value),
            StatusDistribution = BuildDistribution(
                services,
                service => localizer[service.Status.ToString()].Value),
            PurposeDistribution = BuildDistribution(
                services,
                service => localizer[service.Purpose.ToString()].Value),
            PageTitle = localizer["Services Dashboard"]
        });
    }

    public async Task<IActionResult> List(int? serverId = null)
    {
        Server? filteredServer = null;
        if (serverId.HasValue)
        {
            filteredServer = await context.Servers
                .AsNoTracking()
                .FirstOrDefaultAsync(server => server.Id == serverId.Value);
            if (filteredServer == null)
            {
                return NotFound();
            }
        }

        var servicesQuery = context.Services
            .Include(s => s.Owner)
            .Include(s => s.CrossEntityLink)
            .Include(s => s.DnsProvider)
            .Include(s => s.Server)
            .ThenInclude(s => s!.Location)
            .Include(s => s.FrpsServer)
            .AsQueryable();
        if (serverId.HasValue)
        {
            servicesQuery = servicesQuery.Where(service =>
                service.ServerId == serverId.Value || service.FrpsServerId == serverId.Value);
        }

        var services = await servicesQuery
            .OrderBy(s => s.Domain)
            .ToListAsync();

        return this.StackView(new IndexViewModel
        {
            Services = services,
            FilteredServer = filteredServer,
            PageTitle = filteredServer == null
                ? localizer["Services"]
                : localizer["Services associated with {0}", filteredServer.Hostname ?? filteredServer.ServerIp ?? filteredServer.Id.ToString()]
        });
    }

    private static List<ServiceDashboardDistributionItem> BuildDistribution(
        IEnumerable<Service> services,
        Func<Service, string> keySelector)
    {
        return services
            .GroupBy(keySelector)
            .Select(group => new ServiceDashboardDistributionItem
            {
                Name = group.Key,
                Count = group.Count()
            })
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Name)
            .ToList();
    }

    [Authorize(Policy = AppPermissionNames.CanManageServices)]
    public async Task<IActionResult> Create()
    {
        return this.StackView(new CreateServiceViewModel
        {
            AllOwners = await context.CompanyEntities.ToListAsync(),
            AllDnsProviders = await context.DnsProviders.ToListAsync(),
            AllServices = await context.Services.ToListAsync(),
            AllServers = await context.Servers.ToListAsync()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanManageServices)]
    public async Task<IActionResult> Create(CreateServiceViewModel model)
    {
        if (ModelState.IsValid)
        {
            var service = new Service
            {
                Domain = model.Domain,
                OwnerId = model.OwnerId,
                CrossEntityLinkId = model.CrossEntityLinkId,
                Protocols = model.Protocols,
                ServerId = model.ServerId,
                FrpsServerId = model.IsViaFrps ? model.FrpsServerId : null,
                DnsProviderId = model.DnsProviderId,
                IsViaFrps = model.IsViaFrps,
                IsCloudflareProxied = model.IsCloudflareProxied,
                IsAvailabilityAuditEnabled = model.IsAvailabilityAuditEnabled,
                Status = model.Status,
                Purpose = model.Purpose,
                AuthentikIntegrated = model.AuthentikIntegrated,
                IsSelfDeveloped = model.IsSelfDeveloped,
                Remark = model.Remark,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.Services.Add(service);
            await context.SaveChangesAsync();
            return RedirectToAction(nameof(List));
        }

        model.AllOwners = await context.CompanyEntities.ToListAsync();
        model.AllDnsProviders = await context.DnsProviders.ToListAsync();
        model.AllServices = await context.Services.ToListAsync();
        model.AllServers = await context.Servers.ToListAsync();
        return this.StackView(model);
    }

    [Authorize(Policy = AppPermissionNames.CanManageServices)]
    public async Task<IActionResult> Edit(int id)
    {
        var service = await context.Services.FindAsync(id);
        if (service == null) return NotFound();

        return this.StackView(new EditServiceViewModel
        {
            Id = service.Id,
            Domain = service.Domain,
            OwnerId = service.OwnerId,
            CrossEntityLinkId = service.CrossEntityLinkId,
            Protocols = service.Protocols,
            ServerId = service.ServerId,
            FrpsServerId = service.FrpsServerId,
            DnsProviderId = service.DnsProviderId,
            IsViaFrps = service.IsViaFrps,
            IsCloudflareProxied = service.IsCloudflareProxied,
            IsAvailabilityAuditEnabled = service.IsAvailabilityAuditEnabled,
            Status = service.Status,
            Purpose = service.Purpose,
            AuthentikIntegrated = service.AuthentikIntegrated,
            IsSelfDeveloped = service.IsSelfDeveloped,
            Remark = service.Remark,
            AllOwners = await context.CompanyEntities.ToListAsync(),
            AllDnsProviders = await context.DnsProviders.ToListAsync(),
            AllServices = await context.Services.Where(s => s.Id != id).ToListAsync(),
            AllServers = await context.Servers.ToListAsync()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanManageServices)]
    public async Task<IActionResult> Edit(EditServiceViewModel model)
    {
        var service = await context.Services.FindAsync(model.Id);
        if (service == null) return NotFound();

        if (ModelState.IsValid)
        {
            service.Domain = model.Domain;
            service.OwnerId = model.OwnerId;
            service.CrossEntityLinkId = model.CrossEntityLinkId;
            service.Protocols = model.Protocols;
            service.ServerId = model.ServerId;
            service.FrpsServerId = model.IsViaFrps ? model.FrpsServerId : null;
            service.DnsProviderId = model.DnsProviderId;
            service.IsViaFrps = model.IsViaFrps;
            service.IsCloudflareProxied = model.IsCloudflareProxied;
            service.IsAvailabilityAuditEnabled = model.IsAvailabilityAuditEnabled;
            service.Status = model.Status;
            service.Purpose = model.Purpose;
            service.AuthentikIntegrated = model.AuthentikIntegrated;
            service.IsSelfDeveloped = model.IsSelfDeveloped;
            service.Remark = model.Remark;
            service.UpdatedAt = DateTime.UtcNow;

            await context.SaveChangesAsync();
            return RedirectToAction(nameof(List));
        }

        model.AllOwners = await context.CompanyEntities.ToListAsync();
        model.AllDnsProviders = await context.DnsProviders.ToListAsync();
        model.AllServices = await context.Services.Where(s => s.Id != model.Id).ToListAsync();
        model.AllServers = await context.Servers.ToListAsync();
        return this.StackView(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanManageServices)]
    public async Task<IActionResult> Delete(int id)
    {
        var service = await context.Services.FindAsync(id);
        if (service == null) return NotFound();

        // Check if any service links to this one
        if (await context.Services.AnyAsync(s => s.CrossEntityLinkId == id))
        {
            return BadRequest("Cannot delete a service that is linked by another service.");
        }

        if (await context.DomainAliases.AnyAsync(alias => alias.TargetServiceId == id))
        {
            return BadRequest("Cannot delete a service that is targeted by a domain alias.");
        }

        context.Services.Remove(service);
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(List));
    }

    [Authorize(Policy = AppPermissionNames.CanManageServices)]
    public async Task<IActionResult> DnsProviders()
    {
        return this.StackView(new ManageDnsProvidersViewModel
        {
            DnsProviders = await context.DnsProviders.ToListAsync()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanManageServices)]
    public async Task<IActionResult> CreateDnsProvider(ManageDnsProvidersViewModel model)
    {
        if (!string.IsNullOrWhiteSpace(model.NewName))
        {
            context.DnsProviders.Add(new DnsProvider
            {
                Name = model.NewName,
                Description = model.NewDescription
            });
            await context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(DnsProviders));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanManageServices)]
    public async Task<IActionResult> DeleteDnsProvider(int id)
    {
        var provider = await context.DnsProviders
            .Include(p => p.Services)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (provider == null) return NotFound();

        if (provider.Services.Any())
        {
            return BadRequest("Cannot delete a DNS provider that is being used by services.");
        }

        context.DnsProviders.Remove(provider);
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(DnsProviders));
    }

    [Authorize(Policy = AppPermissionNames.CanManageServices)]
    public async Task<IActionResult> Providers()
    {
        return this.StackView(new ManageProvidersViewModel
        {
            Providers = await context.Providers.ToListAsync()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanManageServices)]
    public async Task<IActionResult> CreateProvider(ManageProvidersViewModel model)
    {
        if (!string.IsNullOrWhiteSpace(model.NewName))
        {
            context.Providers.Add(new Provider
            {
                Name = model.NewName
            });
            await context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Providers));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanManageServices)]
    public async Task<IActionResult> DeleteProvider(int id)
    {
        var provider = await context.Providers
            .Include(p => p.Servers)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (provider == null) return NotFound();

        if (provider.Servers.Any())
        {
            return BadRequest("Cannot delete a provider that is being used by servers.");
        }

        context.Providers.Remove(provider);
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Providers));
    }

    [HttpGet]
    public async Task<IActionResult> GetProviders()
    {
        var providers = await context.Providers
            .OrderBy(p => p.Name)
            .ToListAsync();
        return Json(providers.Select(p => new { p.Id, p.Name }));
    }

    [HttpGet]
    public async Task<IActionResult> GetServers()
    {
        var servers = await context.Servers
            .OrderBy(s => s.Hostname)
            .ToListAsync();
        return Json(servers.Select(s => new { s.Id, s.Hostname }));
    }

    [HttpGet]
    public async Task<IActionResult> GetDnsProviders()
    {
        var providers = await context.DnsProviders
            .OrderBy(p => p.Name)
            .ToListAsync();
        return Json(providers.Select(p => new { p.Id, p.Name }));
    }

    [HttpGet]
    public async Task<IActionResult> GetServices()
    {
        var services = await context.Services
            .OrderBy(s => s.Domain)
            .ToListAsync();
        return Json(services.Select(s => new { s.Id, s.Domain }));
    }

    [HttpGet]
    public async Task<IActionResult> GetLocations()
    {
        var locations = await context.Locations
            .OrderBy(l => l.Name)
            .ToListAsync();
        return Json(locations.Select(l => new { l.Id, l.Name }));
    }

    [HttpGet]
    public async Task<IActionResult> GetCompanyEntities()
    {
        var entities = await context.CompanyEntities
            .OrderBy(e => e.CompanyName)
            .ToListAsync();
        return Json(entities.Select(e => new { e.Id, e.CompanyName }));
    }
}
