using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Configuration;
using Aiursoft.EmployeeCenter.Models.AiAssistantViewModels;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.UiStack.Navigation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Memory;
using System.Globalization;
using System.Text;
using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace Aiursoft.EmployeeCenter.Controllers;

[Authorize(Policy = AppPermissionNames.CanChatWithAi)]
public class AiAssistantController(
    IOptions<AppSettings> appSettings,
    IHttpClientFactory httpClientFactory,
    IServiceScopeFactory scopeFactory,
    IMemoryCache cache) : Controller
{
    [RenderInNavBar(
        NavGroupName = "AI",
        NavGroupOrder = 0,
        CascadedLinksGroupName = "AI Assistant",
        CascadedLinksIcon = "sparkles",
        CascadedLinksOrder = 1,
        LinkText = "Company Info Consultation",
        LinkOrder = 1)]
    public IActionResult Index()
    {
        return this.StackView(new IndexViewModel());
    }

    [HttpPost]
    public IActionResult Ask([FromBody] AskRequest request)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var rateLimitCacheKey = $"ai-assistant-rate-limit-{ip}";
        if (!cache.TryGetValue(rateLimitCacheKey, out int count))
        {
            count = 0;
        }

        if (count >= 5)
        {
            return BadRequest(new { error = "Too many requests. Please try again in a minute." });
        }
        cache.Set(rateLimitCacheKey, count + 1, TimeSpan.FromMinutes(1));

        var taskId = Guid.NewGuid().ToString();
        var status = new TaskStatus { Status = "Processing", TaskId = taskId };
        cache.Set($"ai-task-{taskId}", status, TimeSpan.FromMinutes(30));

        // Start background task
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var scopedGlobalSettingsService = scope.ServiceProvider.GetRequiredService<GlobalSettingsService>();

                var systemPrompt = await scopedGlobalSettingsService.GetSettingValueAsync(SettingsMap.AiAssistantSystemPrompt);
                var currentCulture = CultureInfo.CurrentUICulture.NativeName;
                systemPrompt = systemPrompt.Replace("{{LANG}}", currentCulture);

                // Construct full question with history
                var fullQuestionBuilder = new StringBuilder();
                if (request.History.Any())
                {
                    fullQuestionBuilder.AppendLine("Previous conversation history:");
                    foreach (var msg in request.History)
                    {
                        fullQuestionBuilder.AppendLine($"{(msg.Role == "user" ? "User" : "Assistant")}: {msg.Content}");
                    }
                    fullQuestionBuilder.AppendLine("\nCurrent Question:");
                }
                fullQuestionBuilder.Append(request.Question);

                var client = httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromMinutes(10);
                var response = await client.PostAsJsonAsync(appSettings.Value.Agent.Endpoint, new
                {
                    system_prompt = systemPrompt,
                    question = fullQuestionBuilder.ToString()
                });

                if (!response.IsSuccessStatusCode)
                {
                    status.Status = "Error";
                    status.ErrorMessage = "Agent is not responding.";
                    cache.Set($"ai-task-{taskId}", status, TimeSpan.FromMinutes(30));
                    return;
                }

                var result = await response.Content.ReadFromJsonAsync<AgentResponse>();
                status.Answer = result?.Answer ?? "No answer received.";
                status.Status = "Completed";
                cache.Set($"ai-task-{taskId}", status, TimeSpan.FromMinutes(30));
            }
            catch (Exception ex)
            {
                status.Status = "Error";
                status.ErrorMessage = $"Agent failed to respond: {ex.Message}";
                cache.Set($"ai-task-{taskId}", status, TimeSpan.FromMinutes(30));
            }
        });

        return Json(new { taskId });
    }

    [HttpGet]
    public IActionResult CheckStatus(string taskId)
    {
        if (cache.TryGetValue($"ai-task-{taskId}", out TaskStatus? status))
        {
            return Json(status);
        }
        return NotFound();
    }
}

public class TaskStatus
{
    [JsonProperty("taskId")]
    public required string TaskId { get; set; }
    
    [JsonProperty("status")]
    public required string Status { get; set; } // Processing, Completed, Error
    
    [JsonProperty("answer")]
    public string? Answer { get; set; }
    
    [JsonProperty("errorMessage")]
    public string? ErrorMessage { get; set; }
}

public class ChatMessage
{
    [JsonProperty("role")]
    public required string Role { get; set; } // "user" or "assistant"

    [JsonProperty("content")]
    public required string Content { get; set; }
}

public class AskRequest
{
    [JsonProperty("question")]
    public required string Question { get; set; }

    [JsonProperty("history")]
    public ChatMessage[] History { get; set; } = Array.Empty<ChatMessage>();
}

public class AgentResponse
{
    [JsonPropertyName("answer")]
    public string? Answer { get; set; }
}

