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
    public async Task CreateAcceptsExistingVaultLogicalPath()
    {
        await LoginAsAdmin();

        var filePath = $"audio/create-{Guid.NewGuid():N}.mp3";
        using (var scope = Server!.Services.CreateScope())
        {
            var storage = scope.ServiceProvider.GetRequiredService<StorageService>();
            await using var stream = new MemoryStream("audio"u8.ToArray());
            await storage.SaveFromStream(filePath, stream, isVault: true);
        }

        var response = await PostForm(
            "/Audio/Create",
            new Dictionary<string, string>
            {
                { "Name", "Standard logical path upload" },
                { "FilePath", filePath }
            },
            "/Audio/Create");

        Assert.AreEqual(HttpStatusCode.Found, response.StatusCode);
        using var verificationScope = Server.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
        var audio = await verificationDb.Audios.SingleAsync(item => item.Name == "Standard logical path upload");
        Assert.AreEqual(filePath, audio.FilePath);
    }

    [TestMethod]
    public async Task CreateRejectsLogicalPathOutsideAudioBucket()
    {
        await LoginAsAdmin();

        var response = await PostForm(
            "/Audio/Create",
            new Dictionary<string, string>
            {
                { "Name", "Invalid bucket upload" },
                { "FilePath", "contract/private.mp3" }
            },
            "/Audio/Create");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        using var verificationScope = Server!.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
        Assert.IsFalse(await verificationDb.Audios.AnyAsync(item => item.Name == "Invalid bucket upload"));
    }

    [TestMethod]
    public async Task CreateRejectsAudioPathEscapingItsVaultBucket()
    {
        await LoginAsAdmin();

        var response = await PostForm(
            "/Audio/Create",
            new Dictionary<string, string>
            {
                { "Name", "Escaped bucket upload" },
                { "FilePath", "audio/../contract/private.mp3" }
            },
            "/Audio/Create");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        using var verificationScope = Server!.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
        Assert.IsFalse(await verificationDb.Audios.AnyAsync(item => item.Name == "Escaped bucket upload"));
    }

    [TestMethod]
    public async Task DeletePreservesAFileReferencedByAnotherAudio()
    {
        await LoginAsAdmin();

        int deletedAudioId;
        string physicalPath;
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var storage = scope.ServiceProvider.GetRequiredService<StorageService>();
            var admin = await db.Users.FirstAsync(user => user.Email == "admin@default.com");
            var filePath = $"audio/shared-{Guid.NewGuid():N}.mp3";
            await using var stream = new MemoryStream("shared audio"u8.ToArray());
            await storage.SaveFromStream(filePath, stream, isVault: true);
            physicalPath = storage.GetFilePhysicalPath(filePath, isVault: true);
            var deletedAudio = new Audio { Name = "Shared one", FilePath = filePath, OwnerId = admin.Id };
            var remainingAudio = new Audio { Name = "Shared two", FilePath = filePath, OwnerId = admin.Id };
            db.Audios.AddRange(deletedAudio, remainingAudio);
            await db.SaveChangesAsync();
            deletedAudioId = deletedAudio.Id;
        }

        var response = await PostForm(
            $"/Audio/Delete/{deletedAudioId}",
            new Dictionary<string, string>(),
            "/Audio/Index");

        Assert.AreEqual(HttpStatusCode.Found, response.StatusCode);
        Assert.IsTrue(File.Exists(physicalPath));
        File.Delete(physicalPath);
    }

    [TestMethod]
    public async Task ReplacingSharedLegacyFilePreservesItForRemainingAudio()
    {
        await LoginAsAdmin();

        int replacedAudioId;
        string oldPhysicalPath;
        string newFilePath;
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var storage = scope.ServiceProvider.GetRequiredService<StorageService>();
            var admin = await db.Users.FirstAsync(user => user.Email == "admin@default.com");
            var oldFilePath = $"audio/shared-edit-{Guid.NewGuid():N}.mp3";
            await using var oldStream = new MemoryStream("old shared audio"u8.ToArray());
            await storage.SaveFromStream(oldFilePath, oldStream, isVault: true);
            oldPhysicalPath = storage.GetFilePhysicalPath(oldFilePath, isVault: true);
            var replacedAudio = new Audio { Name = "Replace shared one", FilePath = oldFilePath, OwnerId = admin.Id };
            var remainingAudio = new Audio { Name = "Replace shared two", FilePath = oldFilePath, OwnerId = admin.Id };
            db.Audios.AddRange(replacedAudio, remainingAudio);
            await db.SaveChangesAsync();
            replacedAudioId = replacedAudio.Id;

            newFilePath = $"audio/{admin.Id}/{Guid.NewGuid():N}.mp3";
            await using var newStream = new MemoryStream("replacement audio"u8.ToArray());
            await storage.SaveFromStream(newFilePath, newStream, isVault: true);
        }

        var response = await PostForm(
            "/Audio/Edit",
            new Dictionary<string, string>
            {
                { "Id", replacedAudioId.ToString() },
                { "Name", "Replaced shared one" },
                { "FilePath", newFilePath }
            },
            $"/Audio/Edit/{replacedAudioId}");
        Assert.AreEqual(HttpStatusCode.Found, response.StatusCode);

        await Task.Delay(TimeSpan.FromSeconds(2));

        using var verificationScope = Server!.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
        var updatedAudio = await verificationDb.Audios.FindAsync(replacedAudioId);
        Assert.AreEqual(newFilePath, updatedAudio?.FilePath);
        Assert.IsTrue(File.Exists(oldPhysicalPath));
        File.Delete(oldPhysicalPath);
    }

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

        var transcriptResponse = await Http.GetAsync($"/Audio/Transcript/{audioId}");
        transcriptResponse.EnsureSuccessStatusCode();
        var transcriptHtml = await transcriptResponse.Content.ReadAsStringAsync();
        StringAssert.Contains(transcriptHtml, "id=\"resetAsrModal\"");
        StringAssert.Contains(transcriptHtml, "Are you sure you want to continue?");

        var response = await PostForm(
            $"/Audio/ResetAsr/{audioId}",
            new Dictionary<string, string>(),
            $"/Audio/Transcript/{audioId}");
        AssertRedirect(response, $"/Audio/Transcript/{audioId}");

        var redirectedResponse = await Http.GetAsync(response.Headers.Location);
        redirectedResponse.EnsureSuccessStatusCode();
        var redirectedHtml = await redirectedResponse.Content.ReadAsStringAsync();
        StringAssert.Contains(
            redirectedHtml,
            "An offline ASR task has been created. Please do not create it again.");

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
    public async Task DeleteAudioRemovesStoredRecordingFile()
    {
        await LoginAsAdmin();

        int audioId;
        string physicalPath;
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var storage = scope.ServiceProvider.GetRequiredService<StorageService>();
            var admin = await db.Users.FirstAsync(user => user.Email == "admin@default.com");
            var filePath = $"audio/delete-{Guid.NewGuid():N}.mka";
            await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("audio"));
            filePath = await storage.SaveFromStream(filePath, stream, isVault: true);
            physicalPath = storage.GetFilePhysicalPath(filePath, isVault: true);
            var audio = new Audio
            {
                Name = "Delete File Test",
                FilePath = filePath,
                OwnerId = admin.Id
            };
            db.Audios.Add(audio);
            await db.SaveChangesAsync();
            audioId = audio.Id;
        }

        Assert.IsTrue(File.Exists(physicalPath));

        var response = await PostForm(
            $"/Audio/Delete/{audioId}",
            new Dictionary<string, string>(),
            "/Audio/Index");
        AssertRedirect(response, "/Audio");

        using var verificationScope = Server.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
        Assert.IsNull(await verificationDb.Audios.FindAsync(audioId));
        Assert.IsFalse(File.Exists(physicalPath));
    }

    [TestMethod]
    public async Task EditAudioNamePreservesExistingWebmRecording()
    {
        await LoginAsAdmin();

        int audioId;
        string filePath;
        string physicalPath;
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var storage = scope.ServiceProvider.GetRequiredService<StorageService>();
            var admin = await db.Users.FirstAsync(user => user.Email == "admin@default.com");
            await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("existing webm audio"));
            filePath = await storage.SaveFromStream(
                $"audio/edit-{Guid.NewGuid():N}.webm",
                stream,
                isVault: true);
            physicalPath = storage.GetFilePhysicalPath(filePath, isVault: true);
            var audio = new Audio
            {
                Name = "Original Name",
                FilePath = filePath,
                OwnerId = admin.Id
            };
            db.Audios.Add(audio);
            await db.SaveChangesAsync();
            audioId = audio.Id;
        }

        var response = await PostForm(
            "/Audio/Edit",
            new Dictionary<string, string>
            {
                { "Id", audioId.ToString() },
                { "Name", "Updated Name" },
                { "FilePath", filePath }
            },
            $"/Audio/Edit/{audioId}");

        AssertRedirect(response, $"/Audio/Transcript/{audioId}");
        Assert.IsTrue(File.Exists(physicalPath));
        using var verificationScope = Server.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
        var updatedAudio = await verificationDb.Audios.FindAsync(audioId);
        Assert.IsNotNull(updatedAudio);
        Assert.AreEqual("Updated Name", updatedAudio.Name);
        Assert.AreEqual(filePath, updatedAudio.FilePath);
        File.Delete(physicalPath);
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
        string replacementFilePath;
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

            var storage = scope.ServiceProvider.GetRequiredService<StorageService>();
            replacementFilePath = $"audio/{admin.Id}/{Guid.NewGuid():N}.mp3";
            await using var replacementStream = new MemoryStream("replacement audio"u8.ToArray());
            await storage.SaveFromStream(replacementFilePath, replacementStream, isVault: true);
        }

        var response = await PostForm(
            "/Audio/Edit",
            new Dictionary<string, string>
            {
                { "Id", audioId.ToString() },
                { "Name", "Replaced Recording" },
                { "FilePath", replacementFilePath }
            },
            $"/Audio/Edit/{audioId}");
        AssertRedirect(response, $"/Audio/Transcript/{audioId}");

        await Task.Delay(TimeSpan.FromSeconds(2));

        using var verificationScope = Server.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
        var replacedAudio = await verificationDb.Audios.FindAsync(audioId);
        Assert.IsNotNull(replacedAudio);
        Assert.AreEqual(replacementFilePath, replacedAudio.FilePath);
        Assert.AreEqual(0, replacedAudio.AsrAttemptCount);
        Assert.AreEqual(0, replacedAudio.EmptyResultCount);
        Assert.IsNull(replacedAudio.LastAsrAttemptTime);
        Assert.IsFalse(await verificationDb.AudioAsrResults.AnyAsync(result => result.AudioId == audioId));
    }
}
