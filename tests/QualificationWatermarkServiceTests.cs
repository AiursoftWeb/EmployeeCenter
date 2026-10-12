using System.Buffers.Binary;
using Aiursoft.EmployeeCenter.Services;
using Microsoft.Extensions.Configuration;
using SkiaSharp;

namespace Aiursoft.EmployeeCenter.Tests;

[TestClass]
public class QualificationWatermarkServiceTests
{
    private static readonly QualificationWatermarkRequest Request = new(
        "资质审核", "测试接收单位", "测试申请人", new DateTime(2026, 10, 12, 5, 23, 17, DateTimeKind.Utc),
        Guid.Parse("ebd2caa5-7e7e-4cc3-a779-3c44e2928ba7"));
    private QualificationWatermarkService _service = null!;
    private string? _fontPath;

    [TestInitialize]
    public void Initialize()
    {
        _fontPath = new[]
        {
            Environment.GetEnvironmentVariable("QUALIFICATION_TEST_FONT_PATH"),
            "/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc",
            "/usr/share/fonts/truetype/noto/NotoSansCJK-Regular.ttc",
            "/usr/share/fonts/truetype/droid/DroidSansFallbackFull.ttf"
        }.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));
        _service = CreateService(_fontPath);
    }

    [TestMethod]
    public void PngContainsBlindlyRecoverableTraceIdAndIsDeterministic()
    {
        RequireCjkFont();
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
        RequireCjkFont();
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
        RequireCjkFont();
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
    public void AllVisibleFieldsChangePixelsWithoutChangingHiddenTraceId()
    {
        RequireCjkFont();
        using var source = CreateSource(768, 768, "white");
        var original = _service.Generate(source, Request);
        var changedRequests = new[]
        {
            Request with { Purpose = "银行开户" },
            Request with { Recipient = "另一接收单位" },
            Request with { ApplicantName = "另一申请人" },
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
    [DataRow(512, 512)]
    [DataRow(1311, 927)]
    public void ControllerMaximumLengthFieldsFitWithoutTruncation(int width, int height)
    {
        RequireCjkFont();
        using var source = CreateSource(width, height, "white");
        var maximum = Request with
        {
            Purpose = new string('审', 120),
            Recipient = new string('单', 100),
            ApplicantName = new string('人', 100)
        };
        var output = _service.Generate(source, maximum);
        using var downloaded = new MemoryStream(output);
        Assert.AreEqual(maximum.TraceId, _service.Detect(downloaded));
        var changed = _service.Generate(source, maximum with
        {
            Purpose = maximum.Purpose[..^1] + "核",
            Recipient = maximum.Recipient[..^1] + "位",
            ApplicantName = maximum.ApplicantName[..^1] + "员"
        });
        Assert.IsFalse(output.AsSpan().SequenceEqual(changed), "Field suffixes must not be silently truncated.");
    }

    [TestMethod]
    public void DifferentTraceIdsAreRecoveredSeparately()
    {
        RequireCjkFont();
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
        RequireCjkFont();
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
        RequireCjkFont();
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
        RequireCjkFont();
        using var source = CreateSource(512, 512, "white");
        using var bitmap = SKBitmap.Decode(_service.Generate(source, Request));
        FlipSignalBits(bitmap, sample => sample / 232 < 2);
        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        using var damaged = new MemoryStream(png.ToArray());
        Assert.AreEqual(Request.TraceId, _service.Detect(damaged));
    }

    [TestMethod]
    public void DefaultFontResolutionRendersReadableChinese()
    {
        RequireCjkFont();
        using var source = CreateSource(512, 512);
        var output = CreateService(null).Generate(source, Request);
        using var downloaded = new MemoryStream(output);
        Assert.AreEqual(Request.TraceId, _service.Detect(downloaded));
    }

    [TestMethod]
    public void TransparencyIsFlattenedOntoWhiteAndPngHasNoSourceMetadata()
    {
        RequireCjkFont();
        using var bitmap = new SKBitmap(768, 768, SKColorType.Rgba8888, SKAlphaType.Premul);
        bitmap.Erase(SKColors.Transparent);
        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        using var source = new MemoryStream(AddPngText(png.ToArray(), "private-source-metadata"));
        var output = _service.Generate(source, Request);
        using var decoded = SKBitmap.Decode(output);
        Assert.IsTrue(decoded.Pixels.All(pixel => pixel.Alpha == 255));
        Assert.IsTrue(decoded.Pixels.Count(pixel => pixel.Red >= 245 && pixel.Green >= 245 && pixel.Blue >= 245) > 100_000);
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
        RequireCjkFont();
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
        RequireCjkFont();
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
        var exception = Assert.ThrowsExactly<InvalidDataException>(() => _service.ValidateSource(source));
        StringAssert.Contains(exception.Message, width == 4096 ? "maximum output long side of 2048" : "25 million decoded pixels");
        source.Position = 0;
        Assert.IsNull(_service.Detect(source));
    }

    [TestMethod]
    public void MissingPngEndAndJpegEndMarkersAreRejected()
    {
        RequireCjkFont();
        using var source = CreateSource(512, 512);
        var marked = _service.Generate(source, Request);
        using var truncatedPng = new MemoryStream(marked[..^12]);
        Assert.ThrowsExactly<InvalidDataException>(() => _service.ValidateSource(truncatedPng));
        Assert.IsNull(_service.Detect(truncatedPng));
        using var bitmap = SKBitmap.Decode(marked);
        using var image = SKImage.FromBitmap(bitmap);
        using var jpeg = image.Encode(SKEncodedImageFormat.Jpeg, 85);
        using var truncatedJpeg = new MemoryStream(jpeg.ToArray()[..^2]);
        Assert.ThrowsExactly<InvalidDataException>(() => _service.ValidateSource(truncatedJpeg));
        Assert.IsNull(_service.Detect(truncatedJpeg));
    }

    [TestMethod]
    public void ProgressiveJpegWithRefinementScansIsSupported()
    {
        RequireCjkFont();
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
        using var badBody = new MemoryStream(truncated.Concat(new byte[] { 255, 217 }).ToArray());
        Assert.ThrowsExactly<InvalidDataException>(() => _service.ValidateSource(badBody));
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
            Assert.ThrowsExactly<InvalidDataException>(() => _service.ValidateSource(container));
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
        Assert.ThrowsExactly<InvalidDataException>(() => _service.ValidateSource(truncated));
        Assert.IsNull(_service.Detect(truncated));
    }

    [TestMethod]
    public void UnsupportedEmptyMalformedAndTruncatedFilesAreRejected()
    {
        using var valid = CreateSource(512, 512);
        var files = new[]
        {
            Array.Empty<byte>(),
            "not an image"u8.ToArray(),
            "GIF89a"u8.ToArray(),
            new byte[] { 255, 216, 255, 217 },
            new byte[] { 255, 216, 255, 196, 0, 255, 217 },
            valid.ToArray()[..60],
            PngHeader(512, 512)
        };
        foreach (var file in files)
        {
            using var stream = new MemoryStream(file);
            Assert.ThrowsExactly<InvalidDataException>(() => _service.ValidateSource(stream));
            Assert.ThrowsExactly<InvalidDataException>(() => _service.Generate(stream, Request));
            Assert.IsNull(_service.Detect(stream));
        }
        using var bitmap = new SKBitmap(512, 512);
        using var image = SKImage.FromBitmap(bitmap);
        using var webp = image.Encode(SKEncodedImageFormat.Webp, 90);
        using var unsupported = new MemoryStream(webp.ToArray());
        Assert.ThrowsExactly<InvalidDataException>(() => _service.ValidateSource(unsupported));
        Assert.IsNull(_service.Detect(unsupported));
    }

    [TestMethod]
    public void OversizedSeekableAndNonSeekableStreamsAreBounded()
    {
        using var oversized = new MemoryStream(new byte[QualificationWatermarkService.MaximumEncodedBytes + 1]);
        Assert.ThrowsExactly<InvalidDataException>(() => _service.ValidateSource(oversized));
        Assert.IsNull(_service.Detect(oversized));
        using var nonSeekable = new GeneratedNonSeekableStream(QualificationWatermarkService.MaximumEncodedBytes + 81920);
        Assert.ThrowsExactly<InvalidDataException>(() => _service.ValidateSource(nonSeekable));
        Assert.AreEqual(QualificationWatermarkService.MaximumEncodedBytes + 1, nonSeekable.BytesRead);
        Assert.IsFalse(nonSeekable.Disposed, "Caller owns source streams.");
    }

    [TestMethod]
    public void StreamsAreNotClosedAndSeekablePositionIsRestored()
    {
        RequireCjkFont();
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
    public void MissingConfiguredFontFailsClearly()
    {
        using var source = CreateSource(512, 512);
        var service = CreateService(Path.Combine(Environment.CurrentDirectory, $"missing-font-{Guid.NewGuid():N}.ttf"));
        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => service.Generate(source, Request));
        StringAssert.Contains(exception.Message, "font", StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void EmptyTraceAndUnboundedOrControlCharacterTextAreRejected()
    {
        using var source = CreateSource(512, 512);
        foreach (var invalid in new[]
                 {
                     Request with { TraceId = Guid.Empty },
                     Request with { Purpose = "" },
                     Request with { Purpose = new string('中', 201) },
                     Request with { Recipient = "line\nbreak" },
                     Request with { ApplicantName = "\u202ehidden" }
                 })
        {
            Assert.ThrowsExactly<ArgumentException>(() => _service.Generate(source, invalid));
        }
    }

    [TestMethod]
    public void UnspecifiedTimestampIsInterpretedAsUtc()
    {
        RequireCjkFont();
        using var source = CreateSource(512, 512);
        CollectionAssert.AreEqual(_service.Generate(source, Request), _service.Generate(source,
            Request with { CreatedAt = DateTime.SpecifyKind(Request.CreatedAt, DateTimeKind.Unspecified) }));
        CollectionAssert.AreEqual(_service.Generate(source, Request), _service.Generate(source,
            Request with { CreatedAt = Request.CreatedAt.ToLocalTime() }));
    }

    private static QualificationWatermarkService CreateService(string? fontPath) => new(
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Qualifications:WatermarkFontPath"] = fontPath
        }).Build());

    private void RequireCjkFont()
    {
        using var font = _fontPath == null ? SKFontManager.Default.MatchCharacter('中') : SKTypeface.FromFile(_fontPath);
        using var glyphFont = font == null ? null : new SKFont(font);
        if (glyphFont == null || !glyphFont.ContainsGlyphs("仅供资质审核使用禁止他用接收方测试单位申请人追踪银行开户另一"))
        {
            Assert.Inconclusive("Install Noto Sans CJK or set QUALIFICATION_TEST_FONT_PATH to run rendering tests.");
        }
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
                using var paint = new SKPaint { Color = SKColors.Black };
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
        Segment(219, new byte[] { 0 }.Concat(Enumerable.Repeat((byte)1, 64)).ToArray());
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

    private sealed class GeneratedNonSeekableStream(int length) : Stream
    {
        public int BytesRead { get; private set; }
        public bool Disposed { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = Math.Min(count, length - BytesRead);
            buffer.AsSpan(offset, read).Clear();
            BytesRead += read;
            return read;
        }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
