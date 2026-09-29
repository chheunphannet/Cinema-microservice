using System.Text;

namespace Cinema.Foundation.Storage;

public static class MediaFileValidator
{
    public const long MaxImageSizeBytes = 10 * 1024 * 1024; // 10 MB
    public const long MaxVideoSizeBytes = 100 * 1024 * 1024; // 100 MB

    public static readonly HashSet<string> AllowedCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "posters",
        "backdrops",
        "trailers",
        "screen-logos",
        "hall-logos",
        "promotions",
        "branch-gallery",
        "concessions"
    };

    private static readonly Dictionary<string, string[]> ExtensionToMimeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".jpg", new[] { "image/jpeg" } },
        { ".jpeg", new[] { "image/jpeg" } },
        { ".png", new[] { "image/png" } },
        { ".webp", new[] { "image/webp" } },
        { ".svg", new[] { "image/svg+xml" } },
        { ".mp4", new[] { "video/mp4" } },
        { ".webm", new[] { "video/webm" } }
    };

    private static readonly Dictionary<string, string> MimeToDefaultExtensionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "image/jpeg", ".jpg" },
        { "image/png", ".png" },
        { "image/webp", ".webp" },
        { "image/svg+xml", ".svg" },
        { "video/mp4", ".mp4" },
        { "video/webm", ".webm" }
    };

    public static bool IsImageExtension(string extension)
    {
        var ext = NormalizeExtension(extension);
        return ext is ".jpg" or ".jpeg" or ".png" or ".webp" or ".svg";
    }

    public static bool IsVideoExtension(string extension)
    {
        var ext = NormalizeExtension(extension);
        return ext is ".mp4" or ".webm";
    }

    public static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) return string.Empty;
        var ext = extension.Trim().ToLowerInvariant();
        return ext.StartsWith('.') ? ext : $".{ext}";
    }

    public static string GetContentTypeForExtension(string extension)
    {
        var ext = NormalizeExtension(extension);
        if (ExtensionToMimeMap.TryGetValue(ext, out var mimes) && mimes.Length > 0)
        {
            return mimes[0];
        }
        return "application/octet-stream";
    }

    public static bool IsValidCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category)) return false;
        return AllowedCategories.Contains(category.Trim());
    }

    public static (bool IsValid, string? ErrorMessage) ValidateCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return (false, "Category is required.");
        }

        if (!AllowedCategories.Contains(category.Trim()))
        {
            var allowedList = string.Join(", ", AllowedCategories);
            return (false, $"Invalid category '{category}'. Allowed categories are: {allowedList}.");
        }

        return (true, null);
    }

    public static (bool IsValid, string? ErrorMessage) ValidateExtensionAndContentType(string? fileName, string? contentType)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return (false, "File name is required.");
        }

        var ext = NormalizeExtension(Path.GetExtension(fileName));
        if (string.IsNullOrEmpty(ext) || !ExtensionToMimeMap.ContainsKey(ext))
        {
            var allowedExtensions = string.Join(", ", ExtensionToMimeMap.Keys);
            return (false, $"File extension '{ext}' is not supported. Allowed extensions are: {allowedExtensions}.");
        }

        if (string.IsNullOrWhiteSpace(contentType))
        {
            return (false, "Content-Type is required.");
        }

        var normalizedMime = contentType.Split(';')[0].Trim().ToLowerInvariant();
        var allowedMimes = ExtensionToMimeMap[ext];

        if (!allowedMimes.Contains(normalizedMime, StringComparer.OrdinalIgnoreCase))
        {
            return (false, $"Content-Type '{contentType}' does not match file extension '{ext}'. Expected: {string.Join(" or ", allowedMimes)}.");
        }

        return (true, null);
    }

    public static (bool IsValid, string? ErrorMessage) ValidateFileSize(long sizeBytes, string extension)
    {
        if (sizeBytes <= 0)
        {
            return (false, "File cannot be empty (0 bytes).");
        }

        var ext = NormalizeExtension(extension);
        if (IsImageExtension(ext))
        {
            if (sizeBytes > MaxImageSizeBytes)
            {
                return (false, $"Image file size ({sizeBytes:N0} bytes) exceeds maximum limit of {MaxImageSizeBytes / (1024 * 1024)}MB.");
            }
        }
        else if (IsVideoExtension(ext))
        {
            if (sizeBytes > MaxVideoSizeBytes)
            {
                return (false, $"Video file size ({sizeBytes:N0} bytes) exceeds maximum limit of {MaxVideoSizeBytes / (1024 * 1024)}MB.");
            }
        }
        else
        {
            return (false, $"Unsupported file type for extension '{ext}'.");
        }

        return (true, null);
    }

    public static (bool IsValid, string? ErrorMessage) ValidateMagicNumbers(Stream stream, string extension)
    {
        var ext = NormalizeExtension(extension);
        if (!stream.CanRead)
        {
            return (false, "Stream cannot be read for magic number verification.");
        }

        long originalPosition = 0;
        if (stream.CanSeek)
        {
            originalPosition = stream.Position;
        }

        try
        {
            var header = new byte[32];
            var bytesRead = stream.Read(header, 0, header.Length);

            if (bytesRead < 4 && ext != ".svg")
            {
                return (false, "File stream is too short to be a valid media file.");
            }

            switch (ext)
            {
                case ".jpg":
                case ".jpeg":
                    // JPEG starts with FF D8 FF
                    if (header[0] != 0xFF || header[1] != 0xD8 || header[2] != 0xFF)
                    {
                        return (false, "File header signature does not match JPEG format (0xFF, 0xD8, 0xFF).");
                    }
                    break;

                case ".png":
                    // PNG starts with 89 50 4E 47 0D 0A 1A 0A
                    byte[] pngMagic = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
                    for (int i = 0; i < pngMagic.Length; i++)
                    {
                        if (header[i] != pngMagic[i])
                        {
                            return (false, "File header signature does not match PNG format.");
                        }
                    }
                    break;

                case ".webp":
                    // Starts with RIFF (0x52, 0x49, 0x46, 0x46) and bytes 8..11 are WEBP (0x57, 0x45, 0x42, 0x50)
                    if (bytesRead < 12 || header[0] != 0x52 || header[1] != 0x49 || header[2] != 0x46 || header[3] != 0x46)
                    {
                        return (false, "File header signature does not match WebP RIFF format.");
                    }
                    if (header[8] != 0x57 || header[9] != 0x45 || header[10] != 0x42 || header[11] != 0x50)
                    {
                        return (false, "File header signature does not match WebP format (missing WEBP tag).");
                    }
                    break;

                case ".svg":
                    // SVG is XML text. Read up to 512KB to inspect the full SVG structure & security boundaries
                    string svgText;
                    if (stream.CanSeek)
                    {
                        stream.Position = originalPosition;
                        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true);
                        var buffer = new char[512 * 1024];
                        var charsRead = reader.Read(buffer, 0, buffer.Length);
                        svgText = new string(buffer, 0, charsRead);
                    }
                    else
                    {
                        svgText = Encoding.UTF8.GetString(header, 0, bytesRead);
                    }

                    if (string.IsNullOrWhiteSpace(svgText) || !svgText.Contains("<svg", StringComparison.OrdinalIgnoreCase))
                    {
                        return (false, "File content is not valid SVG XML.");
                    }

                    // Security check: Reject SVG containing embedded JavaScript execution or dangerous active content
                    string[] dangerousPatterns = {
                        "<script", "javascript:", "onload=", "onerror=", "onclick=", "onmouseover=", "onfocus=",
                        "<iframe", "<object", "<embed", "xlink:href=\"data:text/html", "xlink:href=\"javascript:",
                        "href=\"javascript:"
                    };

                    foreach (var pattern in dangerousPatterns)
                    {
                        if (svgText.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                        {
                            return (false, "SVG file contains prohibited embedded scripts or active content (XSS prevention).");
                        }
                    }
                    break;

                case ".mp4":
                    // MP4 has 'ftyp' in bytes 4..7 (0x66, 0x74, 0x79, 0x70)
                    if (bytesRead < 8 || header[4] != 0x66 || header[5] != 0x74 || header[6] != 0x79 || header[7] != 0x70)
                    {
                        return (false, "File header signature does not match MP4 container format (missing 'ftyp' atom).");
                    }
                    break;

                case ".webm":
                    // WebM EBML header: 1A 45 DF A3
                    if (header[0] != 0x1A || header[1] != 0x45 || header[2] != 0xDF || header[3] != 0xA3)
                    {
                        return (false, "File header signature does not match WebM EBML format (0x1A, 0x45, 0xDF, 0xA3).");
                    }
                    break;
            }

            return (true, null);
        }
        finally
        {
            if (stream.CanSeek)
            {
                stream.Position = originalPosition;
            }
        }
    }

    public static (bool IsValid, string? ErrorMessage) ValidateFile(
        string? fileName, 
        string? contentType, 
        string? category, 
        long length, 
        Stream? stream = null)
    {
        var categoryCheck = ValidateCategory(category);
        if (!categoryCheck.IsValid) return categoryCheck;

        var extCheck = ValidateExtensionAndContentType(fileName, contentType);
        if (!extCheck.IsValid) return extCheck;

        var ext = NormalizeExtension(Path.GetExtension(fileName!));
        var sizeCheck = ValidateFileSize(length, ext);
        if (!sizeCheck.IsValid) return sizeCheck;

        if (stream != null)
        {
            var magicCheck = ValidateMagicNumbers(stream, ext);
            if (!magicCheck.IsValid) return magicCheck;
        }

        return (true, null);
    }

    public static (bool IsValid, string? ErrorMessage) ValidatePresignRequest(
        string? fileName, 
        string? contentType, 
        string? category)
    {
        var categoryCheck = ValidateCategory(category);
        if (!categoryCheck.IsValid) return categoryCheck;

        var extCheck = ValidateExtensionAndContentType(fileName, contentType);
        if (!extCheck.IsValid) return extCheck;

        return (true, null);
    }

    public static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return "file";

        var nameOnly = Path.GetFileName(fileName);
        var invalidChars = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder();

        foreach (var c in nameOnly)
        {
            if (!invalidChars.Contains(c) && c != '/' && c != '\\' && c != ':' && c != '?' && c != '*' && c != '"' && c != '<' && c != '>' && c != '|')
            {
                sb.Append(c);
            }
        }

        var result = sb.ToString().Trim();
        return string.IsNullOrWhiteSpace(result) ? "file" : result;
    }
}
