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
        Exception? exception = null)
        : AsrMediaProcessor(
            Options.Create(new AsrSettings { PreferOriginalSegmentCodec = preferOriginalCodec }),
            NullLogger<AsrMediaProcessor>.Instance)
    {
        protected override async Task<ProcessResult> RunProcessAsync(
            ProcessStartInfo startInfo,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            var outputPath = startInfo.ArgumentList[^1];
            await File.WriteAllBytesAsync(outputPath, "partial"u8.ToArray(), CancellationToken.None);
            if (exception != null)
            {
                throw exception;
            }
            return new ProcessResult(exitCode, string.Empty, "stub failure");
        }
    }
}
