namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class FeedbackTests
{
    private readonly int _port;
    private readonly HttpClient _http;
    private IHost? _server;

    public FeedbackTests()
    {
        var cookieContainer = new CookieContainer();
        var handler = new HttpClientHandler
        {
            CookieContainer = cookieContainer,
            AllowAutoRedirect = false
        };
        _port = Network.GetAvailablePort();
        _http = new HttpClient(handler)
        {
            BaseAddress = new Uri($"http://localhost:{_port}")
        };
    }

    [TestInitialize]
    public async Task CreateServer()
    {
        _server = await AppAsync<Startup>([], port: _port);
        await _server.UpdateDbAsync<EmployeeCenterDbContext>();
        await _server.SeedAsync();
        await _server.StartAsync();
    }

    [TestCleanup]
    public async Task CleanServer()
    {
        if (_server == null) return;
        await _server.StopAsync();
        _server.Dispose();
    }

    private async Task<string> GetAntiCsrfToken(string url)
    {
        var response = await _http.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(html, @"__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""");
        if (!match.Success)
        {
            throw new InvalidOperationException($"Could not find anti-CSRF token on page: {url}");
        }

        return match.Groups[1].Value;
    }

    [TestMethod]
    public async Task FeedbackWorkflowTest()
    {
        // 1. Login as admin
        var loginToken = await GetAntiCsrfToken("/Account/Login");
        var loginContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "EmailOrUserName", "admin" },
            { "Password", "Admin@123456!" },
            { "__RequestVerificationToken", loginToken }
        });
        var loginResponse = await _http.PostAsync("/Account/Login", loginContent);
        Assert.AreEqual(HttpStatusCode.Found, loginResponse.StatusCode);

        // 2. Create a question
        var createQuestionToken = await GetAntiCsrfToken("/ManageFeedback/CreateQuestion");
        var questionContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Title", "Do you like our company?" },
            { "Type", SignalQuestionType.MultipleChoice.ToString() },
            { "Tags", "Work Environment" },
            { "Meta", "{\"Options\":[\"Yes\", \"No\"]}" },
            { "__RequestVerificationToken", createQuestionToken }
        });
        var createQuestionResponse = await _http.PostAsync("/ManageFeedback/CreateQuestion", questionContent);
        Assert.AreEqual(HttpStatusCode.Found, createQuestionResponse.StatusCode);

        // 3. Verify question
        var questionsResponse = await _http.GetAsync("/ManageFeedback/Questions");
        questionsResponse.EnsureSuccessStatusCode();
        var questionsHtml = await questionsResponse.Content.ReadAsStringAsync();
        Assert.Contains("Do you like our company?", questionsHtml);

        // 4. Create a questionnaire
        int questionId;
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            questionId = (await db.SignalQuestions.FirstAsync()).Id;
        }

        var createQuestionnaireToken = await GetAntiCsrfToken("/ManageFeedback/CreateQuestionnaire");
        var questionnaireContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Title", "Annual Survey 2026" },
            { "Description", "Tell us what you think." },
            { "IsActive", "true" },
            { "SelectedQuestionIds", questionId.ToString() },
            { "__RequestVerificationToken", createQuestionnaireToken }
        });
        var createQuestionnaireResponse = await _http.PostAsync("/ManageFeedback/CreateQuestionnaire", questionnaireContent);
        Assert.AreEqual(HttpStatusCode.Found, createQuestionnaireResponse.StatusCode);

        // 5. Verify questionnaire
        var manageResponse = await _http.GetAsync("/ManageFeedback/Index");
        manageResponse.EnsureSuccessStatusCode();
        var manageHtml = await manageResponse.Content.ReadAsStringAsync();
        Assert.Contains("Annual Survey 2026", manageHtml);

        // 6. Give normal user the permission to submit feedback
        // In this test environment, we'll just log in as admin again to submit, 
        // because admin has CanSubmitFeedback as well (by default role).

        // 7. View available questionnaires
        var feedbackIndexResponse = await _http.GetAsync("/Feedback/Index");
        feedbackIndexResponse.EnsureSuccessStatusCode();
        var feedbackIndexHtml = await feedbackIndexResponse.Content.ReadAsStringAsync();
        Assert.Contains("Annual Survey 2026", feedbackIndexHtml);

        // 8. Fill questionnaire
        int questionnaireId;
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            questionnaireId = (await db.SignalQuestionnaires.FirstAsync()).Id;
        }

        var fillToken = await GetAntiCsrfToken($"/Feedback/Fill/{questionnaireId}");
        var fillContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "QuestionnaireId", questionnaireId.ToString() },
            { $"Answers[{questionId}]", "Yes" },
            { "__RequestVerificationToken", fillToken }
        });
        var fillResponse = await _http.PostAsync("/Feedback/Fill", fillContent);
        Assert.AreEqual(HttpStatusCode.Found, fillResponse.StatusCode);

        // 9. Verify submitted
        feedbackIndexResponse = await _http.GetAsync("/Feedback/Index");
        feedbackIndexResponse.EnsureSuccessStatusCode();
        feedbackIndexHtml = await feedbackIndexResponse.Content.ReadAsStringAsync();
        Assert.Contains("All caught up!", feedbackIndexHtml);

        // 10. Admin view responses
        var responsesResponse = await _http.GetAsync($"/ManageFeedback/Responses/{questionnaireId}");
        responsesResponse.EnsureSuccessStatusCode();
        var responsesHtml = await responsesResponse.Content.ReadAsStringAsync();
        Assert.Contains("Super Administrator", responsesHtml);

        // 11. View response detail
        int responseId;
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            responseId = (await db.SignalResponses.FirstAsync()).Id;
        }
        var detailResponse = await _http.GetAsync($"/ManageFeedback/ResponseDetail/{responseId}");
        detailResponse.EnsureSuccessStatusCode();
        var detailHtml = await detailResponse.Content.ReadAsStringAsync();
        Assert.Contains("Do you like our company?", detailHtml);
        Assert.Contains("Yes", detailHtml);
    }
}
