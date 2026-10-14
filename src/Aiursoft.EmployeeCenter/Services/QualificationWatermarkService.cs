using System.Buffers;
using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Aiursoft.Scanner.Abstractions;
using Microsoft.Extensions.Localization;
using SkiaSharp;

namespace Aiursoft.EmployeeCenter.Services;

/// <summary>
/// V1 accepts PNG and 8-bit Huffman-coded baseline/progressive JPEG up to 20 MiB
/// and 25 million decoded pixels (JPEG: up to 64 scans and 4M scan-block visits). Both sides must be
/// at least 512 pixels, including after reducing the long side to at most 2048 pixels.
/// The opaque, auto-oriented PNG contains no source metadata. Its repeated DCT payload
/// contains only a trace ID and format/integrity bytes, not applicant or recipient data.
/// Blind detection supports PNG and same-size JPEG recompression at quality 85 or above;
/// cropping, resizing, printing and adversarial removal are not supported. The checksum
/// prevents accidental matches, but is not a cryptographic proof of authenticity.
/// </summary>
public sealed class QualificationWatermarkService(
    IConfiguration configuration,
    IStringLocalizer<QualificationWatermarkService> localizer) : ITransientDependency
{
    public const int MinimumDimension = 512;
    public const int MaximumOutputDimension = 2048;
    public const long MaximumDecodedPixels = 25_000_000;
    public const int MaximumEncodedBytes = 20 * 1024 * 1024;

    private const int BlockSize = 8;
    private const int PayloadBytes = 29;
    private const int PayloadBits = PayloadBytes * 8;
    private const int MaximumRepetitions = 31;
    private const double CoefficientMargin = 64;
    private static readonly double[] BasisDifference = CreateBasisDifference();
    private static readonly uint[] CrcTable = CreateCrcTable();
    private static readonly string[] LinuxFontPaths =
    [
        "/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc",
        "/usr/share/fonts/truetype/noto/NotoSansCJK-Regular.ttc",
        "/usr/share/fonts/truetype/droid/DroidSansFallbackFull.ttf"
    ];

    /// <summary>Validates by decoding, without consuming a seekable caller-owned stream.</summary>
    public void ValidateSource(Stream source)
    {
        using var decoded = Decode(source, detection: false);
    }

    /// <summary>Uses the selected label language, defaulting to the UI culture, without translating user-provided fields.</summary>
    public byte[] Generate(Stream source, QualificationWatermarkRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var purpose = ValidateText(request.Purpose, nameof(request.Purpose), 200);
        var recipient = ValidateText(request.Recipient, nameof(request.Recipient), 200);
        var applicant = ValidateText(request.ApplicantName, nameof(request.ApplicantName), 100);
        if (request.TraceId == Guid.Empty)
        {
            throw new ArgumentException("A non-empty trace ID is required.", nameof(request));
        }

        // Database UTC timestamps may have Unspecified kind; never interpret these as server-local time.
        var utc = request.CreatedAt.Kind == DateTimeKind.Local
            ? request.CreatedAt.ToUniversalTime()
            : DateTime.SpecifyKind(request.CreatedAt, DateTimeKind.Utc);
        var lines = LocalizedLines(request, purpose, recipient, applicant, utc);

        using var bitmap = Decode(source, detection: false);
        using var typeface = ResolveTypeface(string.Join(string.Empty, lines));
        DrawVisibleWatermark(bitmap, typeface, lines);
        Embed(bitmap, CreatePayload(request.TraceId));
        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidDataException("Unable to encode the watermarked PNG.");
        return png.ToArray();
    }

    private string[] LocalizedLines(QualificationWatermarkRequest request, string purpose, string recipient,
        string applicant, DateTime utc)
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        var culture = request.WatermarkLanguage switch
        {
            "auto" => originalCulture,
            "en-US" or "zh-CN" or "zh-HK" => CultureInfo.GetCultureInfo(request.WatermarkLanguage),
            _ => throw new ArgumentException("Select a supported watermark language.", nameof(request))
        };
        try
        {
            // Scope the override to synchronous label lookup; never change the surrounding request's language.
            CultureInfo.CurrentUICulture = culture;
            return
            [
                localizer["For {0} only / No other use", purpose].Value,
                localizer["Recipient: {0}", recipient].Value,
                localizer["Applicant: {0}", applicant].Value,
                localizer["UTC: {0} UTC", utc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)].Value,
                localizer["Trace ID: {0}", request.TraceId.ToString("D")].Value
            ];
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    /// <summary>Returns null for unmarked, unsupported, over-limit or malformed images.</summary>
    public Guid? Detect(Stream image)
    {
        try
        {
            using var bitmap = Decode(image, detection: true);
            var pixels = ReadPixels(bitmap);
            var votes = new int[PayloadBits];
            var blocksAcross = bitmap.Width / BlockSize;
            var blockCount = blocksAcross * (bitmap.Height / BlockSize);
            var samples = GetSampleCount(blockCount);
            for (var sample = 0; sample < samples; sample++)
            {
                var block = (int)((long)sample * blockCount / samples);
                var coefficient = ReadCoefficientDifference(pixels, bitmap.RowBytes,
                    block % blocksAcross * BlockSize, block / blocksAcross * BlockSize);
                votes[sample % PayloadBits] += coefficient >= 0 ? 1 : -1;
            }

            var payload = new byte[PayloadBytes];
            for (var bit = 0; bit < PayloadBits; bit++)
            {
                if (votes[bit] > 0)
                {
                    payload[bit / 8] |= (byte)(1 << (7 - bit % 8));
                }
            }

            if (!payload.AsSpan(0, 4).SequenceEqual("QWM1"u8) || payload[4] != 1)
            {
                return null;
            }

            Span<byte> hash = stackalloc byte[32];
            SHA256.HashData(payload.AsSpan(0, 21), hash);
            if (!CryptographicOperations.FixedTimeEquals(hash[..8], payload.AsSpan(21, 8)))
            {
                return null;
            }

            var id = new Guid(payload.AsSpan(5, 16));
            return id == Guid.Empty ? null : id;
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or ArgumentException
                                         or NotSupportedException or ObjectDisposedException or OverflowException)
        {
            return null;
        }
    }

    private static string ValidateText(string text, string name, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > maximumLength ||
            text.Any(character => char.IsControl(character) || char.GetUnicodeCategory(character) == UnicodeCategory.Format))
        {
            throw new ArgumentException($"{name} must contain 1–{maximumLength} printable characters.", name);
        }

        return text.Trim();
    }

    private SKTypeface ResolveTypeface(string text)
    {
        var path = configuration["Qualifications:WatermarkFontPath"];
        SKTypeface? typeface;
        if (!string.IsNullOrWhiteSpace(path))
        {
            try
            {
                typeface = SKTypeface.FromFile(path);
            }
            catch (Exception exception) when (exception is IOException or ArgumentException)
            {
                throw new InvalidOperationException("Qualifications:WatermarkFontPath could not be loaded.", exception);
            }
        }
        else
        {
            typeface = SKFontManager.Default.MatchFamily("sans-serif");
            if (typeface == null || !ContainsGlyphs(typeface, text))
            {
                typeface?.Dispose();
                typeface = null;
                foreach (var character in text.EnumerateRunes().Select(rune => rune.Value).Distinct())
                {
                    var candidate = SKFontManager.Default.MatchCharacter(null,
                        [CultureInfo.CurrentUICulture.Name], character);
                    if (candidate != null && ContainsGlyphs(candidate, text))
                    {
                        typeface = candidate;
                        break;
                    }
                    candidate?.Dispose();
                }
                if (typeface == null)
                {
                    foreach (var pathCandidate in LinuxFontPaths.Where(File.Exists))
                    {
                        var fallback = SKTypeface.FromFile(pathCandidate);
                        if (fallback != null && ContainsGlyphs(fallback, text))
                        {
                            typeface = fallback;
                            break;
                        }
                        fallback?.Dispose();
                    }
                }
            }
        }

        if (typeface == null || !ContainsGlyphs(typeface, text))
        {
            typeface?.Dispose();
            throw new InvalidOperationException(
                "A font covering all watermark characters is required. Install Noto Sans CJK or configure Qualifications:WatermarkFontPath.");
        }

        return typeface;
    }

    private static bool ContainsGlyphs(SKTypeface typeface, string text)
    {
        using var font = new SKFont(typeface);
        return font.ContainsGlyphs(text);
    }

    private static void DrawVisibleWatermark(SKBitmap bitmap, SKTypeface typeface, string[] text)
    {
        using var canvas = new SKCanvas(bitmap);
        using var font = new SKFont(typeface, Math.Clamp(bitmap.Width / 40f, 16f, 28f));
        using var paint = new SKPaint();
        paint.Color = new SKColor(155, 30, 40, 100);
        paint.IsAntialias = true;
        var wrappingWidth = Math.Min(900f, bitmap.Width * .72f);
        List<string> lines;
        float tileWidth;
        float tileHeight;
        float lineHeight;
        while (true)
        {
            lines = WrapLines(text, font, wrappingWidth);
            lineHeight = font.Size * 1.4f;
            tileWidth = lines.Max(line => font.MeasureText(line)) + font.Size * 2;
            tileHeight = (lines.Count + 1) * lineHeight;
            // Keep one complete centered tile inside the rotated canvas, even for long fields.
            if (tileWidth * .8660254f + tileHeight * .5f <= bitmap.Width * .98f &&
                tileWidth * .5f + tileHeight * .8660254f <= bitmap.Height * .98f)
            {
                break;
            }
            if (font.Size <= 14f)
            {
                throw new ArgumentException("The watermark text is too long to render legibly on this image. Use a larger image or shorter fields.");
            }
            font.Size = Math.Max(14f, font.Size - .5f);
        }
        canvas.Translate(bitmap.Width / 2f, bitmap.Height / 2f);
        canvas.RotateDegrees(-30);
        var extent = MathF.Sqrt((float)bitmap.Width * bitmap.Width + (float)bitmap.Height * bitmap.Height);
        var columnCount = (int)Math.Ceiling(extent / tileWidth);
        var rowCount = (int)Math.Ceiling(extent / tileHeight);
        for (var row = -rowCount; row <= rowCount; row++)
        {
            for (var column = -columnCount; column <= columnCount; column++)
            {
                var x = column * tileWidth - tileWidth / 2 + font.Size;
                var y = row * tileHeight - tileHeight / 2 + lineHeight;
                for (var line = 0; line < lines.Count; line++)
                {
                    canvas.DrawText(lines[line], x, y + line * lineHeight, SKTextAlign.Left, font, paint);
                }
            }
        }
        canvas.Flush();
    }

    private static List<string> WrapLines(IEnumerable<string> lines, SKFont font, float maximumWidth)
    {
        var wrapped = new List<string>();
        foreach (var line in lines)
        {
            var current = new StringBuilder();
            foreach (var rune in line.EnumerateRunes())
            {
                var next = rune.ToString();
                if (current.Length > 0 && font.MeasureText(current + next) > maximumWidth)
                {
                    wrapped.Add(current.ToString());
                    current.Clear();
                }
                current.Append(next);
            }
            wrapped.Add(current.ToString());
        }
        return wrapped;
    }

    private static SKBitmap Decode(Stream source, bool detection)
    {
        using var encoded = ReadBounded(source);
        ValidateContainer(encoded.GetBuffer().AsSpan(0, (int)encoded.Length));
        using var codec = SKCodec.Create(encoded);
        if (codec == null || codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png))
        {
            throw new InvalidDataException("Only valid JPEG and PNG images are supported.");
        }

        var width = codec.Info.Width;
        var height = codec.Info.Height;
        if (width < MinimumDimension || height < MinimumDimension || (long)width * height > MaximumDecodedPixels)
        {
            throw new InvalidDataException("The source must be at least 512 × 512 and at most 25 million decoded pixels.");
        }
        var scale = Math.Min(1d, (double)MaximumOutputDimension / Math.Max(width, height));
        if (Math.Floor(Math.Min(width, height) * scale) < MinimumDimension ||
            (detection && Math.Max(width, height) > MaximumOutputDimension))
        {
            throw new InvalidDataException("The image must remain at least 512 pixels on both sides with a maximum output long side of 2048.");
        }

        using var decoded = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        if (codec.GetPixels(decoded.Info, decoded.GetPixels()) != SKCodecResult.Success)
        {
            throw new InvalidDataException("The image is corrupt or incomplete.");
        }

        var swapsDimensions = (int)codec.EncodedOrigin >= (int)SKEncodedOrigin.LeftTop;
        var outputWidth = (int)Math.Floor((swapsDimensions ? height : width) * scale);
        var outputHeight = (int)Math.Floor((swapsDimensions ? width : height) * scale);
        var output = new SKBitmap(new SKImageInfo(outputWidth, outputHeight, SKColorType.Rgba8888, SKAlphaType.Opaque));
        try
        {
            using var canvas = new SKCanvas(output);
            canvas.Clear(SKColors.White);
            canvas.Scale((float)outputWidth / (swapsDimensions ? height : width),
                (float)outputHeight / (swapsDimensions ? width : height));
            var matrix = new SKMatrix { Values = OrientationMatrix(codec.EncodedOrigin, width, height) };
            canvas.Concat(matrix);
            using var decodedImage = SKImage.FromBitmap(decoded);
            canvas.DrawImage(decodedImage, 0, 0, new SKSamplingOptions(SKFilterMode.Linear));
            canvas.Flush();
            return output;
        }
        catch
        {
            output.Dispose();
            throw;
        }
    }

    private static float[] OrientationMatrix(SKEncodedOrigin origin, int width, int height) => origin switch
    {
        SKEncodedOrigin.TopRight => [-1, 0, width, 0, 1, 0, 0, 0, 1],
        SKEncodedOrigin.BottomRight => [-1, 0, width, 0, -1, height, 0, 0, 1],
        SKEncodedOrigin.BottomLeft => [1, 0, 0, 0, -1, height, 0, 0, 1],
        SKEncodedOrigin.LeftTop => [0, 1, 0, 1, 0, 0, 0, 0, 1],
        SKEncodedOrigin.RightTop => [0, -1, height, 1, 0, 0, 0, 0, 1],
        SKEncodedOrigin.RightBottom => [0, -1, height, -1, 0, width, 0, 0, 1],
        SKEncodedOrigin.LeftBottom => [0, 1, 0, -1, 0, width, 0, 0, 1],
        _ => [1, 0, 0, 0, 1, 0, 0, 0, 1]
    };

    private static void ValidateContainer(ReadOnlySpan<byte> encoded)
    {
        if (encoded.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
        {
            var offset = 8;
            var chunks = 0;
            var hasImageData = false;
            while (offset <= encoded.Length - 12 && ++chunks <= 4096)
            {
                var length = BinaryPrimitives.ReadUInt32BigEndian(encoded.Slice(offset, 4));
                if (length > (uint)(encoded.Length - offset - 12))
                {
                    break;
                }
                var chunkLength = (int)length;
                var typeAndData = encoded.Slice(offset + 4, chunkLength + 4);
                var type = typeAndData[..4];
                if (chunks == 1 && (!type.SequenceEqual("IHDR"u8) || chunkLength != 13))
                {
                    break;
                }
                uint crc = uint.MaxValue;
                foreach (var value in typeAndData)
                {
                    crc = (crc >> 8) ^ CrcTable[(crc ^ value) & 255];
                }
                if (~crc != BinaryPrimitives.ReadUInt32BigEndian(encoded.Slice(offset + 8 + chunkLength, 4)))
                {
                    break;
                }
                hasImageData |= type.SequenceEqual("IDAT"u8);
                offset += chunkLength + 12;
                if (type.SequenceEqual("IEND"u8))
                {
                    if (chunkLength == 0 && hasImageData && offset == encoded.Length)
                    {
                        return;
                    }
                    break;
                }
            }
            throw new InvalidDataException("The PNG is corrupt, incomplete or has too many chunks.");
        }

        if (encoded.Length >= 4 && encoded[0] == 255 && encoded[1] == 216 &&
            encoded[^2] == 255 && encoded[^1] == 217)
        {
            QualificationJpegValidator.Validate(encoded);
            return;
        }
        throw new InvalidDataException("Only complete JPEG and PNG images are supported.");
    }

    private static uint[] CreateCrcTable()
    {
        var table = new uint[256];
        for (uint index = 0; index < table.Length; index++)
        {
            var value = index;
            for (var bit = 0; bit < 8; bit++)
            {
                value = (value >> 1) ^ ((value & 1) == 1 ? 0xedb88320u : 0);
            }
            table[index] = value;
        }
        return table;
    }

    private static MemoryStream ReadBounded(Stream source)
    {
        if (source == null || !source.CanRead)
        {
            throw new InvalidDataException("A readable image stream is required.");
        }
        var position = source.CanSeek ? source.Position : (long?)null;
        var encoded = new MemoryStream();
        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            if (source.CanSeek)
            {
                if (source.Length > MaximumEncodedBytes)
                {
                    throw new InvalidDataException("The encoded image exceeds the 20 MiB limit.");
                }
                source.Position = 0;
            }
            int count;
            while ((count = source.Read(buffer, 0, Math.Min(buffer.Length, MaximumEncodedBytes - (int)encoded.Length + 1))) > 0)
            {
                if (encoded.Length + count > MaximumEncodedBytes)
                {
                    throw new InvalidDataException("The encoded image exceeds the 20 MiB limit.");
                }
                encoded.Write(buffer, 0, count);
            }
            encoded.Position = 0;
            return encoded;
        }
        catch
        {
            encoded.Dispose();
            throw;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            if (position.HasValue)
            {
                source.Position = position.Value;
            }
        }
    }

    private static byte[] CreatePayload(Guid id)
    {
        var payload = new byte[PayloadBytes];
        "QWM1"u8.CopyTo(payload);
        payload[4] = 1;
        id.TryWriteBytes(payload.AsSpan(5, 16));
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(payload.AsSpan(0, 21), hash);
        hash[..8].CopyTo(payload.AsSpan(21));
        return payload;
    }

    private static int GetSampleCount(int blockCount)
    {
        var repetitions = Math.Min(MaximumRepetitions, blockCount / PayloadBits);
        if (repetitions % 2 == 0)
        {
            repetitions--;
        }
        return repetitions * PayloadBits;
    }

    private static double[] CreateBasisDifference()
    {
        var basis = new double[BlockSize * BlockSize];
        for (var y = 0; y < BlockSize; y++)
        {
            for (var x = 0; x < BlockSize; x++)
            {
                // Orthonormal 8×8 DCT coefficients (1,2) minus (2,1), both in JPEG's luminance band.
                basis[y * BlockSize + x] = .25 * (
                    Math.Cos((2 * x + 1) * Math.PI / 16) * Math.Cos((2 * y + 1) * 2 * Math.PI / 16) -
                    Math.Cos((2 * x + 1) * 2 * Math.PI / 16) * Math.Cos((2 * y + 1) * Math.PI / 16));
            }
        }
        return basis;
    }

    private static byte[] ReadPixels(SKBitmap bitmap)
    {
        var pixels = new byte[bitmap.RowBytes * bitmap.Height];
        Marshal.Copy(bitmap.GetPixels(), pixels, 0, pixels.Length);
        return pixels;
    }

    private static double ReadCoefficientDifference(byte[] pixels, int stride, int left, int top)
    {
        double coefficient = 0;
        for (var y = 0; y < BlockSize; y++)
        {
            for (var x = 0; x < BlockSize; x++)
            {
                var offset = (top + y) * stride + (left + x) * 4;
                var luminance = .299 * pixels[offset] + .587 * pixels[offset + 1] + .114 * pixels[offset + 2];
                coefficient += luminance * BasisDifference[y * BlockSize + x];
            }
        }
        return coefficient;
    }

    private static void Embed(SKBitmap bitmap, byte[] payload)
    {
        var pixels = ReadPixels(bitmap);
        var blocksAcross = bitmap.Width / BlockSize;
        var blockCount = blocksAcross * (bitmap.Height / BlockSize);
        var samples = GetSampleCount(blockCount);
        for (var sample = 0; sample < samples; sample++)
        {
            var bit = sample % PayloadBits;
            var positive = (payload[bit / 8] & (1 << (7 - bit % 8))) != 0;
            var block = (int)((long)sample * blockCount / samples);
            var left = block % blocksAcross * BlockSize;
            var top = block / blocksAcross * BlockSize;
            var difference = ReadCoefficientDifference(pixels, bitmap.RowBytes, left, top);
            if ((positive ? difference : -difference) >= CoefficientMargin)
            {
                continue;
            }
            var adjustment = ((positive ? CoefficientMargin : -CoefficientMargin) - difference) / 2;
            for (var y = 0; y < BlockSize; y++)
            {
                for (var x = 0; x < BlockSize; x++)
                {
                    var offset = (top + y) * bitmap.RowBytes + (left + x) * 4;
                    var change = adjustment * BasisDifference[y * BlockSize + x];
                    for (var channel = 0; channel < 3; channel++)
                    {
                        pixels[offset + channel] = (byte)Math.Clamp((int)Math.Round(pixels[offset + channel] + change), 0, 255);
                    }
                }
            }
        }
        Marshal.Copy(pixels, 0, bitmap.GetPixels(), pixels.Length);
    }
}
