using System.Text.Json;
using Aiursoft.AlertCenter.SDK.Models;
using Aiursoft.EmployeeCenter.Configuration;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Services.DnsAudit;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;

namespace Aiursoft.EmployeeCenter.Services.BackgroundJobs;

public sealed class ExportJobIncidentService(
    EmployeeCenterDbContext db, IAuditIncidentClient client,
    IAlertCenterSettingsProvider settingsProvider, ILogger<ExportJobIncidentService> logger)
{
    private const string StateKey = "Internal.ExportJob.AlertState";
    private const string Source = "EmployeeCenter.ExportJob";
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task RecordAsync(bool succeeded, string? exceptionType = null)
    {
        await Gate.WaitAsync();
        try
        {
            var row = await db.GlobalSettings.SingleOrDefaultAsync(item => item.Key == StateKey);
            var state = row?.Value == null ? new State() :
                JsonSerializer.Deserialize<State>(row.Value) ?? throw new InvalidOperationException("Invalid export job alert state.");
            if (succeeded)
            {
                state.Failures = 0;
                state.ExceptionType = null;
            }
            else
            {
                if (state.Failures == 0)
                {
                    state.ReportId = Guid.NewGuid();
                    state.FirstFailureUtc = DateTimeOffset.UtcNow;
                }
                if (state.Failures < int.MaxValue) state.Failures++;
                state.ExceptionType = exceptionType;
            }
            if (row == null)
            {
                row = new GlobalSetting { Key = StateKey };
                db.GlobalSettings.Add(row);
            }
            // Save the outcome and reporting receipt before making any remote request.
            row.Value = JsonSerializer.Serialize(state);
            await db.SaveChangesAsync();
            var settings = await settingsProvider.GetAsync();
            if (!settings.Enabled) return;
            settings.Validate();
            var incidents = await LoadAsync(settings.PublicBaseUrl);
            var active = incidents.Where(item => item.Status is not ("mitigated" or "resolved"))
                .OrderBy(item => item.ReceivedAtUtc).ThenBy(item => item.IncidentId).ToList();
            if (state.Failures == 0)
            {
                foreach (var incident in active)
                    await client.MitigateAsync(incident.IncidentId, new MitigateIncidentRequest
                    {
                        Version = incident.Version,
                        Reason = "The export job completed successfully. Consecutive failures have been reset to zero."
                    });
                return;
            }
            var level = state.Failures >= 4 ? "sev2" : "sev3";
            if (active.Count > 0)
            {
                var incident = active[0];
                if (string.CompareOrdinal(level, incident.Level) < 0)
                    await client.ChangeSeverityAsync(incident.IncidentId, new ChangeIncidentSeverityRequest
                    {
                        Version = incident.Version, Level = level,
                        Reason = "The export job has failed in four consecutive executions. The export is still incomplete."
                    });
                return;
            }
            // A closed incident is never reopened, while an uncertain create reuses its saved receipt.
            if (incidents.Any(item => item.ReportId == state.ReportId))
            {
                state.ReportId = Guid.NewGuid();
                row.Value = JsonSerializer.Serialize(state);
                await db.SaveChangesAsync();
            }
            await client.CreateAsync(new CreateIncidentRequest
            {
                ReportId = state.ReportId, Title = "Employee Center scheduled export failed",
                Level = level, OccurredAtUtc = state.FirstFailureUtc,
                Content = $"The scheduled export did not finish. Consecutive failed executions: {state.Failures}. " +
                    $"Exception type: {state.ExceptionType}. The exported context may be incomplete or stale; this does not prove that original records or attachments were lost.\n\n" +
                    "Check the Export Job result and application logs, correct the export failure, then run the job again. " +
                    "The first failure raises Sev3; the fourth consecutive failure escalates the same incident to Sev2. " +
                    "A successful export resets the counter and automatically mitigates the incident. " +
                    $"Job status: {settings.PublicBaseUrl.TrimEnd('/')}/Jobs",
                Context = new JObject { ["source"] = Source, ["instance"] = settings.PublicBaseUrl.TrimEnd('/') }
            });
        }
        catch (Exception ex)
        {
            // Alert delivery must not replace an export exception or mark a successful export as failed.
            logger.LogError(ex, "Could not persist or synchronize the export job alert; the next job execution will retry synchronization");
        }
        finally
        {
            Gate.Release();
        }
    }

    private async Task<List<IncidentDetails>> LoadAsync(string instance)
    {
        var incidents = new List<IncidentDetails>();
        int? total = null;
        for (var page = 1; ; page++)
        {
            var result = await client.QueryAsync(new QueryIncidentsRequest { Page = page, PageSize = 100, MetadataKey = "source", MetadataValue = Source });
            total ??= result.TotalCount;
            if (total != result.TotalCount || result.Page != page || result.PageSize != 100)
                throw new InvalidOperationException("The incident listing changed during export alert synchronization.");
            incidents.AddRange(result.Items);
            if (incidents.Count >= total)
            {
                if (incidents.Count != total || incidents.Select(item => item.IncidentId).Distinct().Count() != incidents.Count)
                    throw new InvalidOperationException("The incident listing is inconsistent.");
                return incidents.Where(item => item.Context?.Value<string>("source") == Source &&
                    item.Context.Value<string>("instance") == instance.TrimEnd('/')).ToList();
            }
            if (result.Items.Count == 0) throw new InvalidOperationException("The incident listing ended early.");
        }
    }

    public sealed class State
    {
        public int Failures { get; set; }
        public Guid ReportId { get; set; }
        public DateTimeOffset FirstFailureUtc { get; set; }
        public string? ExceptionType { get; set; }
    }
}
