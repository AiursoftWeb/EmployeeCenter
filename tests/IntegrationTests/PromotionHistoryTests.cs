namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class PromotionHistoryTests : TestBase
{
    [TestMethod]
    public async Task TestPromotionHistoryPage()
    {
        // 1. Create a user
        string email, userId;
        var suffix = Guid.NewGuid().ToString("N")[..6];
        using (var scope = Server!.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = new User
            {
                UserName = "user" + suffix,
                DisplayName = "Promotion Test User",
                Email = "user" + suffix + "@test.com",
                AvatarRelativePath = User.DefaultAvatarPath
            };
            var res = await userManager.CreateAsync(user, "Password123!");
            if (!res.Succeeded)
            {
                throw new Exception("Failed to create user: " + string.Join(", ", res.Errors.Select(e => e.Description)));
            }
            email = user.Email;
            userId = user.Id;
        }

        // 2. Add some promotion history
        using (var scope = Server!.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            dbContext.PromotionHistories.Add(new PromotionHistory
            {
                UserId = userId,
                PromoterId = userId, // Self promote for testing
                OldJobLevel = "L1",
                NewJobLevel = "L2",
                OldTitle = "Junior",
                NewTitle = "Senior",
                ChangeTime = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        // 3. Login
        await PostForm("/Account/Login", new Dictionary<string, string>
        {
            { "EmailOrUserName", email },
            { "Password", "Password123!" }
        });

        // 4. Access Promotion History page
        var response = await Http.GetAsync("/PromotionHistory");
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();

        // 5. Verify content
        Assert.Contains("Promotion History", html);
        Assert.Contains("L1", html);
        Assert.Contains("L2", html);
        Assert.Contains("Junior", html);
        Assert.Contains("Senior", html);
    }

    [TestMethod]
    public async Task TestManualPromotionCreation()
    {
        // 1. Create a target user
        string userId;
        var suffix = Guid.NewGuid().ToString("N")[..6];
        using (var scope = Server!.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = new User
            {
                UserName = "target" + suffix,
                DisplayName = "Target User",
                Email = "target" + suffix + "@test.com",
                AvatarRelativePath = User.DefaultAvatarPath,
                JobLevel = "L1",
                Title = "Junior"
            };
            await userManager.CreateAsync(user, "Password123!");
            userId = user.Id;
        }

        // 2. Login as admin
        await LoginAsAdmin();

        // 3. Go to create promotion page
        var createPageResponse = await Http.GetAsync($"/PromotionHistory/Create?userId={userId}");
        createPageResponse.EnsureSuccessStatusCode();
        var createPageHtml = await createPageResponse.Content.ReadAsStringAsync();
        Assert.Contains("Target User", createPageHtml);
        Assert.Contains("L1", createPageHtml);
        Assert.Contains("Junior", createPageHtml);

        // 4. Submit promotion
        var postResponse = await PostForm("/PromotionHistory/Create", new Dictionary<string, string>
        {
            { "UserId", userId },
            { "NewJobLevel", "L2" },
            { "NewTitle", "Senior" }
        });
        AssertRedirect(postResponse, "/PromotionHistory");

        // 5. Verify user status updated
        using (var scope = Server!.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = await userManager.FindByIdAsync(userId);
            Assert.AreEqual("L2", user!.JobLevel);
            Assert.AreEqual("Senior", user.Title);
        }

        // 6. Verify history recorded
        var indexResponse = await Http.GetAsync("/PromotionHistory");
        indexResponse.EnsureSuccessStatusCode();
        var indexHtml = await indexResponse.Content.ReadAsStringAsync();
        Assert.Contains("Target User", indexHtml);
        Assert.Contains("L1", indexHtml);
        Assert.Contains("L2", indexHtml);
    }
}
