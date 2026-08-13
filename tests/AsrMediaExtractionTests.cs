using System.Diagnostics;
using Aiursoft.EmployeeCenter.Configuration;
using Aiursoft.EmployeeCenter.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Aiursoft.EmployeeCenter.Tests;

[TestClass]
public class AsrMediaExtractionTests
{
    [TestMethod]
    public async Task ProbeReportsVideoStreamFromMediaContent()
    {
        var processor = new StubMediaProcessor(
            preferOriginalCodec: true,
            output: """
                    {"streams":[{"codec_type":"video"},{"codec_type":"audio"}],"format":{"duration":"12.5"}}
                    """);

        var probe = await processor.ProbeAsync("recording.mp3");

        Assert.IsTrue(probe.HasVideoStream);
        Assert.AreEqual(TimeSpan.FromSeconds(12.5), probe.Duration);
    }

    [TestMethod]
    public async Task ProbeRejectsMediaWithoutAudioStream()
    {
        var processor = new StubMediaProcessor(
            preferOriginalCodec: true,
            output: """
                    {"streams":[{"codec_type":"video"}],"format":{"duration":"12.5"}}
                    """);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await processor.ProbeAsync("silent-video.mp4"));
    }

    [TestMethod]
    public async Task DecodedDurationUsesMediaProcessingTimeout()
    {
        var processor = new StubMediaProcessor(
            preferOriginalCodec: true,
            mediaProcessingTimeoutSeconds: 123,
            output: """
                    {"streams":[{"codec_type":"audio"}],"format":{}}
                    """,
            ffmpegOutput: "out_time_us=2500000");

        var probe = await processor.ProbeAsync("durationless.mp3");

        Assert.AreEqual(TimeSpan.FromSeconds(2.5), probe.Duration);
        Assert.AreEqual(TimeSpan.FromSeconds(123), processor.LastTimeout);
    }

    [TestMethod]
    public async Task StreamCopyTimeoutDeletesPartialOutput()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("asr-extraction-timeout-");
        try
        {
            var processor = new StubMediaProcessor(
                preferOriginalCodec: true,
                exception: new TimeoutException("ffmpeg timed out"));

            await Assert.ThrowsExactlyAsync<TimeoutException>(() => processor.ExtractAudioTrackAsync(
                "input.mp4",
                tempDirectory.FullName,
                "timeout"));

            Assert.IsFalse(File.Exists(Path.Combine(tempDirectory.FullName, "timeout.mka")));
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [TestMethod]
    public async Task FlacFailureDeletesPartialOutput()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("asr-extraction-failure-");
        try
        {
            var processor = new StubMediaProcessor(preferOriginalCodec: false, exitCode: 1);

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => processor.ExtractAudioTrackAsync(
                "input.mp4",
                tempDirectory.FullName,
                "failure"));

            Assert.IsFalse(File.Exists(Path.Combine(tempDirectory.FullName, "failure.flac")));
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [TestMethod]
    public async Task FlacCancellationDeletesPartialOutput()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("asr-extraction-cancellation-");
        try
        {
            using var cancellationSource = new CancellationTokenSource();
            cancellationSource.Cancel();
            var processor = new StubMediaProcessor(
                preferOriginalCodec: false,
                exception: new OperationCanceledException(cancellationSource.Token));

            await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => processor.ExtractAudioTrackAsync(
                "input.mp4",
                tempDirectory.FullName,
                "cancellation",
                cancellationSource.Token));

            Assert.IsFalse(File.Exists(Path.Combine(tempDirectory.FullName, "cancellation.flac")));
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    private sealed class StubMediaProcessor(
        bool preferOriginalCodec,
        int exitCode = 0,
        Exception? exception = null,
        string output = "",
        string? ffmpegOutput = null,
        int mediaProcessingTimeoutSeconds = 1800)
        : AsrMediaProcessor(
            Options.Create(new AsrSettings
            {
                PreferOriginalSegmentCodec = preferOriginalCodec,
                MediaProcessingTimeoutSeconds = mediaProcessingTimeoutSeconds
            }),
            NullLogger<AsrMediaProcessor>.Instance)
    {
        public TimeSpan LastTimeout { get; private set; }

        protected override async Task<ProcessResult> RunProcessAsync(
            ProcessStartInfo startInfo,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            LastTimeout = timeout;
            if (startInfo.FileName == "ffmpeg" && startInfo.ArgumentList[^1] != "pipe:1")
            {
                var outputPath = startInfo.ArgumentList[^1];
                await File.WriteAllBytesAsync(outputPath, "partial"u8.ToArray(), CancellationToken.None);
            }
            if (exception != null)
            {
                throw exception;
            }
            var processOutput = startInfo.FileName == "ffmpeg" ? ffmpegOutput ?? output : output;
            return new ProcessResult(exitCode, processOutput, "stub failure");
        }
    }
}
