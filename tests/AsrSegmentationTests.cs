using Aiursoft.EmployeeCenter.Configuration;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Services;
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
    public void ProcessingTimeoutIncludesCleanupBuffer()
    {
        var settings = new AsrSettings
        {
            TimeoutSeconds = 1800
        };

        Assert.AreEqual(TimeSpan.FromSeconds(1830), settings.GetProcessingTimeout());
    }

    [TestMethod]
    public void MissingSystemEndpointUsesTranscriptionEndpointOrigin()
    {
        var settings = new AsrSettings
        {
            Endpoint = "https://asr.example.com/v1/audio/transcriptions"
        };

        Assert.AreEqual("https://asr.example.com/v1/system", settings.ResolveSystemEndpoint());
    }

    [TestMethod]
    public void ExplicitSystemEndpointIsPreserved()
    {
        var settings = new AsrSettings
        {
            Endpoint = "https://asr.example.com/v1/audio/transcriptions",
            SystemEndpoint = "https://status.example.com/custom/system"
        };

        Assert.AreEqual(settings.SystemEndpoint, settings.ResolveSystemEndpoint());
    }

    [TestMethod]
    public void NonstandardTranscriptionEndpointDoesNotDeriveSystemEndpoint()
    {
        var settings = new AsrSettings
        {
            Endpoint = "https://asr.example.com/custom/transcribe"
        };

        Assert.IsNull(settings.ResolveSystemEndpoint());
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
}
