using Aiursoft.EmployeeCenter.Configuration;
using Aiursoft.EmployeeCenter.InMemory;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.EmployeeCenter.Services.FileStorage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Aiursoft.EmployeeCenter.Tests;

[TestClass]
public class AsrSegmentationTests
{
    [TestMethod]
    public void ThreeHourMediaCreatesSixOverlappingWindows()
    {
        var windows = AsrMediaProcessor.CreateSegmentWindows(TimeSpan.FromHours(3), 1800, 2);

        Assert.HasCount(6, windows);
        Assert.AreEqual(0, windows[0].CoreStartMilliseconds);
        Assert.AreEqual(1_800_000, windows[0].CoreEndMilliseconds);
        Assert.AreEqual(0, windows[0].InputStartMilliseconds);
        Assert.AreEqual(1_802_000, windows[0].InputEndMilliseconds);
        Assert.AreEqual(1_798_000, windows[1].InputStartMilliseconds);
        Assert.AreEqual(3_602_000, windows[1].InputEndMilliseconds);
        Assert.AreEqual(8_998_000, windows[^1].InputStartMilliseconds);
        Assert.AreEqual(10_800_000, windows[^1].InputEndMilliseconds);
    }

    [TestMethod]
    public void DecodedProgressProvidesDurationWhenMetadataIsMissing()
    {
        const string progress = """
                                bitrate=N/A
                                out_time_us=1200000
                                progress=continue
                                out_time_us=3552653
                                progress=end
                                """;

        var duration = AsrMediaProcessor.ParseDecodedDuration(progress);

        Assert.AreEqual(TimeSpan.FromMicroseconds(3_552_653), duration);
    }

    [TestMethod]
    public void DecodedProgressRejectsInvalidDuration()
    {
        var duration = AsrMediaProcessor.ParseDecodedDuration("out_time_us=N/A");

        Assert.IsNull(duration);
    }

    [TestMethod]
    public void SegmentSelectionUsesAbsoluteCoreWindow()
    {
        var window = new AsrSegmentWindow(1, 1_800_000, 3_600_000, 1_798_000, 3_602_000);
        var responseSegments = new List<AsrApiSegment>
        {
            new() { Start = 0, End = 1, Text = "context only" },
            new() { Start = 1, End = 3, Text = "boundary sentence" },
            new() { Start = 1801, End = 1803, Text = "next context" }
        };

        var selected = AsrService.SelectTranscriptSegments(responseSegments, window, false, null);

        Assert.IsNotNull(selected);
        Assert.HasCount(1, selected);
        Assert.AreEqual("boundary sentence", selected[0].Text);
        Assert.AreEqual(1_799_000, selected[0].StartMilliseconds);
        Assert.AreEqual(1_801_000, selected[0].EndMilliseconds);
    }

    [TestMethod]
    public void MergeRemovesOnlyOverlappingIdenticalSegments()
    {
        var storedSegments = new List<AudioAsrSegment>
        {
            StoredSegment(0,
            [
                new AsrTranscriptSegment(0, 1000, "Opening"),
                new AsrTranscriptSegment(1799000, 1801000, "Same boundary")
            ]),
            StoredSegment(1,
            [
                new AsrTranscriptSegment(1800000, 1802000, "Same  boundary"),
                new AsrTranscriptSegment(1803000, 1804000, "Next"),
                new AsrTranscriptSegment(1805000, 1806000, "Next")
            ])
        };

        var text = AsrService.MergeTranscriptSegments(storedSegments);

        Assert.AreEqual("Opening Same boundary Next Next", text);
    }

    [TestMethod]
    public void MultiSegmentResponseRequiresTimestamps()
    {
        var window = new AsrSegmentWindow(0, 0, 1_800_000, 0, 1_802_000);

        var selected = AsrService.SelectTranscriptSegments(null, window, false, "plain text");

        Assert.IsNull(selected);
    }

    [TestMethod]
    public void TaskIdsAreGloballyUniqueAndAsrApiCompatible()
    {
        var taskIds = Enumerable.Range(0, 100)
            .Select(_ => AsrService.BuildTaskId(42, 3, 7))
            .ToList();

        Assert.HasCount(taskIds.Count, taskIds.Distinct().ToList());
        Assert.IsTrue(taskIds.All(taskId => taskId.Length <= 128));
        Assert.IsTrue(taskIds.All(taskId => taskId.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '.' or '_' or '~')));
    }

    [TestMethod]
    public void CancelEndpointUsesConfiguredApiBasePath()
    {
        var endpoint = AsrService.ResolveCancelEndpoint(
            "https://stt.example.com/v1/audio/transcriptions",
            "audio-42-segment-3");

        Assert.IsNotNull(endpoint);
        Assert.AreEqual(
            "https://stt.example.com/v1/tasks/audio-42-segment-3/cancel",
            endpoint.AbsoluteUri);
    }

    [TestMethod]
    public void CancelEndpointRejectsUnexpectedTranscriptionPath()
    {
        var endpoint = AsrService.ResolveCancelEndpoint(
            "https://stt.example.com/custom/transcribe",
            "audio-42-segment-3");

        Assert.IsNull(endpoint);
    }

    [TestMethod]
    public async Task CancelActiveTaskUsesGatewayCancellationEndpoint()
    {
        var handler = new RecordingHttpMessageHandler();
        var service = new AsrService(
            new HttpClient(handler),
            Options.Create(new AsrSettings
            {
                Endpoint = "https://stt.example.com/v1/audio/transcriptions",
                BearerToken = "test-token"
            }),
            null!,
            null!,
            null!,
            NullLogger<AsrService>.Instance);
        var audio = new Audio
        {
            Name = "Cancellation Test",
            FilePath = "audio/cancellation-test.mp3",
            AsrActiveTaskId = "audio-42-segment-3"
        };

        await service.CancelActiveTaskAsync(audio);

        Assert.IsNotNull(handler.Request);
        Assert.AreEqual(HttpMethod.Post, handler.Request.Method);
        Assert.AreEqual(
            "https://stt.example.com/v1/tasks/audio-42-segment-3/cancel",
            handler.Request.RequestUri?.AbsoluteUri);
        Assert.AreEqual("Bearer", handler.Request.Headers.Authorization?.Scheme);
        Assert.AreEqual("test-token", handler.Request.Headers.Authorization?.Parameter);
        Assert.IsTrue(handler.CancellationCanBeCanceled);
    }

    [TestMethod]
    public async Task CancelActiveTaskPropagatesGatewayFailure()
    {
        var handler = new RecordingHttpMessageHandler(
            HttpStatusCode.BadGateway,
            "upstream cancellation failed");
        var service = new AsrService(
            new HttpClient(handler),
            Options.Create(new AsrSettings
            {
                Endpoint = "https://stt.example.com/v1/audio/transcriptions",
                BearerToken = "test-token"
            }),
            null!,
            null!,
            null!,
            NullLogger<AsrService>.Instance);
        var audio = new Audio
        {
            Name = "Cancellation Failure Test",
            FilePath = "audio/cancellation-failure-test.mp3",
            AsrActiveTaskId = "audio-42-segment-3"
        };

        var exception = await Assert.ThrowsExactlyAsync<HttpRequestException>(
            async () => await service.CancelActiveTaskAsync(audio));

        Assert.AreEqual(HttpStatusCode.BadGateway, exception.StatusCode);
        StringAssert.Contains(exception.Message, "upstream cancellation failed");
    }

    [TestMethod]
    public async Task FailedCancellationDoesNotAdvanceProcessingAttempt()
    {
        var options = new DbContextOptionsBuilder<InMemoryContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new InMemoryContext(options);
        var processingToken = Guid.NewGuid().ToString("N");
        var audio = new Audio
        {
            Name = "Failed Takeover Test",
            FilePath = "audio/failed-takeover-test.mp3",
            AsrProcessingToken = processingToken,
            AsrActiveTaskId = "previous-task"
        };
        db.Audios.Add(audio);
        await db.SaveChangesAsync();

        var handler = new RecordingHttpMessageHandler(
            HttpStatusCode.BadGateway,
            "upstream cancellation failed");
        var service = new AsrService(
            new HttpClient(handler),
            Options.Create(new AsrSettings
            {
                Endpoint = "https://stt.example.com/v1/audio/transcriptions",
                BearerToken = "test-token"
            }),
            db,
            null!,
            null!,
            NullLogger<AsrService>.Instance);

        await Assert.ThrowsExactlyAsync<HttpRequestException>(
            async () => await service.ProcessAudioAsrAsync(audio.Id));

        Assert.AreEqual(processingToken, audio.AsrProcessingToken);
        Assert.AreEqual("previous-task", audio.AsrActiveTaskId);
        Assert.AreEqual(0, audio.AsrAttemptCount);
    }

    [TestMethod]
    public async Task NewProcessingAttemptCancelsPreviouslyActiveTask()
    {
        var options = new DbContextOptionsBuilder<InMemoryContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new InMemoryContext(options);
        var previousToken = Guid.NewGuid().ToString("N");
        var audio = new Audio
        {
            Name = "Takeover Test",
            FilePath = "audio/takeover-test.mp3",
            AsrProcessingToken = previousToken,
            AsrActiveTaskId = "previous-task"
        };
        db.Audios.Add(audio);
        await db.SaveChangesAsync();

        var storageRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var storageConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:Path"] = storageRoot
            })
            .Build();
        var storageService = new StorageService(
            new FeatureFoldersProvider(new StorageRootPathProvider(storageConfiguration)),
            null!,
            null!);
        var handler = new RecordingHttpMessageHandler();
        var service = new AsrService(
            new HttpClient(handler),
            Options.Create(new AsrSettings
            {
                Endpoint = "https://stt.example.com/v1/audio/transcriptions",
                BearerToken = "test-token"
            }),
            db,
            storageService,
            null!,
            NullLogger<AsrService>.Instance);

        try
        {
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                async () => await service.ProcessAudioAsrAsync(audio.Id));
        }
        finally
        {
            Directory.Delete(storageRoot, recursive: true);
        }

        Assert.IsNotNull(handler.Request);
        Assert.AreEqual(
            "https://stt.example.com/v1/tasks/previous-task/cancel",
            handler.Request.RequestUri?.AbsoluteUri);
        Assert.AreNotEqual(previousToken, audio.AsrProcessingToken);
        Assert.IsNull(audio.AsrActiveTaskId);
        Assert.AreEqual(1, audio.AsrAttemptCount);
    }

    [TestMethod]
    public void FailedAsrResponseRemainsRetryable()
    {
        Assert.IsNull(AsrService.GetRecognizedText(null));
    }

    [TestMethod]
    public void SuccessfulAsrResponseWithoutTextIsEmpty()
    {
        Assert.AreEqual(string.Empty, AsrService.GetRecognizedText(new AsrResponse()));
    }

    [TestMethod]
    public void MatroskaAudioSegmentsAreSupported()
    {
        Assert.IsTrue(AsrService.IsSupportedMediaExtension(".mka"));
    }

    [TestMethod]
    public void ProcessingTimeoutIncludesUploadAndCleanupBuffer()
    {
        var settings = new AsrSettings
        {
            TimeoutSeconds = 1800
        };

        Assert.AreEqual(TimeSpan.FromSeconds(2400), settings.GetProcessingTimeout());
    }

    [TestMethod]
    public void SegmentationDefaultsToThirtyMinutesWithTwoSecondOverlap()
    {
        var settings = new AsrSettings();

        Assert.AreEqual(1800, settings.SegmentDurationSeconds);
        Assert.AreEqual(2, settings.SegmentOverlapSeconds);
        Assert.IsTrue(settings.PreferOriginalSegmentCodec);
        settings.ValidateSegmentation();
    }

    [TestMethod]
    public void SegmentationAllowsZeroOverlap()
    {
        var settings = new AsrSettings
        {
            SegmentDurationSeconds = 60,
            SegmentOverlapSeconds = 0
        };

        settings.ValidateSegmentation();
    }

    [TestMethod]
    public void SegmentationRejectsOverlapEqualToDuration()
    {
        var settings = new AsrSettings
        {
            SegmentDurationSeconds = 60,
            SegmentOverlapSeconds = 60
        };

        Assert.ThrowsExactly<InvalidOperationException>(settings.ValidateSegmentation);
    }

    private static AudioAsrSegment StoredSegment(int index, IReadOnlyList<AsrTranscriptSegment> segments)
    {
        return new AudioAsrSegment
        {
            AudioId = 1,
            SegmentIndex = index,
            CoreStartMilliseconds = 0,
            CoreEndMilliseconds = 0,
            InputStartMilliseconds = 0,
            InputEndMilliseconds = 0,
            SegmentDurationSeconds = 1800,
            OverlapSeconds = 2,
            SegmentsJson = JsonConvert.SerializeObject(segments),
            PlainText = string.Empty
        };
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string? _content;

        public RecordingHttpMessageHandler(
            HttpStatusCode statusCode = HttpStatusCode.Accepted,
            string? content = null)
        {
            _statusCode = statusCode;
            _content = content;
        }

        public HttpRequestMessage? Request { get; private set; }
        public bool CancellationCanBeCanceled { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            CancellationCanBeCanceled = cancellationToken.CanBeCanceled;
            var response = new HttpResponseMessage(_statusCode);
            if (_content != null)
            {
                response.Content = new StringContent(_content);
            }
            return Task.FromResult(response);
        }
    }
}
