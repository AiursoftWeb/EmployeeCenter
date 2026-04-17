using Aiursoft.EmployeeCenter.Configuration;
using Aiursoft.EmployeeCenter.Services;

namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class AiAssistantTests : TestBase
{
    [TestMethod]
    public async Task GetIndex_Anonymous_RedirectsToLogin()
    {
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
        Assert.IsTrue(content.Contains("Chat with EmployeeCenter AI"));
    }

    [TestMethod]
    public async Task Ask_RateLimited_ReturnsTooManyRequests()
    {
        await LoginAsAdmin();
        var request = new { Question = "Hello" };

        for (int i = 1; i <= 5; i++)
        {
            await Http.PostAsJsonAsync("/AiAssistant/Ask", request);
        }

        // 6th request should be rate limited
        var lastResponse = await Http.PostAsJsonAsync("/AiAssistant/Ask", request);
        var lastContent = await lastResponse.Content.ReadAsStringAsync();
        Assert.AreEqual(HttpStatusCode.BadRequest, lastResponse.StatusCode);
        Assert.IsTrue(lastContent.Contains("Too many requests. Please try again in a minute."));
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
