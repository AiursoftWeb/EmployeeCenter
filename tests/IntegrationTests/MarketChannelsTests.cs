namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class MarketChannelsTests : TestBase
{
    private async Task<string> GetUserIdByEmail(string email)
    {
        using var scope = Server!.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.FindByEmailAsync(email);
        return user?.Id ?? throw new Exception($"User with email {email} not found");
    }

    [TestMethod]
    public async Task AdminCanAccessManagePage()
    {
        await LoginAsAdmin();
        var response = await Http.GetAsync("/MarketChannels/Manage");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task AdminCanCreateChannel()
    {
        await LoginAsAdmin();

        var name = "Test Channel " + Guid.NewGuid();
        var adminId = await GetUserIdByEmail("admin@default.com");

        var response = await PostForm("/MarketChannels/Create", new Dictionary<string, string>
        {
            { "Name", name },
            { "ManagerId", adminId },
            { "TargetAudience", "Test Audience" },
            { "Description", "Test Description" }
        });

        AssertRedirect(response, "/MarketChannels/Manage");

        // Verify it's created
        var managePage = await Http.GetStringAsync("/MarketChannels/Manage");
        Assert.IsTrue(managePage.Contains(name));

        // Verify details page
        var indexPage = await Http.GetStringAsync("/MarketChannels/Index");
        Assert.IsTrue(indexPage.Contains(name));
    }

    [TestMethod]
    public async Task UserCanAccessIndexButNotManage()
    {
        await RegisterAndLoginAsync();

        // Can access Index
        var indexResponse = await Http.GetAsync("/MarketChannels/Index");
        Assert.AreEqual(HttpStatusCode.OK, indexResponse.StatusCode);

        // Cannot access Manage (should redirect to Code403)
        var manageResponse = await Http.GetAsync("/MarketChannels/Manage");
        Assert.AreEqual(HttpStatusCode.Found, manageResponse.StatusCode);
        var location = manageResponse.Headers.Location?.ToString();
        Assert.IsTrue(location?.Contains("Code403") == true, $"Expected Code403, but got {location}");

        // Cannot access Create
        var createResponse = await Http.GetAsync("/MarketChannels/Create");
        Assert.AreEqual(HttpStatusCode.Found, createResponse.StatusCode);
        location = createResponse.Headers.Location?.ToString();
        Assert.IsTrue(location?.Contains("Code403") == true, $"Expected Code403, but got {location}");
    }
}
