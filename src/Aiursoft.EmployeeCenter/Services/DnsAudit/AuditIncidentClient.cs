using Aiursoft.AlertCenter.SDK;
using Aiursoft.AlertCenter.SDK.Models;

namespace Aiursoft.EmployeeCenter.Services.DnsAudit;

public interface IAuditIncidentClient
{
    Task<QueryIncidentsResponse> QueryAsync(QueryIncidentsRequest request);
    Task<CreateIncidentResponse> CreateAsync(CreateIncidentRequest request);
    Task<ResolveIncidentResponse> ResolveAsync(Guid incidentId, ResolveIncidentRequest request);
}

public sealed class AuditIncidentClient(AlertCenterApiClient client) : IAuditIncidentClient
{
    public Task<QueryIncidentsResponse> QueryAsync(QueryIncidentsRequest request) => client.QueryIncidentsAsync(request);
    public Task<CreateIncidentResponse> CreateAsync(CreateIncidentRequest request) => client.CreateIncidentAsync(request);
    public Task<ResolveIncidentResponse> ResolveAsync(Guid incidentId, ResolveIncidentRequest request) => client.ResolveIncidentAsync(incidentId, request);
}
