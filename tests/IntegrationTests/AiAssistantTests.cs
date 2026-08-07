using System.Text.Json;
using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Configuration;
using Aiursoft.EmployeeCenter.Services;
using Microsoft.Extensions.Caching.Memory;

namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class AiAssistantTests : TestBase
{
    [TestMethod]
    public async Task GetIndex_Anonymous_RedirectsToLogin()
    {
        await Http.GetAsync("/Account/LogOff");
        var response = await Http.GetAsync("/AiAssistant/Index");
        Assert.AreEqual(HttpStatusCode.Found, response.StatusCode);
        Assert.IsTrue(response.Headers.Location?.OriginalString.Contains("/Account/Login") ?? false);
    }

    [TestMethod]
    public async Task GetIndex_Authenticated_ReturnsSuccess()
    {
        await LoginAsAdmin();
        var response = await Http.GetAsync("/AiAssistant/Index");
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.IsTrue(content.Contains("AI Assistant"));
        StringAssert.Contains(content, "mathjax/es5/tex-mml-chtml.js");
        StringAssert.Contains(content, "mermaid/dist/mermaid.min.js");
        StringAssert.Contains(content, "highlight.min.js");
        StringAssert.Contains(content, "initializeMarkdownReader");
        StringAssert.Contains(content, "copyButton.textContent = 'Copy'");
        StringAssert.Contains(content, "copyButton.textContent = 'Copied'");
        Assert.IsFalse(content.Contains("data-lucide=\"copy\""));
        Assert.IsFalse(content.Contains("data-lucide=\"check\""));
        Assert.IsFalse(content.Contains("function renderMarkdown"));
    }

    [TestMethod]
    public async Task AiAssistantPermission_Works()
    {
        // 1. Create a user without permission
        await Http.GetAsync("/Account/LogOff");
        var (email, password) = await RegisterAndLoginAsync();

        // 2. Regular user -> /AiAssistant/Index -> Forbidden/Redirect to Unauthorized
        var response = await Http.GetAsync("/AiAssistant/Index");
        Assert.IsTrue(response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.Found);
        if (response.StatusCode == HttpStatusCode.Found)
        {
            var location = response.Headers.Location?.OriginalString ?? string.Empty;
            Assert.IsTrue(location.Contains("/Error/Unauthorized") || location.Contains("AccessDenied") || location.Contains("/Error/Code403"), 
                $"Redirected to {location} instead of Unauthorized");
        }

        // 3. Grant CanChatWithAi
        using (var scope = Server!.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            var roleName = "AiUser";
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var role = new IdentityRole(roleName);
                await roleManager.CreateAsync(role);
                await roleManager.AddClaimAsync(role, new Claim(AppPermissions.Type, AppPermissionNames.CanChatWithAi));
            }

            var user = await userManager.FindByEmailAsync(email);
            await userManager.AddToRoleAsync(user!, roleName);
        }

        // 4. Re-login to refresh claims
        await Http.GetAsync("/Account/LogOff");
        await LoginAsAsync(email, password);

        // 5. User with permission -> /AiAssistant/Index -> OK
        response = await Http.GetAsync("/AiAssistant/Index");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task Ask_RateLimited_ReturnsTooManyRequests()
    {
        await LoginAsAdmin();
        var request = new { Question = "Hello" };

        for (int i = 1; i <= 5; i++)
        {
            var message = new HttpRequestMessage(HttpMethod.Post, "/AiAssistant/Ask")
            {
                Content = JsonContent.Create(request)
            };
            message.Headers.Add("X-Test-Rate-Limit", "true");
            var response = await Http.SendAsync(message);
            response.EnsureSuccessStatusCode();
            
            var data = await response.Content.ReadFromJsonAsync<JsonDocument>();
            Assert.IsTrue(data?.RootElement.TryGetProperty("taskId", out _));
        }

        // 6th request should be rate limited
        var lastResponse = await Http.PostAsJsonAsync("/AiAssistant/Ask", request);
        var lastContent = await lastResponse.Content.ReadAsStringAsync();
        
        Assert.AreEqual(HttpStatusCode.BadRequest, lastResponse.StatusCode);
        Assert.IsTrue(lastContent.Contains("Too many requests. Please try again in a minute."));
    }

    [TestMethod]
    public async Task Ask_ReturnsTaskId_AndStatusCompletedOrError()
    {
        await LoginAsAdmin();
        var request = new { Question = "Hello", History = new List<object>() };

        var startResponse = await Http.PostAsJsonAsync("/AiAssistant/Ask", request);
        startResponse.EnsureSuccessStatusCode();

        var startData = await startResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var taskId = startData?.RootElement.GetProperty("taskId").GetString();
        Assert.IsFalse(string.IsNullOrEmpty(taskId));

        // Poll for completion
        bool finished = false;
        for (int i = 0; i < 20; i++) // Try for 20 seconds
        {
            var statusResponse = await Http.GetAsync($"/AiAssistant/CheckStatus?taskId={taskId}");
            statusResponse.EnsureSuccessStatusCode();
            var statusData = await statusResponse.Content.ReadFromJsonAsync<JsonDocument>();
            
            string? status = null;
            if (statusData != null && statusData.RootElement.TryGetProperty("status", out var statusProp))
            {
                status = statusProp.GetString();
            }
            else if (statusData != null && statusData.RootElement.TryGetProperty("Status", out var statusPropPascal))
            {
                status = statusPropPascal.GetString();
            }

            if (status == "Completed")
            {
                finished = true;
                Assert.IsTrue(statusData!.RootElement.TryGetProperty("answer", out _) || statusData.RootElement.TryGetProperty("Answer", out _));
                break;
            }
            else if (status == "Error")
            {
                finished = true;
                Assert.IsTrue(statusData!.RootElement.TryGetProperty("errorMessage", out _) || statusData.RootElement.TryGetProperty("ErrorMessage", out _));
                break;
            }

            await Task.Delay(1000);
        }

        Assert.IsTrue(finished, "Task did not finish (Completed or Error) within timeout.");
    }

    [TestMethod]
    public async Task Ask_BackgroundTask_ShouldNotFailDueToDisposedContext()
    {
        await LoginAsAdmin();
        
        // Clear cache to ensure GlobalSettingsService hits the database
        var cache = GetService<IMemoryCache>();
        var definitions = SettingsMap.Definitions;
        foreach (var def in definitions)
        {
            cache.Remove($"global-setting-{def.Key}");
        }

        var request = new { Question = "Hello", History = new List<object>() };

        var startResponse = await Http.PostAsJsonAsync("/AiAssistant/Ask", request);
        startResponse.EnsureSuccessStatusCode();

        var startData = await startResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var taskId = startData?.RootElement.GetProperty("taskId").GetString();
        Assert.IsNotNull(taskId);

        // Poll for result
        string? status = null;
        string? errorMessage = null;
        for (int i = 0; i < 10; i++)
        {
            var statusResponse = await Http.GetAsync($"/AiAssistant/CheckStatus?taskId={taskId}");
            statusResponse.EnsureSuccessStatusCode();
            var statusData = await statusResponse.Content.ReadFromJsonAsync<JsonDocument>();
            
            if (statusData != null)
            {
                var root = statusData.RootElement;
                if (root.TryGetProperty("status", out var s) || root.TryGetProperty("Status", out s))
                    status = s.GetString();
                
                if (root.TryGetProperty("errorMessage", out var e) || root.TryGetProperty("ErrorMessage", out e))
                    errorMessage = e.GetString();
            }

            if (status == "Completed" || status == "Error") break;
            await Task.Delay(500);
        }

        if (status == "Error")
        {
            Assert.IsFalse(errorMessage?.Contains("Cannot access a disposed context instance") ?? false, 
                $"Task failed due to disposed context: {errorMessage}");
        }
    }

    [TestMethod]
    public async Task Ask_CheckStatus_ReturnsCamelCaseJson()
    {
        await LoginAsAdmin();
        var request = new { Question = "Hello", History = new List<object>() };

        // Test Ask response
        var askResponse = await Http.PostAsJsonAsync("/AiAssistant/Ask", request);
        askResponse.EnsureSuccessStatusCode();
        var askContent = await askResponse.Content.ReadAsStringAsync();
        
        // Ensure "taskId" exists and "TaskId" does not (in raw JSON)
        Assert.IsTrue(askContent.Contains("\"taskId\":"), $"Ask response should contain camelCase 'taskId': {askContent}");
        Assert.IsFalse(askContent.Contains("\"TaskId\":"), $"Ask response should NOT contain PascalCase 'TaskId': {askContent}");

        var askData = await askResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var taskId = askData?.RootElement.GetProperty("taskId").GetString();
        Assert.IsNotNull(taskId);

        // Test CheckStatus response
        var statusResponse = await Http.GetAsync($"/AiAssistant/CheckStatus?taskId={taskId}");
        statusResponse.EnsureSuccessStatusCode();
        var statusContent = await statusResponse.Content.ReadAsStringAsync();

        // Ensure camelCase properties exist and PascalCase do not
        Assert.IsTrue(statusContent.Contains("\"taskId\":"), $"CheckStatus response should contain camelCase 'taskId': {statusContent}");
        Assert.IsTrue(statusContent.Contains("\"status\":"), $"CheckStatus response should contain camelCase 'status': {statusContent}");
        Assert.IsFalse(statusContent.Contains("\"TaskId\":"), $"CheckStatus response should NOT contain PascalCase 'TaskId': {statusContent}");
        Assert.IsFalse(statusContent.Contains("\"Status\":"), $"CheckStatus response should NOT contain PascalCase 'Status': {statusContent}");
    }

    [TestMethod]
    public async Task AiAssistantSystemPrompt_IsSeeded()
    {
        var settingsService = GetService<GlobalSettingsService>();
        var prompt = await settingsService.GetSettingValueAsync(SettingsMap.AiAssistantSystemPrompt);
        Assert.IsFalse(string.IsNullOrWhiteSpace(prompt));
        Assert.IsTrue(prompt.Contains("专业AI助手"));
    }
}
