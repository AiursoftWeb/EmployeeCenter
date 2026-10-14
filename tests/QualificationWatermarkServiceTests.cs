using System.Buffers.Binary;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using Aiursoft.EmployeeCenter.Services;
using Microsoft.Extensions.Localization;
using SkiaSharp;

namespace Aiursoft.EmployeeCenter.Tests;

[TestClass]
public class QualificationWatermarkServiceTests
{
    private static readonly QualificationWatermarkRequest Request = new(
        "Qualification review", "Test recipient", "Test applicant", new DateTime(2026, 10, 12, 5, 23, 17, DateTimeKind.Utc),
        Guid.Parse("ebd2caa5-7e7e-4cc3-a779-3c44e2928ba7"));
    private ServiceProvider _localizationServices = null!;
    private QualificationWatermarkService _service = null!;
    private CultureInfo _originalCulture = null!;
    private CultureInfo _originalUiCulture = null!;
    private string? _fontPath;

    [TestInitialize]
    public void Initialize()
    {
        _originalCulture = CultureInfo.CurrentCulture;
        _originalUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-GB");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-GB");
        _localizationServices = new ServiceCollection()
            .AddLogging()
            .AddLocalization(options => options.ResourcesPath = "Resources")
            .BuildServiceProvider();
        _fontPath = new[]
        {
            Environment.GetEnvironmentVariable("QUALIFICATION_TEST_FONT_PATH"),
            "/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc",
            "/usr/share/fonts/truetype/noto/NotoSansCJK-Regular.ttc",
            "/usr/share/fonts/truetype/droid/DroidSansFallbackFull.ttf"
        }.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));
        _service = CreateService(_fontPath);
    }

    [TestCleanup]
    public void Cleanup()
    {
        CultureInfo.CurrentCulture = _originalCulture;
        CultureInfo.CurrentUICulture = _originalUiCulture;
        _localizationServices.Dispose();
    }

    [TestMethod]
    public void PngContainsBlindlyRecoverableTraceIdAndIsDeterministic()
    {
        RequireRenderingFont();
        using var source = CreateSource(512, 512);
        var output = _service.Generate(source, Request);
        using var downloaded = new MemoryStream(output);
        Assert.AreEqual(Request.TraceId, _service.Detect(downloaded));
        CollectionAssert.AreEqual(output, _service.Generate(source, Request));
        CollectionAssert.AreEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, output[..8]);
        Assert.IsFalse(System.Text.Encoding.UTF8.GetString(output).Contains(Request.ApplicantName));
    }

    [TestMethod]
    [DataRow(512, 512, "white", 85)]
    [DataRow(512, 512, "black", 85)]
    [DataRow(512, 512, "noise", 85)]
    [DataRow(512, 512, "document", 85)]
    [DataRow(517, 769, "gradient", 85)]
    [DataRow(1024, 768, "document", 90)]
    [DataRow(1536, 1024, "noise", 95)]
    [DataRow(2048, 512, "gradient", 85)]
    public void JpegRecompressionPreservesTraceId(int width, int height, string pattern, int quality)
    {
        RequireRenderingFont();
        using var source = CreateSource(width, height, pattern);
        var marked = _service.Generate(source, Request);
        using var downloadedPng = new MemoryStream(marked);
        Assert.AreEqual(Request.TraceId, _service.Detect(downloadedPng), "Original PNG");
        using var bitmap = SKBitmap.Decode(marked);
        using var image = SKImage.FromBitmap(bitmap);
        using var jpeg = image.Encode(SKEncodedImageFormat.Jpeg, quality);
        using var recompressed = new MemoryStream(jpeg.ToArray());
        Assert.AreEqual(Request.TraceId, _service.Detect(recompressed), $"JPEG quality {quality}, {pattern}");
    }

    [TestMethod]
    public void EveryJpegQualityFrom85Through100PreservesMinimumSizeSignal()
    {
        RequireRenderingFont();
        using var source = CreateSource(512, 512, "noise");
        using var bitmap = SKBitmap.Decode(_service.Generate(source, Request));
        using var image = SKImage.FromBitmap(bitmap);
        for (var quality = 85; quality <= 100; quality++)
        {
            using var jpeg = image.Encode(SKEncodedImageFormat.Jpeg, quality);
            using var recompressed = new MemoryStream(jpeg.ToArray());
            Assert.AreEqual(Request.TraceId, _service.Detect(recompressed), $"JPEG quality {quality}");
        }
    }

    [TestMethod]
    [DataRow("en-GB")]
    [DataRow("zh-CN")]
    [DataRow("zh-HK")]
    [DataRow("zh-TW")]
    [DataRow("fr-FR")]
    public void CurrentUiCultureRendersExactLocalizedTextAndPreservesTrace(string culture)
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture.StartsWith("zh-", StringComparison.Ordinal) ? "en-GB" : "zh-CN");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        var request = Request with
        {
            Purpose = "Purpose {0} / 100%",
            Recipient = "Recipient {1}",
            ApplicantName = "Applicant {2}"
        };
        var localizer = CreateLocalizer();
        Assert.IsFalse(localizer["For {0} only / No other use"].ResourceNotFound,
            "The service must use its embedded localization resources, including the English fallback.");
        string[] expectedLines =
        [
            localizer["For {0} only / No other use", request.Purpose].Value,
            localizer["Recipient: {0}", request.Recipient].Value,
            localizer["Applicant: {0}", request.ApplicantName].Value,
            localizer["UTC: {0} UTC", "2026-10-12 05:23:17"].Value,
            localizer["Trace ID: {0}", request.TraceId.ToString("D")].Value
        ];
        if (culture == "zh-CN")
        {
            CollectionAssert.AreEqual(new[] { 0x4ec5, 0x4f9b }, expectedLines[0].EnumerateRunes().Take(2).Select(rune => rune.Value).ToArray());
            CollectionAssert.AreEqual(new[] { 0x7533, 0x8bf7, 0x4eba }, expectedLines[2].EnumerateRunes().Take(3).Select(rune => rune.Value).ToArray());
            CollectionAssert.AreEqual(new[] { 0x8ffd, 0x8e2a }, expectedLines[4].EnumerateRunes().Take(2).Select(rune => rune.Value).ToArray());
        }
        else if (culture is "zh-HK" or "zh-TW")
        {
            CollectionAssert.AreEqual(new[] { 0x50c5, 0x4f9b }, expectedLines[0].EnumerateRunes().Take(2).Select(rune => rune.Value).ToArray());
            CollectionAssert.AreEqual(new[] { 0x7533, 0x8acb, 0x4eba }, expectedLines[2].EnumerateRunes().Take(3).Select(rune => rune.Value).ToArray());
            CollectionAssert.AreEqual(new[] { 0x8ffd, 0x8e64 }, expectedLines[4].EnumerateRunes().Take(2).Select(rune => rune.Value).ToArray());
        }
        else
        {
            CollectionAssert.AreEqual(new[]
            {
                "For Purpose {0} / 100% only / No other use",
                "Recipient: Recipient {1}",
                "Applicant: Applicant {2}",
                "UTC: 2026-10-12 05:23:17 UTC",
                $"Trace ID: {request.TraceId:D}"
            }, expectedLines);
        }
        StringAssert.Contains(expectedLines[0], request.Purpose);
        StringAssert.Contains(expectedLines[1], request.Recipient);
        StringAssert.Contains(expectedLines[2], request.ApplicantName);
        RequireRenderingFont(string.Concat(expectedLines));
        using var source = CreateSource(768, 768, "white");
        var output = _service.Generate(source, request);
        CollectionAssert.AreEqual(RenderExpectedWatermark(expectedLines, request.TraceId), output,
            "The actual PNG must contain the expected localized glyphs, not merely different pixels.");
        using var download = new MemoryStream(output);
        Assert.AreEqual(request.TraceId, _service.Detect(download));
    }

    [TestMethod]
    [DataRow("zh-CN", "en-US")]
    [DataRow("zh-CN", "zh-HK")]
    [DataRow("en-US", "zh-CN")]
    [DataRow("zh-HK", "en-US")]
    [DataRow("zh-HK", "auto")]
    [DataRow("en-GB", "auto")]
    public void SelectedLanguageOverridesOnlyWatermarkLabelsAndPreservesRequestCulture(string uiCulture, string language)
    {
        var originalUiCulture = CultureInfo.GetCultureInfo(uiCulture);
        CultureInfo.CurrentUICulture = originalUiCulture;
        var originalCulture = CultureInfo.CurrentCulture;
        var request = Request with { WatermarkLanguage = language };
        RequireRenderingFont();
        using var source = CreateSource(768, 768, "white");
        var actual = _service.Generate(source, request);
        Assert.AreSame(originalUiCulture, CultureInfo.CurrentUICulture);
        Assert.AreSame(originalCulture, CultureInfo.CurrentCulture);
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language == "auto" ? uiCulture : language);
        var expected = _service.Generate(source, Request);
        CollectionAssert.AreEqual(expected, actual,
            "Explicit language must render exactly like that interface language without translating fields.");
        using var download = new MemoryStream(actual);
        Assert.AreEqual(request.TraceId, _service.Detect(download));
    }

    [TestMethod]
    [DataRow("fr-FR")]
    [DataRow("en")]
    [DataRow("")]
    [DataRow(null)]
    public void UnsupportedWatermarkLanguageFailsWithoutChangingCulture(string? language)
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");
        var originalCulture = CultureInfo.CurrentUICulture;
        using var source = CreateSource(512, 512);
        AssertImageRejected<ArgumentException>(source, Request with { WatermarkLanguage = language! });
        Assert.AreSame(originalCulture, CultureInfo.CurrentUICulture);
        Assert.IsTrue(source.CanRead);
    }

    [TestMethod]
    [DataRow("en-GB")]
    [DataRow("zh-CN")]
    [DataRow("zh-HK")]
    [DataRow("zh-TW")]
    public void MixedLanguageUserFieldsAreRenderedVerbatim(string culture)
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");
        var purpose = CreateLocalizer()["Applicant: {0}", "Review {0}"].Value;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-HK");
        var recipient = CreateLocalizer()["Trace ID: {0}", "Recipient {1}"].Value;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        var request = Request with { Purpose = purpose, Recipient = recipient, ApplicantName = "Ren\u00e9e {2}" };
        var localizer = CreateLocalizer();
        string[] expectedLines =
        [
            localizer["For {0} only / No other use", request.Purpose].Value,
            localizer["Recipient: {0}", request.Recipient].Value,
            localizer["Applicant: {0}", request.ApplicantName].Value,
            localizer["UTC: {0} UTC", "2026-10-12 05:23:17"].Value,
            localizer["Trace ID: {0}", request.TraceId.ToString("D")].Value
        ];
        StringAssert.Contains(expectedLines[0], request.Purpose);
        StringAssert.Contains(expectedLines[1], request.Recipient);
        StringAssert.Contains(expectedLines[2], request.ApplicantName);
        RequireRenderingFont(string.Concat(expectedLines));
        using var source = CreateSource(768, 768, "white");
        var output = _service.Generate(source, request);
        CollectionAssert.AreEqual(RenderExpectedWatermark(expectedLines, request.TraceId), output);
        using var download = new MemoryStream(output);
        Assert.AreEqual(request.TraceId, _service.Detect(download));
    }

    [TestMethod]
    public void PayloadProtocolAndGuidByteLayoutRemainV1()
    {
        var payloadFactory = typeof(QualificationWatermarkService).GetMethod("CreatePayload", BindingFlags.NonPublic | BindingFlags.Static)!;
        var payload = (byte[])payloadFactory.Invoke(null, [Request.TraceId])!;
        Assert.AreEqual(29, payload.Length);
        CollectionAssert.AreEqual("QWM1"u8.ToArray(), payload[..4]);
        Assert.AreEqual((byte)1, payload[4]);
        CollectionAssert.AreEqual(new byte[]
        {
            0xa5, 0xca, 0xd2, 0xeb, 0x7e, 0x7e, 0xc3, 0x4c,
            0xa7, 0x79, 0x3c, 0x44, 0xe2, 0x92, 0x8b, 0xa7
        }, payload[5..21]);
        CollectionAssert.AreEqual(SHA256.HashData(payload[..21])[..8], payload[21..]);
    }

    [TestMethod]
    public void AllVisibleFieldsChangePixelsWithoutChangingHiddenTraceId()
    {
        RequireRenderingFont();
        using var source = CreateSource(768, 768, "white");
        var original = _service.Generate(source, Request);
        var changedRequests = new[]
        {
            Request with { Purpose = "Bank account opening" },
            Request with { Recipient = "Another recipient" },
            Request with { ApplicantName = "Another applicant" },
            Request with { CreatedAt = Request.CreatedAt.AddSeconds(1) }
        };
        foreach (var changed in changedRequests)
        {
            var output = _service.Generate(source, changed);
            Assert.IsFalse(original.AsSpan().SequenceEqual(output));
            using var stream = new MemoryStream(output);
            Assert.AreEqual(Request.TraceId, _service.Detect(stream));
        }
        using var bitmap = SKBitmap.Decode(original);
        var pixels = bitmap.Pixels;
        Assert.IsTrue(pixels.Count(pixel => pixel.Red > pixel.Green + 20) > 1000,
            "The diagonal visible watermark must leave substantial colored text, not just hidden signal.");
    }

    [TestMethod]
    [DataRow(512, 512, "en-GB")]
    [DataRow(1311, 927, "en-GB")]
    [DataRow(512, 512, "zh-CN")]
    [DataRow(1311, 927, "zh-CN")]
    [DataRow(512, 512, "zh-HK")]
    [DataRow(1311, 927, "zh-HK")]
    public void ControllerMaximumLengthFieldsFitWithoutTruncation(int width, int height, string culture)
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        var localizer = CreateLocalizer();
        var fieldGlyphs = string.Concat(
            localizer["For {0} only / No other use"].Value,
            localizer["Recipient: {0}"].Value,
            localizer["Applicant: {0}"].Value,
            localizer["Trace ID: {0}"].Value);
        RequireRenderingFont(fieldGlyphs);
        using var source = CreateSource(width, height, "white");
        var maximum = Request with
        {
            Purpose = new string(localizer["For {0} only / No other use"].Value[0], 120),
            Recipient = new string(localizer["Recipient: {0}"].Value[0], 100),
            ApplicantName = new string(localizer["Applicant: {0}"].Value[0], 100)
        };
        var output = _service.Generate(source, maximum);
        using var downloaded = new MemoryStream(output);
        Assert.AreEqual(maximum.TraceId, _service.Detect(downloaded));
        var changed = _service.Generate(source, maximum with
        {
            Purpose = maximum.Purpose[..^1] + "D",
            Recipient = maximum.Recipient[..^1] + "E",
            ApplicantName = maximum.ApplicantName[..^1] + "F"
        });
        Assert.IsFalse(output.AsSpan().SequenceEqual(changed), "Field suffixes must not be silently truncated.");
    }

    [TestMethod]
    public void DifferentTraceIdsAreRecoveredSeparately()
    {
        RequireRenderingFont();
        using var source = CreateSource(512, 512);
        var secondId = Guid.Parse("8872370f-8e10-4902-b018-e8549ce41b79");
        var first = _service.Generate(source, Request);
        var second = _service.Generate(source, Request with { TraceId = secondId });
        Assert.IsFalse(first.AsSpan().SequenceEqual(second));
        using var firstStream = new MemoryStream(first);
        using var secondStream = new MemoryStream(second);
        Assert.AreEqual(Request.TraceId, _service.Detect(firstStream));
        Assert.AreEqual(secondId, _service.Detect(secondStream));
    }

    [TestMethod]
    public void UnmarkedImagesAndRandomNoiseAreRejected()
    {
        foreach (var pattern in new[] { "white", "black", "gradient", "document", "noise" })
        {
            for (var seed = 0; seed < 5; seed++)
            {
                using var source = CreateSource(512 + seed * 8, 512, pattern, seed: seed);
                Assert.IsNull(_service.Detect(source), $"Unmarked {pattern}, seed {seed}");
                using var bitmap = SKBitmap.Decode(source.ToArray());
                using var image = SKImage.FromBitmap(bitmap);
                using var jpeg = image.Encode(SKEncodedImageFormat.Jpeg, 85);
                using var stream = new MemoryStream(jpeg.ToArray());
                Assert.IsNull(_service.Detect(stream), $"Unmarked JPEG {pattern}, seed {seed}");
            }
        }
    }

    [TestMethod]
    public void DamagingTheSignalCannotProduceAnotherValidTraceId()
    {
        RequireRenderingFont();
        using var source = CreateSource(512, 512, "white");
        using var bitmap = SKBitmap.Decode(_service.Generate(source, Request));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        using var damaged = new MemoryStream(png.ToArray());
        Assert.IsNull(_service.Detect(damaged));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(39)]
    [DataRow(40)]
    [DataRow(168)]
    public void PayloadMagicVersionAndChecksumRejectAlteredSignal(int bit)
    {
        RequireRenderingFont();
        using var source = CreateSource(512, 512, "white");
        using var bitmap = SKBitmap.Decode(_service.Generate(source, Request));
        FlipSignalBits(bitmap, sample => sample % 232 == bit);
        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        using var corrupted = new MemoryStream(png.ToArray());
        Assert.IsNull(_service.Detect(corrupted));
    }

    [TestMethod]
    public void MajorityRecoversIdDespiteTwoDamagedPayloadRepetitions()
    {
        RequireRenderingFont();
        using var source = CreateSource(512, 512, "white");
        using var bitmap = SKBitmap.Decode(_service.Generate(source, Request));
        FlipSignalBits(bitmap, sample => sample / 232 < 2);
        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        using var damaged = new MemoryStream(png.ToArray());
        Assert.AreEqual(Request.TraceId, _service.Detect(damaged));
    }

    [TestMethod]
    [DataRow("en-GB")]
    [DataRow("zh-CN")]
    [DataRow("zh-HK")]
    [DataRow("zh-TW")]
    public void DefaultFontResolutionRendersCurrentLanguage(string culture)
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        var localizer = CreateLocalizer();
        RequireRenderingFont(string.Concat(
            localizer["For {0} only / No other use", Request.Purpose].Value,
            localizer["Recipient: {0}", Request.Recipient].Value,
            localizer["Applicant: {0}", Request.ApplicantName].Value,
            localizer["UTC: {0} UTC", "2026-10-12 05:23:17"].Value,
            localizer["Trace ID: {0}", Request.TraceId.ToString("D")].Value));
        using var source = CreateSource(512, 512);
        var output = CreateService(null).Generate(source, Request);
        using var downloaded = new MemoryStream(output);
        Assert.AreEqual(Request.TraceId, _service.Detect(downloaded));
    }

    [TestMethod]
    public void TransparencyIsFlattenedOntoWhiteAndPngHasNoSourceMetadata()
    {
        RequireRenderingFont();
        using var bitmap = new SKBitmap(768, 768, SKColorType.Rgba8888, SKAlphaType.Premul);
        bitmap.Erase(SKColors.Transparent);
        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        using var source = new MemoryStream(AddPngText(png.ToArray(), "private-source-metadata"));
        var output = _service.Generate(source, Request);
        using var decoded = SKBitmap.Decode(output);
        Assert.IsTrue(decoded.Pixels.All(pixel => pixel.Alpha == 255));
        Assert.IsTrue(decoded.Pixels.Count(pixel => pixel is { Red: >= 245, Green: >= 245, Blue: >= 245 }) > 100_000);
        CollectionAssert.DoesNotContain(PngChunkTypes(output), "tEXt");
        CollectionAssert.DoesNotContain(PngChunkTypes(output), "eXIf");
        Assert.IsFalse(System.Text.Encoding.UTF8.GetString(output).Contains("private-source-metadata"));
        using var downloaded = new MemoryStream(output);
        Assert.AreEqual(Request.TraceId, _service.Detect(downloaded));
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    [DataRow(8)]
    public void AllExifOrientationsAreAppliedAndRemoved(int orientation)
    {
        RequireRenderingFont();
        using var bitmap = new SKBitmap(768, 512);
        using (var canvas = new SKCanvas(bitmap))
        {
            using var paint = new SKPaint();
            paint.Color = SKColors.Red;
            canvas.DrawRect(0, 0, 384, 256, paint);
            paint.Color = SKColors.Lime;
            canvas.DrawRect(384, 0, 384, 256, paint);
            paint.Color = SKColors.Blue;
            canvas.DrawRect(0, 256, 384, 256, paint);
            paint.Color = SKColors.Yellow;
            canvas.DrawRect(384, 256, 384, 256, paint);
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var jpeg = image.Encode(SKEncodedImageFormat.Jpeg, 100);
        using var source = new MemoryStream(AddExifOrientation(jpeg.ToArray(), orientation));
        var output = _service.Generate(source, Request);
        using var decoded = SKBitmap.Decode(output);
        Assert.AreEqual(orientation >= 5 ? 512 : 768, decoded.Width);
        Assert.AreEqual(orientation >= 5 ? 768 : 512, decoded.Height);
        int[][] quadrants =
        [
            [0, 1, 2, 3], [1, 0, 3, 2], [3, 2, 1, 0], [2, 3, 0, 1],
            [0, 2, 1, 3], [2, 0, 3, 1], [3, 1, 2, 0], [1, 3, 0, 2]
        ];
        var colors = new[] { SKColors.Red, SKColors.Lime, SKColors.Blue, SKColors.Yellow };
        for (var quadrant = 0; quadrant < 4; quadrant++)
        {
            var x = decoded.Width * (quadrant % 2 * 2 + 1) / 4;
            var y = decoded.Height * (quadrant / 2 * 2 + 1) / 4;
            var actual = decoded.GetPixel(x, y);
            var expected = colors[quadrants[orientation - 1][quadrant]];
            Assert.IsTrue(Math.Abs(actual.Red - expected.Red) < 90 &&
                          Math.Abs(actual.Green - expected.Green) < 90 &&
                          Math.Abs(actual.Blue - expected.Blue) < 90,
                $"Orientation {orientation}, quadrant {quadrant}: expected {expected}, got {actual}");
        }
        using var pngStream = new MemoryStream(output);
        using var codec = SKCodec.Create(pngStream);
        Assert.AreEqual(SKEncodedOrigin.TopLeft, codec.EncodedOrigin);
        CollectionAssert.DoesNotContain(PngChunkTypes(output), "eXIf");
        pngStream.Position = 0;
        Assert.AreEqual(Request.TraceId, _service.Detect(pngStream));
    }

    [TestMethod]
    public void LongSideIsBoundedAndAspectRatioIsPreserved()
    {
        RequireRenderingFont();
        using var source = CreateSource(4096, 2048, "white");
        _service.ValidateSource(source);
        var output = _service.Generate(source, Request);
        using var bitmap = SKBitmap.Decode(output);
        Assert.AreEqual(2048, bitmap.Width);
        Assert.AreEqual(1024, bitmap.Height);
        using var stream = new MemoryStream(output);
        Assert.AreEqual(Request.TraceId, _service.Detect(stream));
    }

    [TestMethod]
    [DataRow(511, 512)]
    [DataRow(512, 511)]
    [DataRow(4096, 512)]
    [DataRow(5001, 5000)]
    public void DimensionLimitsAreCheckedBeforeDecode(int width, int height)
    {
        using var source = new MemoryStream(PngHeader(width, height));
        var exception = AssertImageRejected<InvalidDataException>(source);
        StringAssert.Contains(exception.Message, width == 4096 ? "maximum output long side of 2048" : "25 million decoded pixels");
        source.Position = 0;
        Assert.IsNull(_service.Detect(source));
    }

    [TestMethod]
    public void MissingPngEndAndJpegEndMarkersAreRejected()
    {
        RequireRenderingFont();
        using var source = CreateSource(512, 512);
        var marked = _service.Generate(source, Request);
        using var truncatedPng = new MemoryStream(marked[..^12]);
        AssertImageRejected<InvalidDataException>(truncatedPng);
        Assert.IsNull(_service.Detect(truncatedPng));
        using var bitmap = SKBitmap.Decode(marked);
        using var image = SKImage.FromBitmap(bitmap);
        using var jpeg = image.Encode(SKEncodedImageFormat.Jpeg, 85);
        using var truncatedJpeg = new MemoryStream(jpeg.ToArray()[..^2]);
        AssertImageRejected<InvalidDataException>(truncatedJpeg);
        Assert.IsNull(_service.Detect(truncatedJpeg));
    }

    [TestMethod]
    public void ProgressiveJpegWithRefinementScansIsSupported()
    {
        RequireRenderingFont();
        using var source = new MemoryStream(CreateProgressiveJpeg());
        _service.ValidateSource(source);
        using var decoded = SKBitmap.Decode(source.ToArray());
        Assert.AreEqual(512, decoded.Width);
        Assert.AreEqual(new SKColor(128, 128, 128), decoded.GetPixel(256, 256));
        Assert.IsNull(_service.Detect(source));
        var output = _service.Generate(source, Request);
        using var png = new MemoryStream(output);
        Assert.AreEqual(Request.TraceId, _service.Detect(png));
        var truncated = source.ToArray()[..^22];
        using var badBody = new MemoryStream(truncated.Concat<byte>([255, 217]).ToArray());
        AssertImageRejected<InvalidDataException>(badBody);
    }

    [TestMethod]
    public void CompleteContainersWithMalformedOrTruncatedBodiesAreRejected()
    {
        using var valid = CreateSource(512, 512);
        var original = valid.ToArray();
        foreach (var corruptPayload in new[] { "invalid-zlib-data"u8.ToArray(), new byte[] { 120, 156, 1, 0 } })
        {
            using var container = new MemoryStream();
            container.Write(original.AsSpan(0, 33));
            WritePngChunk(container, "IDAT", corruptPayload);
            WritePngChunk(container, "IEND", []);
            container.Position = 0;
            AssertImageRejected<InvalidDataException>(container);
            Assert.IsNull(_service.Detect(container));
        }
        using var bitmap = SKBitmap.Decode(original);
        using var image = SKImage.FromBitmap(bitmap);
        using var jpeg = image.Encode(SKEncodedImageFormat.Jpeg, 85);
        var bytes = jpeg.ToArray();
        using var truncated = new MemoryStream();
        truncated.Write(bytes.AsSpan(0, bytes.Length / 2));
        truncated.Write([255, 217]);
        truncated.Position = 0;
        AssertImageRejected<InvalidDataException>(truncated);
        Assert.IsNull(_service.Detect(truncated));
    }

    [TestMethod]
    public void UnsupportedEmptyMalformedAndTruncatedFilesAreRejected()
    {
        using var valid = CreateSource(512, 512);
        byte[][] files =
        [
            Array.Empty<byte>(),
            "not an image"u8.ToArray(),
            "GIF89a"u8.ToArray(),
            [255, 216, 255, 217],
            [255, 216, 255, 196, 0, 255, 217],
            valid.ToArray()[..60],
            PngHeader(512, 512)
        ];
        foreach (var file in files)
        {
            using var stream = new MemoryStream(file);
            AssertImageRejected<InvalidDataException>(stream);
            AssertImageRejected<InvalidDataException>(stream, Request);
            Assert.IsNull(_service.Detect(stream));
        }
        using var bitmap = new SKBitmap(512, 512);
        using var image = SKImage.FromBitmap(bitmap);
        using var webp = image.Encode(SKEncodedImageFormat.Webp, 90);
        using var unsupported = new MemoryStream(webp.ToArray());
        AssertImageRejected<InvalidDataException>(unsupported);
        Assert.IsNull(_service.Detect(unsupported));
    }

    [TestMethod]
    public void OversizedSeekableAndNonSeekableStreamsAreBounded()
    {
        using var oversized = new MemoryStream(new byte[QualificationWatermarkService.MaximumEncodedBytes + 1]);
        AssertImageRejected<InvalidDataException>(oversized);
        Assert.IsNull(_service.Detect(oversized));
        using var nonSeekable = new GeneratedNonSeekableStream(QualificationWatermarkService.MaximumEncodedBytes + 81920);
        AssertImageRejected<InvalidDataException>(nonSeekable);
        Assert.AreEqual(QualificationWatermarkService.MaximumEncodedBytes + 1, nonSeekable.BytesRead);
        Assert.IsFalse(nonSeekable.Disposed, "Caller owns source streams.");
    }

    [TestMethod]
    public void StreamsAreNotClosedAndSeekablePositionIsRestored()
    {
        RequireRenderingFont();
        using var source = CreateSource(512, 512);
        source.Position = 10;
        _service.ValidateSource(source);
        Assert.AreEqual(10L, source.Position);
        var output = _service.Generate(source, Request);
        Assert.AreEqual(10L, source.Position);
        Assert.IsTrue(source.CanRead);
        using var download = new MemoryStream(output);
        download.Position = 15;
        Assert.AreEqual(Request.TraceId, _service.Detect(download));
        Assert.AreEqual(15L, download.Position);
        Assert.IsTrue(download.CanRead);
    }

    [TestMethod]
    [DataRow("en-GB")]
    [DataRow("zh-CN")]
    [DataRow("zh-HK")]
    public void MissingConfiguredFontFailsClearly(string culture)
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        using var source = CreateSource(512, 512);
        var service = CreateService(Path.Combine(Environment.CurrentDirectory, $"missing-font-{Guid.NewGuid():N}.ttf"));
        var exception = AssertImageRejected<InvalidOperationException>(source, Request, service);
        StringAssert.Contains(exception.Message, "font", StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    [DataRow("en-GB", "Purpose")]
    [DataRow("zh-CN", "Recipient")]
    [DataRow("zh-HK", "ApplicantName")]
    [DataRow("zh-TW", "Purpose")]
    public void FontMissingAnyUserFieldGlyphFailsClosed(string culture, string field)
    {
        RequireRenderingFont();
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        var unsupported = char.ConvertFromUtf32(0x10ffff);
        var request = field switch
        {
            "Purpose" => Request with { Purpose = unsupported },
            "Recipient" => Request with { Recipient = unsupported },
            _ => Request with { ApplicantName = unsupported }
        };
        using var source = CreateSource(512, 512);
        var exception = AssertImageRejected<InvalidOperationException>(source, request);
        StringAssert.Contains(exception.Message, "font covering all watermark characters");
    }

    [TestMethod]
    public void EnglishWatermarkDoesNotRequireCjkGlyphs()
    {
        string[] expectedLines =
        [
            "For Qualification review only / No other use",
            "Recipient: Test recipient",
            "Applicant: Test applicant",
            "UTC: 2026-10-12 05:23:17 UTC",
            $"Trace ID: {Request.TraceId:D}"
        ];
        var visibleText = string.Concat(expectedLines);
        Assert.IsTrue(visibleText.All(character => character is >= ' ' and <= '~'));
        var service = CreateService(null);
        var resolver = typeof(QualificationWatermarkService)
            .GetMethod("ResolveTypeface", BindingFlags.NonPublic | BindingFlags.Instance)!;
        using var typeface = (SKTypeface)resolver.Invoke(service, [visibleText])!;
        using var font = new SKFont(typeface);
        Assert.IsTrue(font.ContainsGlyphs(visibleText),
            "The system font must cover English text; CJK glyph coverage is not a prerequisite.");
        using var source = CreateSource(768, 768, "white");
        var output = service.Generate(source, Request);
        CollectionAssert.AreEqual(RenderExpectedWatermark(expectedLines, Request.TraceId, typeface), output,
            "Every visible glyph must be English even when the available system font also supports CJK.");
        using var download = new MemoryStream(output);
        Assert.AreEqual(Request.TraceId, service.Detect(download));
    }

    [TestMethod]
    public void EmptyTraceAndUnboundedOrControlCharacterTextAreRejected()
    {
        using var source = CreateSource(512, 512);
        foreach (var invalid in new[]
                 {
                     Request with { TraceId = Guid.Empty },
                     Request with { Purpose = "" },
                     Request with { Purpose = new string('A', 201) },
                     Request with { Recipient = "line\nbreak" },
                     Request with { ApplicantName = "\u202ehidden" }
                 })
        {
            AssertImageRejected<ArgumentException>(source, invalid);
        }
    }

    [TestMethod]
    public void UnspecifiedTimestampIsInterpretedAsUtc()
    {
        RequireRenderingFont();
        using var source = CreateSource(512, 512);
        CollectionAssert.AreEqual(_service.Generate(source, Request), _service.Generate(source,
            Request with { CreatedAt = DateTime.SpecifyKind(Request.CreatedAt, DateTimeKind.Unspecified) }));
        CollectionAssert.AreEqual(_service.Generate(source, Request), _service.Generate(source,
            Request with { CreatedAt = Request.CreatedAt.ToLocalTime() }));
    }

    private TException AssertImageRejected<TException>(Stream source,
        QualificationWatermarkRequest? request = null, QualificationWatermarkService? service = null)
        where TException : Exception
    {
        var target = service ?? _service;
        try
        {
            if (request == null)
            {
                target.ValidateSource(source);
            }
            else
            {
                target.Generate(source, request);
            }
        }
        catch (Exception exception)
        {
            Assert.AreEqual(typeof(TException), exception.GetType(),
                "The image operation must throw the exact expected exception type.");
            return (TException)exception;
        }
        throw new AssertFailedException($"Expected {typeof(TException).Name}, but the image operation succeeded.");
    }

    private QualificationWatermarkService CreateService(string? fontPath) => new(
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Qualifications:WatermarkFontPath"] = fontPath
        }).Build(), CreateLocalizer());

    private IStringLocalizer<QualificationWatermarkService> CreateLocalizer() =>
        _localizationServices.GetRequiredService<IStringLocalizer<QualificationWatermarkService>>();

    private void RequireRenderingFont(string? text = null)
    {
        using var typeface = _fontPath == null
            ? SKFontManager.Default.MatchFamily("sans-serif")
            : SKTypeface.FromFile(_fontPath);
        Assert.IsNotNull(typeface,
            "Install a font covering the watermark or set QUALIFICATION_TEST_FONT_PATH to run rendering tests.");
        using var font = new SKFont(typeface);
        Assert.IsTrue(font.ContainsGlyphs(text ??
            "For qualification review only / No other use Recipient: Applicant: UTC Trace ID 0123456789"),
            "The rendering-test font must cover every character in the visible watermark.");
    }

    private byte[] RenderExpectedWatermark(string[] lines, Guid traceId)
    {
        using var typeface = _fontPath == null
            ? SKFontManager.Default.MatchFamily("sans-serif")
            : SKTypeface.FromFile(_fontPath);
        Assert.IsNotNull(typeface);
        return RenderExpectedWatermark(lines, traceId, typeface);
    }

    private static byte[] RenderExpectedWatermark(string[] lines, Guid traceId, SKTypeface typeface)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(768, 768, SKColorType.Rgba8888, SKAlphaType.Opaque));
        bitmap.Erase(SKColors.White);
        var draw = typeof(QualificationWatermarkService)
            .GetMethod("DrawVisibleWatermark", BindingFlags.NonPublic | BindingFlags.Static)!;
        var payloadFactory = typeof(QualificationWatermarkService)
            .GetMethod("CreatePayload", BindingFlags.NonPublic | BindingFlags.Static)!;
        var embed = typeof(QualificationWatermarkService)
            .GetMethod("Embed", BindingFlags.NonPublic | BindingFlags.Static)!;
        draw.Invoke(null, [bitmap, typeface, lines]);
        embed.Invoke(null, [bitmap, payloadFactory.Invoke(null, [traceId])!]);
        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        return png.ToArray();
    }

    private static MemoryStream CreateSource(int width, int height, string pattern = "gradient", int seed = 123)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque);
        if (pattern is "white" or "black")
        {
            bitmap.Erase(pattern == "white" ? SKColors.White : SKColors.Black);
        }
        else
        {
            var random = new Random(seed);
            var pixels = new SKColor[width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    pixels[y * width + x] = pattern == "noise"
                        ? new SKColor((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256))
                        : new SKColor((byte)(60 + x * 150 / width), (byte)(50 + y * 170 / height), 170);
                }
            }
            bitmap.Pixels = pixels;
            if (pattern == "document")
            {
                using var canvas = new SKCanvas(bitmap);
                canvas.Clear(SKColors.White);
                using var paint = new SKPaint();
                paint.Color = SKColors.Black;
                for (var y = 30; y < height - 20; y += 27)
                {
                    for (var x = 24; x < width - 20; x += 16)
                    {
                        canvas.DrawRect(x, y, 9, 12, paint);
                    }
                }
            }
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        return new MemoryStream(png.ToArray());
    }

    private static byte[] CreateProgressiveJpeg()
    {
        // Synthetic constant-gray 512×512 JPEG: initial DC/AC and successive-approximation scans.
        using var stream = new MemoryStream();
        stream.Write([255, 216]);
        void Segment(int marker, byte[] content)
        {
            stream.Write([255, (byte)marker, (byte)((content.Length + 2) >> 8), (byte)(content.Length + 2)]);
            stream.Write(content);
        }
        Segment(219, [0, .. Enumerable.Repeat((byte)1, 64)]);
        Segment(194, [8, 2, 0, 2, 0, 1, 1, 17, 0]);
        var dc = new byte[18];
        dc[1] = 1;
        Segment(196, dc);
        var ac = new byte[18];
        ac[0] = 16;
        ac[1] = 1;
        ac[^1] = 96;
        Segment(196, ac);
        Segment(218, [1, 1, 0, 0, 0, 1]);
        stream.Write(new byte[512]);
        Segment(218, [1, 1, 0, 1, 63, 1]);
        stream.Write(new byte[56]);
        Segment(218, [1, 1, 0, 0, 0, 16]);
        stream.Write(new byte[512]);
        Segment(218, [1, 1, 0, 1, 63, 16]);
        stream.Write(new byte[56]);
        stream.Write([255, 217]);
        return stream.ToArray();
    }

    private static void FlipSignalBits(SKBitmap bitmap, Func<int, bool> flipSample)
    {
        const int payloadBits = 232;
        var blocksAcross = bitmap.Width / 8;
        var blockCount = blocksAcross * (bitmap.Height / 8);
        var repetitions = Math.Min(31, blockCount / payloadBits);
        repetitions -= repetitions % 2 == 0 ? 1 : 0;
        var samples = repetitions * payloadBits;
        var basis = new double[64];
        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                basis[y * 8 + x] = .25 * (
                    Math.Cos((2 * x + 1) * Math.PI / 16) * Math.Cos((2 * y + 1) * 2 * Math.PI / 16) -
                    Math.Cos((2 * x + 1) * 2 * Math.PI / 16) * Math.Cos((2 * y + 1) * Math.PI / 16));
            }
        }
        for (var sample = 0; sample < samples; sample++)
        {
            if (!flipSample(sample))
            {
                continue;
            }
            var block = (int)((long)sample * blockCount / samples);
            var left = block % blocksAcross * 8;
            var top = block / blocksAcross * 8;
            double difference = 0;
            for (var y = 0; y < 8; y++)
            {
                for (var x = 0; x < 8; x++)
                {
                    var pixel = bitmap.GetPixel(left + x, top + y);
                    difference += (.299 * pixel.Red + .587 * pixel.Green + .114 * pixel.Blue) * basis[y * 8 + x];
                }
            }
            for (var y = 0; y < 8; y++)
            {
                for (var x = 0; x < 8; x++)
                {
                    var pixel = bitmap.GetPixel(left + x, top + y);
                    var change = -difference * basis[y * 8 + x];
                    bitmap.SetPixel(left + x, top + y, new SKColor(
                        (byte)Math.Clamp((int)Math.Round(pixel.Red + change), 0, 255),
                        (byte)Math.Clamp((int)Math.Round(pixel.Green + change), 0, 255),
                        (byte)Math.Clamp((int)Math.Round(pixel.Blue + change), 0, 255)));
                }
            }
        }
    }

    private static byte[] AddExifOrientation(byte[] jpeg, int orientation)
    {
        byte[] exif =
        [
            69, 120, 105, 102, 0, 0,
            73, 73, 42, 0, 8, 0, 0, 0,
            1, 0,
            18, 1, 3, 0, 1, 0, 0, 0, (byte)orientation, 0, 0, 0,
            0, 0, 0, 0
        ];
        using var stream = new MemoryStream();
        stream.Write(jpeg.AsSpan(0, 2));
        stream.Write([255, 225, (byte)((exif.Length + 2) >> 8), (byte)(exif.Length + 2)]);
        stream.Write(exif);
        stream.Write(jpeg.AsSpan(2));
        return stream.ToArray();
    }

    private static byte[] PngHeader(int width, int height)
    {
        using var stream = new MemoryStream();
        stream.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8;
        header[9] = 6;
        WritePngChunk(stream, "IHDR", header);
        WritePngChunk(stream, "IDAT", []);
        WritePngChunk(stream, "IEND", []);
        return stream.ToArray();
    }

    private static byte[] AddPngText(byte[] png, string text)
    {
        using var stream = new MemoryStream();
        stream.Write(png.AsSpan(0, 33));
        WritePngChunk(stream, "tEXt", System.Text.Encoding.UTF8.GetBytes("Comment\0" + text));
        stream.Write(png.AsSpan(33));
        return stream.ToArray();
    }

    private static void WritePngChunk(Stream stream, string type, byte[] data)
    {
        Span<byte> number = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(number, data.Length);
        stream.Write(number);
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        stream.Write(typeBytes);
        stream.Write(data);
        uint crc = 0xffffffff;
        foreach (var value in typeBytes.Concat(data))
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc >> 1) ^ ((crc & 1) == 1 ? 0xedb88320 : 0);
            }
        }
        BinaryPrimitives.WriteUInt32BigEndian(number, ~crc);
        stream.Write(number);
    }

    private static string[] PngChunkTypes(byte[] png)
    {
        var types = new List<string>();
        for (var offset = 8; offset + 12 <= png.Length;)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(offset, 4));
            types.Add(System.Text.Encoding.ASCII.GetString(png, offset + 4, 4));
            offset += length + 12;
        }
        return types.ToArray();
    }
}
