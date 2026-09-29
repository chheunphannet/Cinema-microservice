using System.Text.RegularExpressions;
using QRCoder;

namespace Cinema.Foundation.Email;

public static class QrCodeHelper
{
    /// <summary>
    /// Generates a standardized ISO/IEC 18004 SVG QR code string.
    /// Fully vector-based, lightweight, and constrained to responsive dimensions.
    /// </summary>
    public static string GenerateSvg(string payload, int size = 160)
    {
        int targetSize = size <= 20 ? 160 : size;
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var svg = new SvgQRCode(data);
        var rawSvg = svg.GetGraphic(4, "#0f172a", "#ffffff", drawQuietZones: true);

        // Replace intrinsic width/height with responsive size and styling so it never overflows on mobile
        return Regex.Replace(
            rawSvg,
            @"<svg\s+([^>]*)width=""\d+""\s+height=""\d+""",
            $@"<svg $1width=""{targetSize}"" height=""{targetSize}"" style=""width: {targetSize}px; height: {targetSize}px; max-width: 100%; height: auto; display: block; margin: 0 auto;"""
        );
    }

    /// <summary>
    /// Generates raw PNG bytes for the QR code without requiring GDI+ or libgdiplus.
    /// Works natively in Linux containers and chiseled images.
    /// </summary>
    public static byte[] GeneratePngBytes(string payload, int pixelsPerModule = 8)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data);
        return png.GetGraphic(pixelsPerModule);
    }

    /// <summary>
    /// Generates a Base64 data URI string for direct embedding into HTML emails (e.g. data:image/png;base64,...).
    /// </summary>
    public static string GeneratePngBase64DataUri(string payload, int pixelsPerModule = 8)
    {
        var bytes = GeneratePngBytes(payload, pixelsPerModule);
        var base64 = Convert.ToBase64String(bytes);
        return $"data:image/png;base64,{base64}";
    }
}
