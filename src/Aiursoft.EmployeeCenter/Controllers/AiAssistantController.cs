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
    public async Task<IActionResult> Ask([FromBody] AskRequest request)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var cacheKey = $"ai-assistant-rate-limit-{ip}";
        if (!cache.TryGetValue(cacheKey, out int count))
        {
            count = 0;
        }
        
        if (count >= 5)
        {
            return BadRequest(new { error = "Too many requests. Please try again in a minute." });
        }
        cache.Set(cacheKey, count + 1, TimeSpan.FromMinutes(1));

        var systemPrompt = await globalSettingsService.GetSettingValueAsync(SettingsMap.AiAssistantSystemPrompt);
        var currentCulture = CultureInfo.CurrentUICulture.NativeName;
        systemPrompt = systemPrompt.Replace("{{LANG}}", currentCulture);

        try
        {
            var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromMinutes(5);
            var response = await client.PostAsJsonAsync(appSettings.Value.Agent.Endpoint, new
            {
                system_prompt = systemPrompt,
                question = request.Question
            });

            if (!response.IsSuccessStatusCode)
            {
                return BadRequest(new { error = "Agent is not responding." });
            }

            var result = await response.Content.ReadFromJsonAsync<AgentResponse>();
            return Json(new { answer = result?.Answer ?? "No answer received." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Agent failed to respond: {ex.Message}" });
        }
    }
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
