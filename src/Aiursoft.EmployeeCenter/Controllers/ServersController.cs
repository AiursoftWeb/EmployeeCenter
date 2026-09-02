using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.ServersViewModels;
using Aiursoft.EmployeeCenter.Services;
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
public class ServersController(
    EmployeeCenterDbContext context,
    InfrastructureChangeLogService changeLog,
    UserManager<User> userManager,
    IStringLocalizer<ServersController> localizer) : Controller
{
    [RenderInNavBar(
        NavGroupName = "Career",
        NavGroupOrder = 1,
        CascadedLinksGroupName = "Development",
        CascadedLinksIcon = "server",
        CascadedLinksOrder = 3,
        LinkText = "Servers",
        LinkOrder = 3)]
    [Authorize(Policy = AppPermissionNames.CanViewInfrastructure)]
    public async Task<IActionResult> Index(bool includeRetired = false)
    {
        var serverQuery = context.Servers.AsQueryable();
        if (!includeRetired)
        {
            serverQuery = serverQuery.Where(server => server.RetiredAt == null);
        }
        var servers = await serverQuery
            .Include(s => s.Location)
            .Include(s => s.TechnicalOwner)
            .Include(s => s.Provider)
            .Include(s => s.CompanyEntity)
            .OrderBy(s => s.Hostname)
            .ToListAsync();

        var serviceAssociations = await context.Services
            .AsNoTracking()
            .Where(service => service.RetiredAt == null)
            .Select(service => new
            {
                service.Id,
                service.ServerId,
                service.FrpsServerId
            })
            .ToListAsync();
        var associatedServiceCounts = serviceAssociations
            .SelectMany(service => new[] { service.ServerId, service.FrpsServerId }
                .Where(serverId => serverId.HasValue)
                .Select(serverId => new { ServiceId = service.Id, ServerId = serverId!.Value }))
            .Distinct()
            .GroupBy(association => association.ServerId)
            .ToDictionary(group => group.Key, group => group.Count());

        return this.StackView(new IndexServerViewModel
        {
            Servers = servers,
            IncludeRetired = includeRetired,
            AssociatedServiceCounts = associatedServiceCounts,
            PageTitle = localizer["Servers"]
        });
    }

    [Authorize(Policy = AppPermissionNames.CanManageInfrastructure)]
    public async Task<IActionResult> Create()
    {
        return this.StackView(new CreateServerViewModel
        {
            AllLocations = await context.Locations.ToListAsync(),
            AllOwners = await context.Users.ToListAsync(),
            AllProviders = await context.Providers.ToListAsync(),
            AllCompanyEntities = await context.CompanyEntities.ToListAsync(),
            PageTitle = localizer["Create Server"]
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanManageInfrastructure)]
    public async Task<IActionResult> Create(CreateServerViewModel model)
    {
        var normalizedHostname = await ValidateServerModelAsync(model, null);
        if (ModelState.IsValid)
        {
            var now = DateTime.UtcNow;
            var server = new Server
            {
                ServerIp = InfrastructureValueNormalizer.NormalizeOptionalIp(
                    model.ServerIp, System.Net.Sockets.AddressFamily.InterNetwork),
                Ipv6Address = InfrastructureValueNormalizer.NormalizeOptionalIp(
                    model.Ipv6Address, System.Net.Sockets.AddressFamily.InterNetworkV6),
                DetailLink = model.DetailLink,
                LocationId = model.LocationId,
                Hostname = normalizedHostname,
                NormalizedHostname = normalizedHostname,
                TechnicalOwnerId = model.TechnicalOwnerId,
                ProviderId = model.ProviderId,
                CompanyEntityId = model.CompanyEntityId,
                IsRegistryValidated = true,
                ConcurrencyToken = Guid.NewGuid().ToString(),
                CreatedAt = now,
                UpdatedAt = now
            };
            await using var transaction = context.Database.IsRelational()
                ? await context.Database.BeginTransactionAsync()
                : null;
            context.Servers.Add(server);
            await context.SaveChangesAsync();
            changeLog.Add(
                nameof(Server),
                server.Id,
                "Created",
                null,
                InfrastructureChangeLogService.Snapshot(server),
                userManager.GetUserId(User));
            await context.SaveChangesAsync();
            if (transaction != null)
            {
                await transaction.CommitAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        await PopulateServerOptionsAsync(model);
        return this.StackView(model);
    }

    [Authorize(Policy = AppPermissionNames.CanManageInfrastructure)]
    public async Task<IActionResult> Edit(int id)
    {
        var server = await context.Servers.FindAsync(id);
        if (server == null) return NotFound();
        if (server.RetiredAt.HasValue) return BadRequest("A retired server cannot be edited.");

        return this.StackView(new EditServerViewModel
        {
            Id = server.Id,
            ServerIp = server.ServerIp,
            Ipv6Address = server.Ipv6Address,
            DetailLink = server.DetailLink,
            LocationId = server.LocationId,
            Hostname = server.Hostname,
            TechnicalOwnerId = server.TechnicalOwnerId,
            ProviderId = server.ProviderId,
            CompanyEntityId = server.CompanyEntityId,
            ConcurrencyToken = server.ConcurrencyToken,
            AllLocations = await context.Locations.ToListAsync(),
            AllOwners = await context.Users.ToListAsync(),
            AllProviders = await context.Providers.ToListAsync(),
            AllCompanyEntities = await context.CompanyEntities.ToListAsync(),
            PageTitle = localizer["Edit Server"]
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanManageInfrastructure)]
    public async Task<IActionResult> Edit(EditServerViewModel model)
    {
        var server = await context.Servers.FindAsync(model.Id);
        if (server == null) return NotFound();
        if (server.RetiredAt.HasValue) return BadRequest("A retired server cannot be edited.");

        var normalizedHostname = await ValidateServerModelAsync(model, model.Id);
        if (ModelState.IsValid)
        {
            var before = InfrastructureChangeLogService.Snapshot(server);
            context.Entry(server).Property(item => item.ConcurrencyToken).OriginalValue = model.ConcurrencyToken;
            server.ServerIp = InfrastructureValueNormalizer.NormalizeOptionalIp(
                model.ServerIp, System.Net.Sockets.AddressFamily.InterNetwork);
            server.Ipv6Address = InfrastructureValueNormalizer.NormalizeOptionalIp(
                model.Ipv6Address, System.Net.Sockets.AddressFamily.InterNetworkV6);
            server.DetailLink = model.DetailLink;
            server.LocationId = model.LocationId;
            server.Hostname = normalizedHostname;
            server.NormalizedHostname = normalizedHostname;
            server.TechnicalOwnerId = model.TechnicalOwnerId;
            server.ProviderId = model.ProviderId;
            server.CompanyEntityId = model.CompanyEntityId;
            server.IsRegistryValidated = true;
            server.ConcurrencyToken = Guid.NewGuid().ToString();
            server.UpdatedAt = DateTime.UtcNow;

            changeLog.Add(
                nameof(Server),
                server.Id,
                "Updated",
                before,
                InfrastructureChangeLogService.Snapshot(server),
                userManager.GetUserId(User));
            try
            {
                await context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                ModelState.AddModelError(string.Empty,
                    "This server was changed by another user. Reload the page and apply your changes again.");
            }
        }

        await PopulateServerOptionsAsync(model);
        return this.StackView(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanManageInfrastructure)]
    public async Task<IActionResult> Delete(int id, string? concurrencyToken)
    {
        var server = await context.Servers
            .Include(s => s.Services)
            .Include(s => s.FrpsServices)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (server == null) return NotFound();
        if (server.RetiredAt.HasValue) return RedirectToAction(nameof(Index));
        if (server.Services.Any(service => service.RetiredAt == null) ||
            server.FrpsServices.Any(service => service.RetiredAt == null))
        {
            return Conflict("This server is still referenced by active services. Reassign or retire those services first.");
        }

        var before = InfrastructureChangeLogService.Snapshot(server);
        context.Entry(server).Property(item => item.ConcurrencyToken).OriginalValue = concurrencyToken;
        server.RetiredAt = DateTime.UtcNow;
        server.RetiredByUserId = userManager.GetUserId(User);
        server.ConcurrencyToken = Guid.NewGuid().ToString();
        server.UpdatedAt = DateTime.UtcNow;
        changeLog.Add(
            nameof(Server),
            server.Id,
            "Retired",
            before,
            InfrastructureChangeLogService.Snapshot(server),
            userManager.GetUserId(User));
        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict("This server changed before it could be retired. Reload and try again.");
        }
        return RedirectToAction(nameof(Index));
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
    public async Task<IActionResult> GetUsers()
    {
        var users = await context.Users
            .OrderBy(u => u.DisplayName)
            .ToListAsync();
        return Json(users.Select(u => new { u.Id, u.DisplayName }));
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
    public async Task<IActionResult> GetCompanyEntities()
    {
        var companyEntities = await context.CompanyEntities
            .OrderBy(c => c.CompanyName)
            .ToListAsync();
        return Json(companyEntities.Select(c => new { c.Id, Name = c.CompanyName }));
    }

    private async Task<string?> ValidateServerModelAsync(CreateServerViewModel model, int? serverId)
    {
        string? normalizedHostname = null;
        if (!string.IsNullOrWhiteSpace(model.Hostname))
        {
            try
            {
                normalizedHostname = InfrastructureValueNormalizer.NormalizeDomain(model.Hostname);
                model.Hostname = normalizedHostname;
                if (normalizedHostname.Length > 100)
                {
                    ModelState.AddModelError(nameof(model.Hostname),
                        "The normalized hostname cannot exceed 100 characters.");
                }
            }
            catch (FormatException exception)
            {
                ModelState.AddModelError(nameof(model.Hostname), exception.Message);
            }
        }

        if (normalizedHostname != null)
        {
            var existingHostnames = await context.Servers
                .Where(server => !serverId.HasValue || server.Id != serverId.Value)
                .Select(server => new { server.Hostname, server.NormalizedHostname })
                .ToListAsync();
            if (existingHostnames.Any(server =>
                    server.NormalizedHostname == normalizedHostname ||
                    TryNormalizeHostname(server.Hostname) == normalizedHostname))
            {
                ModelState.AddModelError(nameof(model.Hostname),
                    "Another server already uses this hostname.");
            }
        }

        return normalizedHostname;
    }

    private async Task PopulateServerOptionsAsync(CreateServerViewModel model)
    {
        model.AllLocations = await context.Locations.OrderBy(location => location.Name).ToListAsync();
        model.AllOwners = await context.Users.OrderBy(user => user.DisplayName).ToListAsync();
        model.AllProviders = await context.Providers.OrderBy(provider => provider.Name).ToListAsync();
        model.AllCompanyEntities = await context.CompanyEntities.OrderBy(entity => entity.CompanyName).ToListAsync();
    }

    private static string? TryNormalizeHostname(string? hostname)
    {
        try
        {
            return InfrastructureValueNormalizer.NormalizeOptionalHostname(hostname);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
