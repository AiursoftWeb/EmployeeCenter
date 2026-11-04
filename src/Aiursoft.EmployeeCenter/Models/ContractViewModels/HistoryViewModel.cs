using System.Text.Json;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.EmployeeCenter.Models.ContractViewModels;

public class HistoryViewModel : UiStackLayoutViewModel
{
    public HistoryViewModel() { PageTitle = "Contract History"; }
    public List<ContractChangeLog> History { get; set; } = [];
    public string? ContractKey { get; set; }
    public int CurrentPage { get; set; }
    public bool HasNextPage { get; set; }

    public static IEnumerable<(string Field, string Before, string After)> Changes(ContractChangeLog log)
    {
        var before = Read(log.BeforeJson);
        var after = Read(log.AfterJson);
        foreach (var field in before.Keys.Union(after.Keys))
        {
            var oldValue = before.GetValueOrDefault(field, "—");
            var newValue = after.GetValueOrDefault(field, "—");
            if (oldValue != newValue) yield return (field, oldValue, newValue);
        }
    }

    private static Dictionary<string, string> Read(string? value) => value == null ? [] :
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(value)!
            .ToDictionary(p => p.Key, p => p.Value.ValueKind == JsonValueKind.String
                ? p.Value.GetString() ?? "—" : p.Value.ToString());
}
