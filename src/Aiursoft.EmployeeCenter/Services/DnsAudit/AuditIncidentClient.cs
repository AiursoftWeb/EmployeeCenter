using Aiursoft.AiurProtocol.Services;
using Aiursoft.EmployeeCenter.Configuration;
using Microsoft.Extensions.Options;
using Aiursoft.AlertCenter.SDK;
using Aiursoft.AlertCenter.SDK.Models;

namespace Aiursoft.EmployeeCenter.Services.DnsAudit;

public interface IAuditIncidentClient
{
    Task<QueryIncidentsResponse> QueryAsync(QueryIncidentsRequest request);
    Task<CreateIncidentResponse> CreateAsync(CreateIncidentRequest request);
    Task<ResolveIncidentResponse> ResolveAsync(Guid incidentId, ResolveIncidentRequest request);
}

public sealed class AuditIncidentClient(AiurProtocolClient http, IAlertCenterSettingsProvider settingsProvider) : IAuditIncidentClient
{
    private Task<AlertCenterApiClient>? _client;

    private async Task<AlertCenterApiClient> CreateClientAsync()
    {
        var settings = await settingsProvider.GetAsync();
        settings.Validate();
        return new AlertCenterApiClient(http, Options.Create(new AlertCenterApiOptions
        {
            Endpoint = settings.Endpoint.TrimEnd('/'), ApiKey = settings.ApiKey
        }), new SnapshotKeyProvider(settings.ApiKey));
    }

    private Task<AlertCenterApiClient> GetClientAsync() => _client ??= CreateClientAsync();

    public async Task<QueryIncidentsResponse> QueryAsync(QueryIncidentsRequest request) =>
        await (await GetClientAsync()).QueryIncidentsAsync(request);
    public async Task<CreateIncidentResponse> CreateAsync(CreateIncidentRequest request) =>
        await (await GetClientAsync()).CreateIncidentAsync(request);
    public async Task<ResolveIncidentResponse> ResolveAsync(Guid incidentId, ResolveIncidentRequest request) =>
        await (await GetClientAsync()).ResolveIncidentAsync(incidentId, request);

    private sealed class SnapshotKeyProvider(string key) : IAlertCenterApiKeyProvider
    {
        public ValueTask<string> GetApiKeyAsync() => ValueTask.FromResult(key);
    }
}
