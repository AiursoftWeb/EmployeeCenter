namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class RequirementTests : TestBase
{
    [TestMethod]
    public async Task GetApprovedProjectsPageTest()
    {
        await LoginAsAdmin();
        var response = await Http.GetAsync("/Requirements/Index");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task GetMyRequirementsPageTest()
    {
        await LoginAsAdmin();
        var response = await Http.GetAsync("/Requirements/My");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task GetHistoryPageTest()
    {
        await LoginAsAdmin();
        var response = await Http.GetAsync("/Requirements/History");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task GetManageRequirementsPageAdminAccessTest()
    {
        await LoginAsAdmin();
        var response = await Http.GetAsync("/Requirements/Manage");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task GetManageRequirementsPageUserAccessTest()
    {
        await RegisterAndLoginAsync();
        var response = await Http.GetAsync("/Requirements/Manage");
        AssertRedirect(response, "/Error/Code403", exact: false);
    }

    [TestMethod]
    public async Task CreateRequirementTest()
    {
        await RegisterAndLoginAsync();
        var response = await Http.GetAsync("/Requirements/Create");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var postResponse = await PostForm("/Requirements/Create", new Dictionary<string, string>
        {
            { "Title", "Test Requirement" },
            { "InputMarkdown", "# Test\nContent" }
        });

        // Should redirect to View
        Assert.AreEqual(HttpStatusCode.Found, postResponse.StatusCode);

        using var scope = Server!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
        var requirement = await db.Requirements.FirstOrDefaultAsync(r => r.Title == "Test Requirement");
        Assert.IsNotNull(requirement);
        Assert.AreEqual(RequirementStatus.PendingApproval, requirement.Status);
    }

    [TestMethod]
    public async Task CommentRequirementTest()
    {
        await LoginAsAdmin();
        // Create a requirement first
        await PostForm("/Requirements/Create", new Dictionary<string, string>
        {
            { "Title", "Comment Test" },
            { "InputMarkdown", "Content" }
        });

        int requirementId;
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            requirementId = (await db.Requirements.FirstAsync(r => r.Title == "Comment Test")).Id;
        }

        var commentResponse = await PostForm($"/Requirements/Comment/{requirementId}", new Dictionary<string, string>
        {
            { "content", "Test Comment" }
        }, tokenUrl: $"/Requirements/View/{requirementId}");

        Assert.AreEqual(HttpStatusCode.Found, commentResponse.StatusCode);

        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var comment = await db.RequirementComments.FirstOrDefaultAsync(c => c.RequirementId == requirementId && c.Content == "Test Comment");
            Assert.IsNotNull(comment);
        }
    }

    [TestMethod]
    public async Task ApproveRequirementTest()
    {
        await LoginAsAdmin();
        // Create a requirement
        await PostForm("/Requirements/Create", new Dictionary<string, string>
        {
            { "Title", "Approve Test" },
            { "InputMarkdown", "Content" }
        });

        int requirementId;
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            requirementId = (await db.Requirements.FirstAsync(r => r.Title == "Approve Test")).Id;
        }

        var approveResponse = await PostForm($"/Requirements/Approve/{requirementId}", new Dictionary<string, string>(), tokenUrl: $"/Requirements/View/{requirementId}");

        Assert.AreEqual(HttpStatusCode.Found, approveResponse.StatusCode);

        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var requirement = await db.Requirements.FindAsync(requirementId);
            Assert.AreEqual(RequirementStatus.Approved, requirement?.Status);
        }
    }

    [TestMethod]
    public async Task EditRequirementAsCreatorTest()
    {
        await RegisterAndLoginAsync();
        // Create a requirement
        await PostForm("/Requirements/Create", new Dictionary<string, string>
        {
            { "Title", "Edit Test" },
            { "InputMarkdown", "Initial Content" }
        });

        int requirementId;
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            requirementId = (await db.Requirements.FirstAsync(r => r.Title == "Edit Test")).Id;
        }

        var editResponse = await PostForm($"/Requirements/Edit/{requirementId}", new Dictionary<string, string>
        {
            { "RequirementId", requirementId.ToString() },
            { "Title", "Edited Title" },
            { "InputMarkdown", "Edited Content" }
        }, tokenUrl: $"/Requirements/Edit/{requirementId}");

        Assert.AreEqual(HttpStatusCode.Found, editResponse.StatusCode);

        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var requirement = await db.Requirements.FindAsync(requirementId);
            Assert.AreEqual("Edited Title", requirement?.Title);
            Assert.AreEqual("Edited Content", requirement?.Content);
        }
    }

    [TestMethod]
    public async Task EditApprovedRequirementAsCreatorFailTest()
    {
        // Let's create it as a regular user, then approve as admin, then try to edit as regular user.
        var (email, password) = await RegisterAndLoginAsync();

        await PostForm("/Requirements/Create", new Dictionary<string, string>
        {
            { "Title", "Approved Creator Edit Test" },
            { "InputMarkdown", "Initial Content" }
        });

        int requirementId;
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            requirementId = (await db.Requirements.FirstAsync(r => r.Title == "Approved Creator Edit Test")).Id;
        }

        // Approve it as admin
        await LoginAsAdmin();
        await PostForm($"/Requirements/Approve/{requirementId}", new Dictionary<string, string>(), tokenUrl: $"/Requirements/View/{requirementId}");

        // Login back as the creator
        await PostForm("/Account/Login", new Dictionary<string, string>
        {
            { "EmailOrUserName", email },
            { "Password", password }
        });

        var editResponse = await PostForm($"/Requirements/Edit/{requirementId}", new Dictionary<string, string>
        {
            { "RequirementId", requirementId.ToString() },
            { "Title", "Edited Title" },
            { "InputMarkdown", "Edited Content" }
        }, tokenUrl: $"/Requirements/Edit/{requirementId}");

        // Should return 400 BadRequest as per controller logic
        Assert.AreEqual(HttpStatusCode.BadRequest, editResponse.StatusCode);
    }

    [TestMethod]
    public async Task EditApprovedRequirementAsAdminSuccessTest()
    {
        await RegisterAndLoginAsync();
        // Create a requirement as a regular user
        await PostForm("/Requirements/Create", new Dictionary<string, string>
        {
            { "Title", "Admin Edit Approved Test" },
            { "InputMarkdown", "Initial Content" }
        });

        int requirementId;
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            requirementId = (await db.Requirements.FirstAsync(r => r.Title == "Admin Edit Approved Test")).Id;
        }

        // Approve and Edit it as admin
        await LoginAsAdmin();
        await PostForm($"/Requirements/Approve/{requirementId}", new Dictionary<string, string>(), tokenUrl: $"/Requirements/View/{requirementId}");

        var editResponse = await PostForm($"/Requirements/Edit/{requirementId}", new Dictionary<string, string>
        {
            { "RequirementId", requirementId.ToString() },
            { "Title", "Admin Edited Title" },
            { "InputMarkdown", "Admin Edited Content" }
        }, tokenUrl: $"/Requirements/Edit/{requirementId}");

        Assert.AreEqual(HttpStatusCode.Found, editResponse.StatusCode);

        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var requirement = await db.Requirements.FindAsync(requirementId);
            Assert.AreEqual("Admin Edited Title", requirement?.Title);
            Assert.AreEqual(RequirementStatus.Approved, requirement?.Status); // Admin edit should keep it Approved
        }
    }

    [TestMethod]
    public async Task EditRequirementUnauthorizedUserTest()
    {
        await RegisterAndLoginAsync(); // User A
        await PostForm("/Requirements/Create", new Dictionary<string, string>
        {
            { "Title", "User A Requirement" },
            { "InputMarkdown", "Content" }
        });

        int requirementId;
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            requirementId = (await db.Requirements.FirstAsync(r => r.Title == "User A Requirement")).Id;
        }

        await RegisterAndLoginAsync(); // User B

        var editResponse = await PostForm($"/Requirements/Edit/{requirementId}", new Dictionary<string, string>
        {
            { "RequirementId", requirementId.ToString() },
            { "Title", "Unauthorized Edit" },
            { "InputMarkdown", "Bad Content" }
        }, tokenUrl: $"/Requirements/Edit/{requirementId}");

        // Should return 401 Unauthorized
        Assert.AreEqual(HttpStatusCode.Unauthorized, editResponse.StatusCode);
    }
}
