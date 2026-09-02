using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.ServicesViewModels;
using Aiursoft.EmployeeCenter.Models.InfrastructureViewModels;
using Aiursoft.EmployeeCenter.Models.DnsAuditViewModels;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.EmployeeCenter.Services.DnsAudit;
using Aiursoft.UiStack.Navigation;
using Aiursoft.WebTools.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Microsoft.Extensions.Localization;

namespace Aiursoft.EmployeeCenter.Controllers;

[Authorize]
[LimitPerMin]
public class ServicesController(
    EmployeeCenterDbContext context,
    DnsAuditSnapshotCache dnsAuditSnapshotCache,
    ServiceAuditStore auditStore,
    IAuthorizationService authorizationService,
    InfrastructureChangeLogService changeLog,
    InfrastructureDataQualityService dataQualityService,
    UserManager<User> userManager,
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
    [Authorize(Policy = AppPermissionNames.CanViewInfrastructure)]
    public async Task<IActionResult> Index(int? serverId = null)
    {
        if (serverId.HasValue)
        {
            return RedirectToAction(nameof(List), new { serverId });
        }

        var services = await context.Services
            .AsNoTracking()
            .Where(service => service.RetiredAt == null)
            .Include(service => service.Server)
            .ThenInclude(server => server!.Location)
            .Include(service => service.DnsProvider)
            .ToListAsync();

        var assignedServices = services.Count(service => service.ServerId.HasValue);
        var dnsAssignedServices = services.Count(service => service.DnsProviderId.HasValue);
        var canViewAudit = (await authorizationService.AuthorizeAsync(
            User,
            AppPermissionNames.CanViewServiceAudit)).Succeeded;
        var auditSnapshot = canViewAudit
            ? await auditStore.LoadSnapshotAsync(dnsAuditSnapshotCache.Current)
            : null;
        var auditHealth = auditSnapshot?.Report == null
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
            CanViewAudit = canViewAudit,
            OperationalPercentage = auditHealth?.Percentage,
            OperationalHealthySubjectCount = auditHealth?.HealthySubjectCount,
            OperationalSubjectCount = auditHealth?.TotalSubjectCount,
            DnsAuditCriticalCount = auditSnapshot?.Report?.CriticalCount,
            DnsAuditErrorCount = auditSnapshot?.Report?.ErrorCount,
            DnsAuditWarningCount = auditSnapshot?.Report?.WarningCount,
            DnsAuditGeneratedAt = auditSnapshot?.LastSuccessfulAt,
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

    [Authorize(Policy = AppPermissionNames.CanViewInfrastructure)]
    public async Task<IActionResult> List(int? serverId = null, bool includeRetired = false)
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
            .Include(s => s.CompanyEntity)
            .Include(s => s.AlternativeService)
            .Include(s => s.DnsProvider)
            .Include(s => s.Server)
            .ThenInclude(s => s!.Location)
            .Include(s => s.FrpsServer)
            .AsQueryable();
        if (!includeRetired)
        {
            servicesQuery = servicesQuery.Where(service => service.RetiredAt == null);
        }
        if (serverId.HasValue)
        {
            servicesQuery = servicesQuery.Where(service =>
                service.ServerId == serverId.Value || service.FrpsServerId == serverId.Value);
        }

        var services = await servicesQuery
            .OrderBy(s => s.PrimaryDomain)
            .ToListAsync();
        var canViewAudit = (await authorizationService.AuthorizeAsync(
            User,
            AppPermissionNames.CanViewServiceAudit)).Succeeded;
        var observations = canViewAudit
            ? (await auditStore.LoadSnapshotAsync(dnsAuditSnapshotCache.Current)).Report?.Observations
                .Where(observation => observation.ServiceId > 0)
                .GroupBy(observation => observation.ServiceId)
                .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.ObservedAt).First())
              ?? new Dictionary<int, ServiceAuditObservationResult>()
            : new Dictionary<int, ServiceAuditObservationResult>();

        return this.StackView(new IndexViewModel
        {
            Services = services,
            FilteredServer = filteredServer,
            CanViewAudit = canViewAudit,
            IncludeRetired = includeRetired,
            LatestObservations = observations,
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

    [Authorize(Policy = AppPermissionNames.CanManageInfrastructure)]
    public async Task<IActionResult> DataQuality()
    {
        return this.StackView(new InfrastructureDataQualityViewModel
        {
            Issues = await dataQualityService.ScanAsync(),
            GeneratedAt = DateTime.UtcNow,
            PageTitle = localizer["Infrastructure data quality"]
        });
    }

    [Authorize(Policy = AppPermissionNames.CanManageInfrastructure)]
    public async Task<IActionResult> Create()
    {
        return this.StackView(new CreateServiceViewModel
        {
            AllOwners = await context.CompanyEntities.ToListAsync(),
            AllDnsProviders = await context.DnsProviders.ToListAsync(),
            AllServices = await context.Services.Where(service => service.RetiredAt == null).ToListAsync(),
            AllServers = await context.Servers.Where(server => server.RetiredAt == null).ToListAsync()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanManageInfrastructure)]
    public async Task<IActionResult> Create(CreateServiceViewModel model)
    {
        var normalizedDomain = await ValidateServiceModelAsync(model, null);
        if (ModelState.IsValid)
        {
            var now = DateTime.UtcNow;
            var service = new Service
            {
                Name = model.Name.Trim(),
                PrimaryDomain = normalizedDomain!,
                NormalizedPrimaryDomain = normalizedDomain,
                CompanyEntityId = model.CompanyEntityId,
                AlternativeServiceId = model.AlternativeServiceId,
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
                IsRegistryValidated = true,
                ConcurrencyToken = Guid.NewGuid().ToString(),
                CreatedAt = now,
                UpdatedAt = now
            };
            await using var transaction = context.Database.IsRelational()
                ? await context.Database.BeginTransactionAsync()
                : null;
            context.Services.Add(service);
            await context.SaveChangesAsync();
            changeLog.Add(
                nameof(Service),
                service.Id,
                "Created",
                null,
                InfrastructureChangeLogService.Snapshot(service),
                userManager.GetUserId(User));
            await context.SaveChangesAsync();
            if (transaction != null)
            {
                await transaction.CommitAsync();
            }
            return RedirectToAction(nameof(List));
        }

        await PopulateServiceOptionsAsync(model);
        return this.StackView(model);
    }

    [Authorize(Policy = AppPermissionNames.CanManageInfrastructure)]
    public async Task<IActionResult> Edit(int id)
    {
        var service = await context.Services.FindAsync(id);
        if (service == null) return NotFound();
        if (service.RetiredAt.HasValue) return BadRequest("A retired service cannot be edited.");

        return this.StackView(new EditServiceViewModel
        {
            Id = service.Id,
            Name = service.Name ?? service.PrimaryDomain,
            PrimaryDomain = service.PrimaryDomain,
            CompanyEntityId = service.CompanyEntityId,
            AlternativeServiceId = service.AlternativeServiceId,
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
            ConcurrencyToken = service.ConcurrencyToken,
            AllOwners = await context.CompanyEntities.ToListAsync(),
            AllDnsProviders = await context.DnsProviders.ToListAsync(),
            AllServices = await context.Services
                .Where(s => s.Id != id && s.RetiredAt == null)
                .ToListAsync(),
            AllServers = await context.Servers.Where(server => server.RetiredAt == null).ToListAsync()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanManageInfrastructure)]
    public async Task<IActionResult> Edit(EditServiceViewModel model)
    {
        var service = await context.Services.FindAsync(model.Id);
        if (service == null) return NotFound();
        if (service.RetiredAt.HasValue) return BadRequest("A retired service cannot be edited.");

        var normalizedDomain = await ValidateServiceModelAsync(model, model.Id);
        if (ModelState.IsValid)
        {
            var before = InfrastructureChangeLogService.Snapshot(service);
            context.Entry(service).Property(item => item.ConcurrencyToken).OriginalValue = model.ConcurrencyToken;
            service.Name = model.Name.Trim();
            service.PrimaryDomain = normalizedDomain!;
            service.NormalizedPrimaryDomain = normalizedDomain;
            service.CompanyEntityId = model.CompanyEntityId;
            service.AlternativeServiceId = model.AlternativeServiceId;
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
            service.IsRegistryValidated = true;
            service.ConcurrencyToken = Guid.NewGuid().ToString();
            service.UpdatedAt = DateTime.UtcNow;

            changeLog.Add(
                nameof(Service),
                service.Id,
                "Updated",
                before,
                InfrastructureChangeLogService.Snapshot(service),
                userManager.GetUserId(User));
            try
            {
                await context.SaveChangesAsync();
                return RedirectToAction(nameof(List));
            }
            catch (DbUpdateConcurrencyException)
            {
                ModelState.AddModelError(string.Empty,
                    "This service was changed by another user. Reload the page and apply your changes again.");
            }
        }

        await PopulateServiceOptionsAsync(model, model.Id);
        return this.StackView(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanManageInfrastructure)]
    public async Task<IActionResult> Delete(int id, string? concurrencyToken)
    {
        var service = await context.Services.FindAsync(id);
        if (service == null) return NotFound();
        if (service.RetiredAt.HasValue) return RedirectToAction(nameof(List));

        var before = InfrastructureChangeLogService.Snapshot(service);
        context.Entry(service).Property(item => item.ConcurrencyToken).OriginalValue = concurrencyToken;
        service.Status = ServiceStatus.Retired;
        service.RetiredAt = DateTime.UtcNow;
        service.RetiredByUserId = userManager.GetUserId(User);
        service.ConcurrencyToken = Guid.NewGuid().ToString();
        service.UpdatedAt = DateTime.UtcNow;
        changeLog.Add(
            nameof(Service),
            service.Id,
            "Retired",
            before,
            InfrastructureChangeLogService.Snapshot(service),
            userManager.GetUserId(User));
        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict("This service changed before it could be retired. Reload and try again.");
        }
        return RedirectToAction(nameof(List));
    }

    [Authorize(Policy = AppPermissionNames.CanManageInfrastructure)]
    public async Task<IActionResult> DnsProviders()
    {
        return this.StackView(new ManageDnsProvidersViewModel
        {
            DnsProviders = await context.DnsProviders.ToListAsync()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanManageInfrastructure)]
    public async Task<IActionResult> CreateDnsProvider(ManageDnsProvidersViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.DnsProviders = await context.DnsProviders.OrderBy(provider => provider.Name).ToListAsync();
            return this.StackView(model, nameof(DnsProviders));
        }

        if (!string.IsNullOrWhiteSpace(model.NewName))
        {
            var normalizedName = InfrastructureValueNormalizer.NormalizeName(model.NewName);
            if (normalizedName.Length > 100)
            {
                ModelState.AddModelError(nameof(model.NewName),
                    "The normalized DNS provider name cannot exceed 100 characters.");
                model.DnsProviders = await context.DnsProviders.OrderBy(provider => provider.Name).ToListAsync();
                return this.StackView(model, nameof(DnsProviders));
            }
            var duplicate = (await context.DnsProviders
                    .Select(provider => new { provider.Name, provider.NormalizedName })
                    .ToListAsync())
                .Any(provider => provider.NormalizedName == normalizedName ||
                                 InfrastructureValueNormalizer.NormalizeName(provider.Name) == normalizedName);
            if (duplicate)
            {
                ModelState.AddModelError(nameof(model.NewName), "A DNS provider with this name already exists.");
                model.DnsProviders = await context.DnsProviders.OrderBy(provider => provider.Name).ToListAsync();
                return this.StackView(model, nameof(DnsProviders));
            }

            var provider = new DnsProvider
            {
                Name = model.NewName.Trim(),
                NormalizedName = normalizedName,
                Description = model.NewDescription
            };
            await using var transaction = context.Database.IsRelational()
                ? await context.Database.BeginTransactionAsync()
                : null;
            context.DnsProviders.Add(provider);
            await context.SaveChangesAsync();
            changeLog.Add(nameof(DnsProvider), provider.Id, "Created", null,
                new { provider.Name, provider.Description }, userManager.GetUserId(User));
            await context.SaveChangesAsync();
            if (transaction != null)
            {
                await transaction.CommitAsync();
            }
        }
        return RedirectToAction(nameof(DnsProviders));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanManageInfrastructure)]
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

        changeLog.Add(nameof(DnsProvider), provider.Id, "Deleted",
            new { provider.Name, provider.Description }, null, userManager.GetUserId(User));
        context.DnsProviders.Remove(provider);
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(DnsProviders));
    }

    [Authorize(Policy = AppPermissionNames.CanManageInfrastructure)]
    public async Task<IActionResult> Providers()
    {
        return this.StackView(new ManageProvidersViewModel
        {
            Providers = await context.Providers.ToListAsync()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanManageInfrastructure)]
    public async Task<IActionResult> CreateProvider(ManageProvidersViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.Providers = await context.Providers.OrderBy(provider => provider.Name).ToListAsync();
            return this.StackView(model, nameof(Providers));
        }

        if (!string.IsNullOrWhiteSpace(model.NewName))
        {
            var normalizedName = InfrastructureValueNormalizer.NormalizeName(model.NewName);
            if (normalizedName.Length > 100)
            {
                ModelState.AddModelError(nameof(model.NewName),
                    "The normalized provider name cannot exceed 100 characters.");
                model.Providers = await context.Providers.OrderBy(provider => provider.Name).ToListAsync();
                return this.StackView(model, nameof(Providers));
            }
            var duplicate = (await context.Providers
                    .Select(provider => new { provider.Name, provider.NormalizedName })
                    .ToListAsync())
                .Any(provider => provider.NormalizedName == normalizedName ||
                                 InfrastructureValueNormalizer.NormalizeName(provider.Name) == normalizedName);
            if (duplicate)
            {
                ModelState.AddModelError(nameof(model.NewName), "A provider with this name already exists.");
                model.Providers = await context.Providers.OrderBy(provider => provider.Name).ToListAsync();
                return this.StackView(model, nameof(Providers));
            }

            var provider = new Provider
            {
                Name = model.NewName.Trim(),
                NormalizedName = normalizedName
            };
            await using var transaction = context.Database.IsRelational()
                ? await context.Database.BeginTransactionAsync()
                : null;
            context.Providers.Add(provider);
            await context.SaveChangesAsync();
            changeLog.Add(nameof(Provider), provider.Id, "Created", null,
                new { provider.Name }, userManager.GetUserId(User));
            await context.SaveChangesAsync();
            if (transaction != null)
            {
                await transaction.CommitAsync();
            }
        }
        return RedirectToAction(nameof(Providers));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanManageInfrastructure)]
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

        changeLog.Add(nameof(Provider), provider.Id, "Deleted",
            new { provider.Name }, null, userManager.GetUserId(User));
        context.Providers.Remove(provider);
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Providers));
    }

    [HttpGet]
    [Authorize(Policy = AppPermissionNames.CanManageInfrastructure)]
    public async Task<IActionResult> GetProviders()
    {
        var providers = await context.Providers
            .OrderBy(p => p.Name)
            .ToListAsync();
        return Json(providers.Select(p => new { p.Id, p.Name }));
    }

    [HttpGet]
    [Authorize(Policy = AppPermissionNames.CanManageInfrastructure)]
    public async Task<IActionResult> GetServers()
    {
        var servers = await context.Servers
            .Where(server => server.RetiredAt == null)
            .OrderBy(s => s.Hostname)
            .ToListAsync();
        return Json(servers.Select(s => new { s.Id, s.Hostname }));
    }

    [HttpGet]
    [Authorize(Policy = AppPermissionNames.CanManageInfrastructure)]
    public async Task<IActionResult> GetDnsProviders()
    {
        var providers = await context.DnsProviders
            .OrderBy(p => p.Name)
            .ToListAsync();
        return Json(providers.Select(p => new { p.Id, p.Name }));
    }

    [HttpGet]
    [Authorize(Policy = AppPermissionNames.CanManageInfrastructure)]
    public async Task<IActionResult> GetServices()
    {
        var services = await context.Services
            .Where(service => service.RetiredAt == null)
            .OrderBy(s => s.PrimaryDomain)
            .ToListAsync();
        return Json(services.Select(s => new { s.Id, s.PrimaryDomain }));
    }

    [HttpGet]
    [Authorize(Policy = AppPermissionNames.CanManageInfrastructure)]
    public async Task<IActionResult> GetLocations()
    {
        var locations = await context.Locations
            .OrderBy(l => l.Name)
            .ToListAsync();
        return Json(locations.Select(l => new { l.Id, l.Name }));
    }

    [HttpGet]
    [Authorize(Policy = AppPermissionNames.CanManageInfrastructure)]
    public async Task<IActionResult> GetCompanyEntities()
    {
        var entities = await context.CompanyEntities
            .OrderBy(e => e.CompanyName)
            .ToListAsync();
        return Json(entities.Select(e => new { e.Id, e.CompanyName }));
    }

    private async Task<string?> ValidateServiceModelAsync(CreateServiceViewModel model, int? serviceId)
    {
        string? normalizedDomain = null;
        try
        {
            normalizedDomain = InfrastructureValueNormalizer.NormalizeDomain(model.PrimaryDomain);
            model.PrimaryDomain = normalizedDomain;
        }
        catch (FormatException exception)
        {
            ModelState.AddModelError(nameof(model.PrimaryDomain), exception.Message);
        }

        if (normalizedDomain != null)
        {
            var existingDomains = await context.Services
                .Where(service => !serviceId.HasValue || service.Id != serviceId.Value)
                .Select(service => new { service.Id, service.PrimaryDomain, service.NormalizedPrimaryDomain })
                .ToListAsync();
            if (existingDomains.Any(service =>
                    service.NormalizedPrimaryDomain == normalizedDomain ||
                    TryNormalizeDomain(service.PrimaryDomain) == normalizedDomain))
            {
                ModelState.AddModelError(nameof(model.PrimaryDomain),
                    "Another service already uses this primary domain.");
            }
        }

        if (model.AlternativeServiceId.HasValue)
        {
            var targetExists = await context.Services.AnyAsync(service =>
                service.Id == model.AlternativeServiceId.Value && service.RetiredAt == null);
            if (!targetExists)
            {
                ModelState.AddModelError(nameof(model.AlternativeServiceId),
                    "The selected alternative service does not exist or is retired.");
            }
            else if (serviceId.HasValue &&
                     await WouldCreateAlternativeCycleAsync(serviceId.Value, model.AlternativeServiceId.Value))
            {
                ModelState.AddModelError(nameof(model.AlternativeServiceId),
                    "An alternative service cannot reference itself or create a cycle.");
            }
        }

        if (model.ServerId.HasValue && !await context.Servers.AnyAsync(server =>
                server.Id == model.ServerId.Value && server.RetiredAt == null))
        {
            ModelState.AddModelError(nameof(model.ServerId),
                "The selected running server does not exist or is retired.");
        }

        if (model.IsViaFrps && model.FrpsServerId.HasValue &&
            !await context.Servers.AnyAsync(server =>
                server.Id == model.FrpsServerId.Value && server.RetiredAt == null))
        {
            ModelState.AddModelError(nameof(model.FrpsServerId),
                "The selected FRPS server does not exist or is retired.");
        }

        return normalizedDomain;
    }

    private async Task<bool> WouldCreateAlternativeCycleAsync(int serviceId, int alternativeServiceId)
    {
        var links = await context.Services
            .AsNoTracking()
            .Select(service => new { service.Id, service.AlternativeServiceId })
            .ToDictionaryAsync(service => service.Id, service => service.AlternativeServiceId);
        var visited = new HashSet<int>();
        int? current = alternativeServiceId;
        while (current.HasValue && visited.Add(current.Value))
        {
            if (current.Value == serviceId)
            {
                return true;
            }

            current = links.GetValueOrDefault(current.Value);
        }

        return current.HasValue;
    }

    private async Task PopulateServiceOptionsAsync(CreateServiceViewModel model, int? excludedServiceId = null)
    {
        model.AllOwners = await context.CompanyEntities.OrderBy(entity => entity.CompanyName).ToListAsync();
        model.AllDnsProviders = await context.DnsProviders.OrderBy(provider => provider.Name).ToListAsync();
        model.AllServices = await context.Services
            .Where(service => service.RetiredAt == null &&
                              (!excludedServiceId.HasValue || service.Id != excludedServiceId.Value))
            .OrderBy(service => service.PrimaryDomain)
            .ToListAsync();
        model.AllServers = await context.Servers
            .Where(server => server.RetiredAt == null)
            .OrderBy(server => server.Hostname)
            .ToListAsync();
    }

    private static string? TryNormalizeDomain(string domain)
    {
        try
        {
            return InfrastructureValueNormalizer.NormalizeDomain(domain);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
