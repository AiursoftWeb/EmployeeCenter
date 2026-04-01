using System.Collections;
using System.Text;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.Scanner.Abstractions;
using Newtonsoft.Json;

namespace Aiursoft.EmployeeCenter.Services.Export;

public class MarkdownExporter : ITransientDependency
{
    public string ExportToMarkdown<T>(T entity) where T : class
    {
        var sb = new StringBuilder();
        sb.AppendLine("---");
        
        // Simple YAML front matter generation using reflection or Json.NET
        var json = JsonConvert.SerializeObject(entity, new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            NullValueHandling = NullValueHandling.Include,
            Formatting = Formatting.Indented
        });
        
        // This is a bit of a hack to turn JSON into something that looks like YAML
        // For a real system, a YAML library would be better.
        var lines = json.Split(Environment.NewLine);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed == "{" || trimmed == "}" || trimmed == "[" || trimmed == "]") continue;
            
            // Remove quotes from keys
            if (trimmed.StartsWith("\"") && trimmed.Contains("\":"))
            {
                var colonIndex = trimmed.IndexOf("\":");
                var key = trimmed.Substring(1, colonIndex - 1);
                var value = trimmed.Substring(colonIndex + 2).Trim();
                
                // Remove trailing comma
                if (value.EndsWith(",")) value = value.Substring(0, value.Length - 1);
                
                // If value is a string with quotes, keep them or remove them?
                // For YAML, let's keep them if they are there.
                
                var indentLevel = line.Length - line.TrimStart().Length;
                sb.Append(new string(' ', indentLevel));
                sb.AppendLine($"{key}: {value}");
            }
            else
            {
                // Handle arrays or other values
                sb.AppendLine(line);
            }
        }
        
        sb.AppendLine("---");
        sb.AppendLine();

        // Add specific content based on type
        if (entity is WeeklyReport report)
        {
            sb.AppendLine($"# Weekly Report - {report.WeekStartDate:yyyy-MM-dd}");
            sb.AppendLine();
            sb.AppendLine(report.Content);
        }
        else if (entity is Asset asset)
        {
            sb.AppendLine($"# Asset: {asset.AssetTag}");
            sb.AppendLine();
            sb.AppendLine($"- **Model**: {asset.Model?.Brand} {asset.Model?.ModelName}");
            sb.AppendLine($"- **Category**: {asset.Model?.Category?.Name}");
            sb.AppendLine($"- **Status**: {asset.Status}");
            sb.AppendLine($"- **Assignee**: {asset.Assignee?.DisplayName}");
        }
        else if (entity is Requirement req)
        {
            sb.AppendLine($"# {req.Title}");
            sb.AppendLine();
            sb.AppendLine(req.Content);
        }
        else if (entity is Server server)
        {
            sb.AppendLine($"# Server: {server.Hostname}");
            sb.AppendLine();
            sb.AppendLine($"- **IP**: {server.ServerIp}");
            sb.AppendLine($"- **Provider**: {server.ProviderId}");
        }
        else if (entity is Service service)
        {
            sb.AppendLine($"# Service: {service.Domain}");
            sb.AppendLine();
            sb.AppendLine($"- **Protocols**: {service.Protocols}");
            sb.AppendLine($"- **Status**: {service.Status}");
        }
        else
        {
            sb.AppendLine($"# {entity.GetType().Name}");
        }

        return sb.ToString();
    }

    public string GetRelativePath<T>(T entity) where T : class
    {
        if (entity is WeeklyReport report)
        {
            return Path.Combine("WeeklyReports", report.WeekStartDate.Year.ToString(), report.WeekStartDate.Month.ToString("D2"), $"{report.WeekStartDate:yyyy-MM-dd}_{report.Id}.md");
        }
        if (entity is Asset asset)
        {
            return Path.Combine("Assets", asset.Model?.Category?.Name ?? "Uncategorized", $"{asset.AssetTag}_{asset.Id}.md");
        }
        if (entity is LeaveApplication leave)
        {
            return Path.Combine("LeaveApplications", leave.StartDate.Year.ToString(), $"{leave.StartDate:yyyy-MM-dd}_{leave.Id}.md");
        }
        if (entity is Requirement req)
        {
            return Path.Combine("Requirements", $"{req.Title.Replace(" ", "_")}_{req.Id}.md");
        }
        if (entity is User user)
        {
            return Path.Combine("Users", $"{user.DisplayName.Replace(" ", "_")}_{user.Id}.md");
        }
        if (entity is Password pass)
        {
            return Path.Combine("Passwords", $"{pass.Title.Replace(" ", "_")}_{pass.Id}.md");
        }
        if (entity is Blueprint bp)
        {
            return Path.Combine("Blueprints", $"{bp.Title.Replace(" ", "_")}_{bp.Id}.md");
        }
        if (entity is Server server)
        {
            return Path.Combine("Servers", $"{(server.Hostname ?? server.ServerIp ?? server.Id.ToString()).Replace(" ", "_")}_{server.Id}.md");
        }
        if (entity is Service service)
        {
            return Path.Combine("Services", $"{service.Domain.Replace(" ", "_")}_{service.Id}.md");
        }
        if (entity is Payroll payroll)
        {
            return Path.Combine("Payrolls", payroll.Owner?.DisplayName?.Replace(" ", "_") ?? "Unknown", $"{payroll.Id}.md");
        }
        if (entity is Contract contract)
        {
            return Path.Combine("Contracts", $"{contract.Id}.md");
        }
        if (entity is CompanyEntity company)
        {
            return Path.Combine("CompanyEntities", $"{company.CompanyName.Replace(" ", "_")}_{company.Id}.md");
        }
        if (entity is FinanceAccount account)
        {
            return Path.Combine("Finance", "Accounts", $"{account.AccountName.Replace(" ", "_")}_{account.Id}.md");
        }
        if (entity is Transaction trans)
        {
            return Path.Combine("Finance", "Transactions", $"{trans.Id}.md");
        }
        if (entity is Incident incident)
        {
            return Path.Combine("Incidents", $"{incident.Id}.md");
        }
        if (entity is OnboardingTask task)
        {
            return Path.Combine("Onboarding", $"{task.Id}.md");
        }
        if (entity is IntangibleAsset iAsset)
        {
            return Path.Combine("IntangibleAssets", $"{iAsset.Id}.md");
        }
        if (entity is CustomerRelationship rel)
        {
            return Path.Combine("CustomerRelationships", $"{rel.Id}.md");
        }
        if (entity is MarketChannel channel)
        {
            return Path.Combine("MarketChannels", $"{channel.Id}.md");
        }

        return Path.Combine(entity.GetType().Name, $"{Guid.NewGuid()}.md");
    }
}
