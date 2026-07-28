namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class AudioTests : TestBase
{
    [TestMethod]
    public async Task AudioSharingMatchesPasswordSharing()
    {
        await LoginAsAdmin();

        const string audioName = "Sharing Test Recording";
        int audioId;
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var admin = await db.Users.FirstAsync(user => user.Email == "admin@default.com");
            var audio = new Audio
            {
                Name = audioName,
                FilePath = "audio/sharing-test.mp3",
                OwnerId = admin.Id
            };
            db.Audios.Add(audio);
            await db.SaveChangesAsync();
            audioId = audio.Id;
        }

        await Http.GetAsync("/Account/LogOff");
        var (email, password) = await RegisterAndLoginAsync();

        string userId;
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            userId = await db.Users
                .Where(user => user.Email == email)
                .Select(user => user.Id)
                .FirstAsync();
        }

        var indexResponse = await Http.GetAsync("/Audio/Index");
        indexResponse.EnsureSuccessStatusCode();
        var indexHtml = await indexResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain(audioName, indexHtml);

        var transcriptResponse = await Http.GetAsync($"/Audio/Transcript/{audioId}");
        Assert.AreEqual(HttpStatusCode.NotFound, transcriptResponse.StatusCode);

        await Http.GetAsync("/Account/LogOff");
        await LoginAsAdmin();

        var shareResponse = await PostForm(
            $"/Audio/AddShare/{audioId}",
            new Dictionary<string, string>
            {
                { "TargetUserId", userId },
                { "Permission", ((int)SharePermission.ReadOnly).ToString() }
            },
            $"/Audio/ManageShares/{audioId}");
        AssertRedirect(shareResponse, $"/Audio/ManageShares/{audioId}");

        await Http.GetAsync("/Account/LogOff");
        await LoginAsAsync(email, password);

        indexResponse = await Http.GetAsync("/Audio/Index");
        indexResponse.EnsureSuccessStatusCode();
        indexHtml = await indexResponse.Content.ReadAsStringAsync();
        StringAssert.Contains(indexHtml, audioName);

        transcriptResponse = await Http.GetAsync($"/Audio/Transcript/{audioId}");
        transcriptResponse.EnsureSuccessStatusCode();

        var editResponse = await Http.GetAsync($"/Audio/Edit/{audioId}");
        Assert.AreEqual(HttpStatusCode.Unauthorized, editResponse.StatusCode);

        using (var scope = Server.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var share = await db.AudioShares.SingleAsync(item =>
                item.AudioId == audioId && item.SharedWithUserId == userId);
            share.Permission = SharePermission.Editable;
            await db.SaveChangesAsync();
        }

        editResponse = await Http.GetAsync($"/Audio/Edit/{audioId}");
        editResponse.EnsureSuccessStatusCode();
    }
}
