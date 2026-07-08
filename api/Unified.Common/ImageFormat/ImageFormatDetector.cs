namespace Unified.Common.ImageFormat;

public static class ImageFormatDetector
{
    public static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    public static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47];

    public const string JpegContentType = "image/jpeg";
    public const string PngContentType = "image/png";

    public static string? Detect(byte[] bytes)
    {
        if (bytes.Length >= JpegSignature.Length && bytes.AsSpan(0, JpegSignature.Length).SequenceEqual(JpegSignature))
            return JpegContentType;
        if (bytes.Length >= PngSignature.Length && bytes.AsSpan(0, PngSignature.Length).SequenceEqual(PngSignature))
            return PngContentType;
        return null;
    }
}
