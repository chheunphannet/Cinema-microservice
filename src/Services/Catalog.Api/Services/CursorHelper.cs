using System.Text;

namespace Catalog.Api.Services;

public static class CursorHelper
{
    public static string EncodeOffset(int offset)
    {
        var safeOffset = Math.Max(0, offset);
        var bytes = Encoding.UTF8.GetBytes($"cur_{safeOffset}");
        return Convert.ToBase64String(bytes);
    }

    public static int DecodeOffset(string? cursor, int defaultOffset = 0)
    {
        var fallback = Math.Max(0, defaultOffset);
        if (string.IsNullOrWhiteSpace(cursor))
            return fallback;

        var trimmed = cursor.Trim();

        // 1. Direct numeric string support e.g. "20"
        if (int.TryParse(trimmed, out var num) && num >= 0)
        {
            return num;
        }

        // 2. Direct unencoded cur_ prefix e.g. "cur_20"
        if (trimmed.StartsWith("cur_", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(trimmed.AsSpan(4), out var directOffset) && directOffset >= 0)
        {
            return directOffset;
        }

        // 3. Base64 / Base64Url decoded cursor
        try
        {
            var padded = trimmed.Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 2: padded += "=="; break;
                case 3: padded += "="; break;
            }

            var bytes = Convert.FromBase64String(padded);
            var str = Encoding.UTF8.GetString(bytes);
            if (str.StartsWith("cur_", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(str.AsSpan(4), out var offset) && offset >= 0)
            {
                return offset;
            }
        }
        catch
        {
            // Fall back to default on invalid or corrupted cursor
        }

        return fallback;
    }
}
