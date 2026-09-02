using System.Text.Json;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.Scanner.Abstractions;

namespace Aiursoft.EmployeeCenter.Services;

public sealed class InfrastructureChangeLogService(EmployeeCenterDbContext context) : IScopedDependency
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Add(
        string resourceType,
        int resourceId,
        string action,
        object? before,
        object? after,
        string? actorUserId)
    {
        context.InfrastructureChangeLogs.Add(new InfrastructureChangeLog
        {
            ResourceType = resourceType,
            ResourceId = resourceId,
            Action = action,
            ActorUserId = actorUserId,
            BeforeJson = before == null ? null : JsonSerializer.Serialize(before, JsonOptions),
            AfterJson = after == null ? null : JsonSerializer.Serialize(after, JsonOptions),
            CreatedAt = DateTime.UtcNow
        });
    }

    public static object Snapshot(Service service) => new
    {
        service.Name,
        service.PrimaryDomain,
        service.CompanyEntityId,
        service.AlternativeServiceId,
        service.Protocols,
        service.ServerId,
        service.FrpsServerId,
        service.DnsProviderId,
        service.IsViaFrps,
        service.IsCloudflareProxied,
        service.IsAvailabilityAuditEnabled,
        service.Status,
        service.Purpose,
        service.AuthentikIntegrated,
        service.IsSelfDeveloped,
        service.Remark,
        service.RetiredAt
    };

    public static object Snapshot(Server server) => new
    {
        server.Hostname,
        server.ServerIp,
        server.Ipv6Address,
        server.DetailLink,
        server.LocationId,
        server.TechnicalOwnerId,
        server.ProviderId,
        server.CompanyEntityId,
        server.RetiredAt
    };
}
