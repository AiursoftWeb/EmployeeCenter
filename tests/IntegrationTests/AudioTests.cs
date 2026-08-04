using Aiursoft.EmployeeCenter.Configuration;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.EmployeeCenter.Services.BackgroundJobs;
using Aiursoft.EmployeeCenter.Services.FileStorage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class AudioTests : TestBase
{
    [TestMethod]
    public async Task AudioAsrJobReportsFailedAudioProcessing()
    {
        using var scope = Server!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<StorageService>();
        var settings = new AsrSettings
        {
            Enabled = true,
            Endpoint = "https://asr.example.com/v1/audio/transcriptions",
            BearerToken = "test-token"
        };
        var options = Options.Create(settings);
        var mediaProcessor = new AsrMediaProcessor(
            options,
            NullLogger<AsrMediaProcessor>.Instance);
        var asrService = new AsrService(
            new HttpClient(),
            options,
            db,
            storage,
            mediaProcessor,
            NullLogger<AsrService>.Instance);
        var job = new AudioAsrJob(
            db,
            asrService,
            options,
            NullLogger<AudioAsrJob>.Instance);
        db.Audios.Add(new Audio
        {
            Name = "Missing ASR Media",
            FilePath = $"audio/{Guid.NewGuid():N}.mp3"
        });
        await db.SaveChangesAsync();

        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(job.ExecuteAsync);

        StringAssert.Contains(exception.Message, "ASR processing failed for audio IDs");
    }

    [TestMethod]
    public async Task ResetAsrClearsCompletedAndSegmentResults()
    {
        await LoginAsAdmin();

        int audioId;
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var admin = await db.Users.FirstAsync(user => user.Email == "admin@default.com");
            var audio = new Audio
            {
                Name = "Reset ASR Test",
                FilePath = "audio/reset-test.mp3",
                OwnerId = admin.Id,
                AsrAttemptCount = 3,
                EmptyResultCount = 1
            };
            db.Audios.Add(audio);
            await db.SaveChangesAsync();
            audioId = audio.Id;
            db.AudioAsrResults.Add(new AudioAsrResult
            {
                AudioId = audioId,
                PlainText = "completed"
            });
            db.AudioAsrSegments.Add(new AudioAsrSegment
            {
                AudioId = audioId,
                SegmentIndex = 0,
                SegmentDurationSeconds = 1800,
                OverlapSeconds = 2,
                SegmentsJson = "[]",
                PlainText = "completed"
            });
            await db.SaveChangesAsync();
        }

        var response = await PostForm(
            $"/Audio/ResetAsr/{audioId}",
            new Dictionary<string, string>(),
            $"/Audio/Transcript/{audioId}");
        AssertRedirect(response, $"/Audio/Transcript/{audioId}");

        using var verificationScope = Server.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
        var audioAfterReset = await verificationDb.Audios.FindAsync(audioId);
        Assert.IsNotNull(audioAfterReset);
        Assert.AreEqual(0, audioAfterReset.AsrAttemptCount);
        Assert.AreEqual(0, audioAfterReset.EmptyResultCount);
        Assert.IsFalse(string.IsNullOrEmpty(audioAfterReset.AsrProcessingToken));
        Assert.IsNull(audioAfterReset.AsrActiveTaskId);
        Assert.IsFalse(await verificationDb.AudioAsrResults.AnyAsync(result => result.AudioId == audioId));
        Assert.IsFalse(await verificationDb.AudioAsrSegments.AnyAsync(segment => segment.AudioId == audioId));
    }

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
            db.AudioAsrResults.Add(new AudioAsrResult
            {
                AudioId = audio.Id,
                PlainText = "Original transcript",
                MeetingMinutesMarkdown = "# Meeting Summary\n\n| Item | Owner |\n|---|---|\n| Ship | Alice |\n\n<script>alert('x')</script>"
            });
            await db.SaveChangesAsync();
            audioId = audio.Id;
        }

        var ownerTranscriptResponse = await Http.GetAsync($"/Audio/Transcript/{audioId}");
        ownerTranscriptResponse.EnsureSuccessStatusCode();
        var ownerTranscriptHtml = await ownerTranscriptResponse.Content.ReadAsStringAsync();
        StringAssert.Contains(ownerTranscriptHtml, "Meeting Summary");
        StringAssert.Contains(ownerTranscriptHtml, "<table");
        Assert.DoesNotContain("<script>alert('x')</script>", ownerTranscriptHtml);

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
        var sharedTranscriptHtml = await transcriptResponse.Content.ReadAsStringAsync();
        StringAssert.Contains(sharedTranscriptHtml, "Meeting Summary");

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

    [TestMethod]
    public async Task ResetAsrClearsTranscriptMinutesAndRetryState()
    {
        await LoginAsAdmin();

        int audioId;
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var admin = await db.Users.FirstAsync(user => user.Email == "admin@default.com");
            var audio = new Audio
            {
                Name = "Reset Minutes Recording",
                FilePath = "audio/reset-minutes.mp3",
                OwnerId = admin.Id,
                AsrAttemptCount = 4,
                EmptyResultCount = 2,
                LastAsrAttemptTime = DateTime.UtcNow
            };
            db.Audios.Add(audio);
            await db.SaveChangesAsync();
            db.AudioAsrResults.Add(new AudioAsrResult
            {
                AudioId = audio.Id,
                PlainText = "Transcript",
                MeetingMinutesMarkdown = "# Minutes",
                MeetingMinutesAttemptCount = 2,
                LastMeetingMinutesAttemptTime = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            audioId = audio.Id;
        }

        var response = await PostForm(
            $"/Audio/ResetAsr/{audioId}",
            new Dictionary<string, string> { { "id", audioId.ToString() } },
            $"/Audio/Transcript/{audioId}");
        AssertRedirect(response, $"/Audio/Transcript/{audioId}");

        using var verificationScope = Server.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
        var audioAfterReset = await verificationDb.Audios.FindAsync(audioId);
        Assert.IsNotNull(audioAfterReset);
        Assert.AreEqual(0, audioAfterReset.AsrAttemptCount);
        Assert.AreEqual(0, audioAfterReset.EmptyResultCount);
        Assert.IsNull(audioAfterReset.LastAsrAttemptTime);
        Assert.IsFalse(await verificationDb.AudioAsrResults.AnyAsync(result => result.AudioId == audioId));
    }

    [TestMethod]
    public async Task ReplacingAudioClearsTranscriptMinutesAndRetryState()
    {
        await LoginAsAdmin();

        int audioId;
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var admin = await db.Users.FirstAsync(user => user.Email == "admin@default.com");
            var audio = new Audio
            {
                Name = "Replace Minutes Recording",
                FilePath = "audio/original.mp3",
                OwnerId = admin.Id,
                AsrAttemptCount = 3,
                EmptyResultCount = 1,
                LastAsrAttemptTime = DateTime.UtcNow
            };
            db.Audios.Add(audio);
            await db.SaveChangesAsync();
            db.AudioAsrResults.Add(new AudioAsrResult
            {
                AudioId = audio.Id,
                PlainText = "Old transcript",
                MeetingMinutesMarkdown = "# Old minutes",
                MeetingMinutesAttemptCount = 2,
                LastMeetingMinutesAttemptTime = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            audioId = audio.Id;

            var storage = scope.ServiceProvider.GetRequiredService<Services.FileStorage.StorageService>();
            var replacementPath = storage.GetFilePhysicalPath("audio/replacement.mp3", isVault: true);
            Directory.CreateDirectory(Path.GetDirectoryName(replacementPath)!);
            await File.WriteAllTextAsync(replacementPath, "replacement audio");
        }

        var response = await PostForm(
            "/Audio/Edit",
            new Dictionary<string, string>
            {
                { "Id", audioId.ToString() },
                { "Name", "Replaced Recording" },
                { "FilePath", "audio/replacement.mp3" }
            },
            $"/Audio/Edit/{audioId}");
        AssertRedirect(response, $"/Audio/Transcript/{audioId}");

        using var verificationScope = Server.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
        var replacedAudio = await verificationDb.Audios.FindAsync(audioId);
        Assert.IsNotNull(replacedAudio);
        Assert.AreEqual("audio/replacement.mp3", replacedAudio.FilePath);
        Assert.AreEqual(0, replacedAudio.AsrAttemptCount);
        Assert.AreEqual(0, replacedAudio.EmptyResultCount);
        Assert.IsNull(replacedAudio.LastAsrAttemptTime);
        Assert.IsFalse(await verificationDb.AudioAsrResults.AnyAsync(result => result.AudioId == audioId));
    }
}
