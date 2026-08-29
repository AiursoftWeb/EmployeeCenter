using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.ServersViewModels;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.UiStack.Navigation;
using Aiursoft.WebTools.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Microsoft.Extensions.Localization;

namespace Aiursoft.EmployeeCenter.Controllers;

[Authorize(Policy = AppPermissionNames.CanManageServices)]
[LimitPerMin]
public class ServersController(
    EmployeeCenterDbContext context,
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
    public async Task<IActionResult> Index()
    {
        var servers = await context.Servers
            .Include(s => s.Location)
            .Include(s => s.Owner)
            .Include(s => s.Provider)
            .Include(s => s.CompanyEntity)
            .OrderBy(s => s.Hostname)
            .ToListAsync();

        var serviceAssociations = await context.Services
            .AsNoTracking()
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
            AssociatedServiceCounts = associatedServiceCounts,
            PageTitle = localizer["Servers"]
        });
    }

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
    public async Task<IActionResult> Create(CreateServerViewModel model)
    {
        if (ModelState.IsValid)
        {
            var server = new Server
            {
                ServerIp = model.ServerIp,
                Ipv6Address = model.Ipv6Address,
                DetailLink = model.DetailLink,
                LocationId = model.LocationId,
                Hostname = model.Hostname,
                OwnerId = model.OwnerId,
                ProviderId = model.ProviderId,
                CompanyEntityId = model.CompanyEntityId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.Servers.Add(server);
            await context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        model.AllLocations = await context.Locations.ToListAsync();
        model.AllOwners = await context.Users.ToListAsync();
        model.AllProviders = await context.Providers.ToListAsync();
        model.AllCompanyEntities = await context.CompanyEntities.ToListAsync();
        return this.StackView(model);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var server = await context.Servers.FindAsync(id);
        if (server == null) return NotFound();

        return this.StackView(new EditServerViewModel
        {
            Id = server.Id,
            ServerIp = server.ServerIp,
            Ipv6Address = server.Ipv6Address,
            DetailLink = server.DetailLink,
            LocationId = server.LocationId,
            Hostname = server.Hostname,
            OwnerId = server.OwnerId,
            ProviderId = server.ProviderId,
            CompanyEntityId = server.CompanyEntityId,
            AllLocations = await context.Locations.ToListAsync(),
            AllOwners = await context.Users.ToListAsync(),
            AllProviders = await context.Providers.ToListAsync(),
            AllCompanyEntities = await context.CompanyEntities.ToListAsync(),
            PageTitle = localizer["Edit Server"]
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditServerViewModel model)
    {
        var server = await context.Servers.FindAsync(model.Id);
        if (server == null) return NotFound();

        if (ModelState.IsValid)
        {
            server.ServerIp = model.ServerIp;
            server.Ipv6Address = model.Ipv6Address;
            server.DetailLink = model.DetailLink;
            server.LocationId = model.LocationId;
            server.Hostname = model.Hostname;
            server.OwnerId = model.OwnerId;
            server.ProviderId = model.ProviderId;
            server.CompanyEntityId = model.CompanyEntityId;
            server.UpdatedAt = DateTime.UtcNow;

            await context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        model.AllLocations = await context.Locations.ToListAsync();
        model.AllOwners = await context.Users.ToListAsync();
        model.AllProviders = await context.Providers.ToListAsync();
        model.AllCompanyEntities = await context.CompanyEntities.ToListAsync();
        return this.StackView(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var server = await context.Servers
            .Include(s => s.Services)
            .Include(s => s.FrpsServices)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (server == null) return NotFound();

        if (server.Services.Any() || server.FrpsServices.Any())
        {
            return BadRequest("Cannot delete a server that is being used by services.");
        }

        context.Servers.Remove(server);
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
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
    public async Task<IActionResult> GetUsers()
    {
        var users = await context.Users
            .OrderBy(u => u.DisplayName)
            .ToListAsync();
        return Json(users.Select(u => new { u.Id, u.DisplayName }));
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
    public async Task<IActionResult> GetCompanyEntities()
    {
        var companyEntities = await context.CompanyEntities
            .OrderBy(c => c.CompanyName)
            .ToListAsync();
        return Json(companyEntities.Select(c => new { c.Id, Name = c.CompanyName }));
    }
}
