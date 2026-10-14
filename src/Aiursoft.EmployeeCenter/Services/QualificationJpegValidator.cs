namespace Aiursoft.EmployeeCenter.Services;

// Skia/libjpeg can report success after synthesizing missing JPEG scan data. Validate the
// Huffman-coded baseline/progressive scans strictly before passing untrusted bytes to Skia.
internal static class QualificationJpegValidator
{
    public static void Validate(ReadOnlySpan<byte> data)
    {
        try
        {
            new QualificationJpegDecoder(data.ToArray()).Validate();
        }
        catch (Exception exception) when (exception is IndexOutOfRangeException or OverflowException or ArgumentException)
        {
            throw new InvalidDataException("The JPEG body is corrupt or incomplete.", exception);
        }
    }

    internal static InvalidDataException Invalid() => new("The JPEG body is corrupt, incomplete or exceeds supported scan limits.");
}
