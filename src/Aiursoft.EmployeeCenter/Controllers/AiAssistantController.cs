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
using System.Text.Json.Serialization;
using Markdig;
using Ganss.Xss;

namespace Aiursoft.EmployeeCenter.Controllers;

[Authorize(Policy = AppPermissionNames.CanChatWithAi)]
public class AiAssistantController(
    IOptions<AppSettings> appSettings,
    IHttpClientFactory httpClientFactory,
    GlobalSettingsService globalSettingsService,
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
                var systemPrompt = await globalSettingsService.GetSettingValueAsync(SettingsMap.AiAssistantSystemPrompt);
                var currentCulture = CultureInfo.CurrentUICulture.NativeName;
                systemPrompt = systemPrompt.Replace("{{LANG}}", currentCulture);

                var client = httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromMinutes(10);
                var response = await client.PostAsJsonAsync(appSettings.Value.Agent.Endpoint, new
                {
                    system_prompt = systemPrompt,
                    question = request.Question
                });

                if (!response.IsSuccessStatusCode)
                {
                    status.Status = "Error";
                    status.ErrorMessage = "Agent is not responding.";
                    cache.Set($"ai-task-{taskId}", status, TimeSpan.FromMinutes(30));
                    return;
                }

                var result = await response.Content.ReadFromJsonAsync<AgentResponse>();
                var rawMarkdown = result?.Answer ?? "No answer received.";

                var pipeline = new MarkdownPipelineBuilder()
                    .UseAdvancedExtensions()
                    .Build();
                var htmlResult = Markdown.ToHtml(rawMarkdown, pipeline);

                var sanitizer = new HtmlSanitizer();
                status.Answer = sanitizer.Sanitize(htmlResult);
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
    public required string TaskId { get; set; }
    public required string Status { get; set; } // Processing, Completed, Error
    public string? Answer { get; set; }
    public string? ErrorMessage { get; set; }
}

public class AskRequest
{
    public required string Question { get; set; }
}

public class AgentResponse
{
    [JsonPropertyName("answer")]
    public string? Answer { get; set; }
}
