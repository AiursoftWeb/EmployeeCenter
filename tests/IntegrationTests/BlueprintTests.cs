namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class BlueprintTests : TestBase
{
    [TestMethod]
    public async Task GetCreateBlueprintPageAdminAccessTest()
    {
        await LoginAsAdmin();
        var response = await Http.GetAsync("/Blueprint/Create");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        // Assert that the old highlight.js scripts are gone
        Assert.DoesNotContain("node_modules/highlight.js/lib/core.js", html);
        Assert.DoesNotContain("node_modules/highlight.js/lib/languages/csharp.js", html);

        // Assert that the new highlight.js cdn-assets script is present
        Assert.Contains("node_modules/@highlightjs/cdn-assets/highlight.min.js", html);

        // Assert that the styles are also updated
        Assert.Contains("node_modules/@highlightjs/cdn-assets/styles/github.min.css", html);
    }

    [TestMethod]
    public async Task ManageBlueprintFolderTest()
    {
        await LoginAsAdmin();

        // 1. Create Root Folder
        var createFolderResponse = await PostForm("/Blueprint/CreateFolder", new Dictionary<string, string>
        {
            { "Name", "Arch Root" }
        });
        AssertRedirect(createFolderResponse, "/Blueprint/Manage", exact: false);

        var dbContext = GetService<EmployeeCenterDbContext>();
        var rootFolder = await dbContext.BlueprintFolders.FirstAsync(f => f.Name == "Arch Root");

        // 2. Create Sub Folder
        var createSubFolderResponse = await PostForm($"/Blueprint/CreateFolder/{rootFolder.Id}", new Dictionary<string, string>
        {
            { "Name", "Frontend" },
            { "ParentFolderId", rootFolder.Id.ToString() }
        }, tokenUrl: $"/Blueprint/CreateFolder/{rootFolder.Id}");
        AssertRedirect(createSubFolderResponse, $"/Blueprint/Manage/{rootFolder.Id}", exact: false);

        var subFolder = await dbContext.BlueprintFolders.FirstAsync(f => f.Name == "Frontend");

        // 3. Create Blueprint in Sub Folder
        var createBlueprintResponse = await PostForm("/Blueprint/Create", new Dictionary<string, string>
        {
            { "Title", "React Guidelines" },
            { "InputMarkdown", "# Hello React" },
            { "FolderId", subFolder.Id.ToString() }
        });
        // Redirects to Edit page
        Assert.AreEqual(HttpStatusCode.Found, createBlueprintResponse.StatusCode);

        // 4. Verify in Manage
        var manageSubResponse = await Http.GetAsync($"/Blueprint/Manage/{subFolder.Id}");
        var manageSubHtml = await manageSubResponse.Content.ReadAsStringAsync();
        Assert.Contains("React Guidelines", manageSubHtml);

        // 5. Test Circular Reference
        var moveRootToSubResponse = await PostForm("/Blueprint/EditFolder", new Dictionary<string, string>
        {
            { "Id", rootFolder.Id.ToString() },
            { "Name", "Arch Root Renamed" },
            { "ParentFolderId", subFolder.Id.ToString() }
        }, tokenUrl: $"/Blueprint/EditFolder/{rootFolder.Id}");
        
        Assert.AreEqual(HttpStatusCode.OK, moveRootToSubResponse.StatusCode);
        var moveHtml = await moveRootToSubResponse.Content.ReadAsStringAsync();
        Assert.Contains("Cannot move a folder to its own child!", moveHtml);
    }
}
