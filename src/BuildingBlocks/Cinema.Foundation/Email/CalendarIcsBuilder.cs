using System.Text;

namespace Cinema.Foundation.Email;

public static class CalendarIcsBuilder
{
    /// <summary>
    /// Generates an RFC 5545 compliant iCalendar (.ics) file content for cinema showtimes.
    /// Compatible with Google Calendar, Apple Calendar, and Microsoft Outlook.
    /// </summary>
    public static byte[] BuildShowtimeIcs(
        string movieTitle,
        string branchName,
        string auditoriumName,
        DateTimeOffset showtimeStart,
        TimeSpan duration,
        string seatsSummary,
        string orderReference,
        Guid orderId)
    {
        var showtimeEnd = showtimeStart.Add(duration > TimeSpan.Zero ? duration : TimeSpan.FromHours(2));
        var nowUtc = DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssZ");
        var startUtc = showtimeStart.ToUniversalTime().ToString("yyyyMMddTHHmmssZ");
        var endUtc = showtimeEnd.ToUniversalTime().ToString("yyyyMMddTHHmmssZ");

        var sb = new StringBuilder();
        sb.AppendLine("BEGIN:VCALENDAR");
        sb.AppendLine("VERSION:2.0");
        sb.AppendLine("PRODID:-//Cinema City//Digital Ticketing System//EN");
        sb.AppendLine("CALSCALE:GREGORIAN");
        sb.AppendLine("METHOD:PUBLISH");
        sb.AppendLine("BEGIN:VEVENT");
        sb.AppendLine($"UID:cinema-order-{orderId:N}@cinemacity.local");
        sb.AppendLine($"DTSTAMP:{nowUtc}");
        sb.AppendLine($"DTSTART:{startUtc}");
        sb.AppendLine($"DTEND:{endUtc}");
        sb.AppendLine($"SUMMARY:🎬 Movie: {movieTitle}");
        sb.AppendLine($"DESCRIPTION:Cinema City E-Ticket Booking\\nMovie: {movieTitle}\\nCinema: {branchName} ({auditoriumName})\\nSeats: {seatsSummary}\\nOrder Reference: {orderReference}\\nPlease arrive 15 minutes before showtime.");
        sb.AppendLine($"LOCATION:{branchName} - {auditoriumName}");
        sb.AppendLine("STATUS:CONFIRMED");
        sb.AppendLine("TRANSP:OPAQUE");
        sb.AppendLine("SEQUENCE:0");
        sb.AppendLine("BEGIN:VALARM");
        sb.AppendLine("TRIGGER:-PT30M");
        sb.AppendLine("ACTION:DISPLAY");
        sb.AppendLine($"DESCRIPTION:Reminder: {movieTitle} starts in 30 minutes at {branchName}!");
        sb.AppendLine("END:VALARM");
        sb.AppendLine("END:VEVENT");
        sb.AppendLine("END:VCALENDAR");

        return Encoding.UTF8.GetBytes(sb.ToString());
    }
}
