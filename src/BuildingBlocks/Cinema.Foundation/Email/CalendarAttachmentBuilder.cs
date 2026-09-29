using System.Text;

namespace Cinema.Foundation.Email;

public static class CalendarAttachmentBuilder
{
    public static EmailAttachment BuildShowtimeIcs(
        string movieTitle,
        string branchName,
        string auditoriumName,
        DateTimeOffset showtimeStart,
        int durationMinutes,
        string orderReference,
        string signedPassUrl)
    {
        var showtimeEnd = showtimeStart.AddMinutes(durationMinutes > 0 ? durationMinutes : 120);
        var now = DateTimeOffset.UtcNow;

        var sb = new StringBuilder();
        sb.AppendLine("BEGIN:VCALENDAR");
        sb.AppendLine("VERSION:2.0");
        sb.AppendLine("PRODID:-//Cinema City//Digital Ticketing//EN");
        sb.AppendLine("CALSCALE:GREGORIAN");
        sb.AppendLine("METHOD:PUBLISH");
        sb.AppendLine("BEGIN:VEVENT");
        sb.AppendLine($"UID:{orderReference}-{showtimeStart.ToUnixTimeSeconds()}@cinemacity.local");
        sb.AppendLine($"DTSTAMP:{now:yyyyMMddTHHmmssZ}");
        sb.AppendLine($"DTSTART:{showtimeStart.UtcDateTime:yyyyMMddTHHmmssZ}");
        sb.AppendLine($"DTEND:{showtimeEnd.UtcDateTime:yyyyMMddTHHmmssZ}");
        sb.AppendLine($"SUMMARY:🎬 Movie: {EscapeIcsText(movieTitle)}");
        sb.AppendLine($"LOCATION:{EscapeIcsText(branchName)} - {EscapeIcsText(auditoriumName)}");
        sb.AppendLine($"DESCRIPTION:Cinema: {EscapeIcsText(branchName)}\\nHall: {EscapeIcsText(auditoriumName)}\\nOrder Ref: {EscapeIcsText(orderReference)}\\nYour Digital Pass: {signedPassUrl}");
        sb.AppendLine("STATUS:CONFIRMED");
        sb.AppendLine("END:VEVENT");
        sb.AppendLine("END:VCALENDAR");

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return new EmailAttachment(
            FileName: $"{orderReference.Trim('#', ' ')}-showtime.ics",
            Content: bytes,
            ContentType: "text/calendar; charset=utf-8; method=REQUEST"
        );
    }

    private static string EscapeIcsText(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text
            .Replace("\\", "\\\\")
            .Replace(";", "\\;")
            .Replace(",", "\\,")
            .Replace("\r", "")
            .Replace("\n", "\\n");
    }
}
