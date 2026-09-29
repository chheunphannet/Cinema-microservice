using System.Text;

namespace Cinema.Foundation.Email;

public record TicketPassItem(
    Guid TicketId,
    string RowLabel,
    int SeatNumber,
    string SeatType,
    string QrToken,
    string SignedPassUrl
);

public static class TicketHtmlTemplateBuilder
{
    public static string GenerateQrCodeSvg(string payload, int size = 200)
    {
        // Use true ISO/IEC 18004 QR generation via QRCoder
        return QrCodeHelper.GenerateSvg(payload, 8);
    }

    public static string GenerateQrCodePngDataUri(string payload)
    {
        return QrCodeHelper.GeneratePngBase64DataUri(payload, 8);
    }

    /// <summary>
    /// Legacy overload for single-pass or pre-formatted lists.
    /// </summary>
    public static string BuildTicketHtml(
        string movieTitle,
        string branchName,
        string auditoriumName,
        DateTimeOffset showtimeStart,
        List<(string Row, int SeatNumber, string SeatType)> seats,
        decimal totalAmount,
        string orderReference,
        string signedPassUrl,
        string qrToken)
    {
        var primaryTicketId = Guid.NewGuid();
        var passItems = seats.Select((s, index) => new TicketPassItem(
            TicketId: index == 0 ? primaryTicketId : Guid.NewGuid(),
            RowLabel: s.Row,
            SeatNumber: s.SeatNumber,
            SeatType: s.SeatType,
            QrToken: index == 0 ? qrToken : $"{qrToken}:{s.Row}-{s.SeatNumber}",
            SignedPassUrl: signedPassUrl
        )).ToList();

        return BuildMultiTicketHtml(
            movieTitle,
            branchName,
            auditoriumName,
            showtimeStart,
            passItems,
            totalAmount,
            orderReference
        );
    }

    /// <summary>
    /// Official Cinema Reservation E-Ticket layout (matching Legend Cinema receipt standard).
    /// Features left-aligned header, single booking QR code for all seats, and structured plain-text receipt table.
    /// Supports external logo customization via URL or styled brand text without touching code.
    /// </summary>
    public static string BuildMultiTicketHtml(
        string movieTitle,
        string branchName,
        string auditoriumName,
        DateTimeOffset showtimeStart,
        List<TicketPassItem> passes,
        decimal totalAmount,
        string orderReference,
        string? logoUrl = null,
        string? logoText = null,
        string? logoColor = null,
        string? slogan = null,
        string? backgroundColor = null,
        string? customerName = null,
        string? foodAndBeverage = null,
        string? bookingNumber = null,
        string? bookingId = null,
        Guid? orderId = null)
    {
        var resolvedLogoText = string.IsNullOrWhiteSpace(logoText) ? "Legend" : logoText.Trim();
        var resolvedLogoColor = string.IsNullOrWhiteSpace(logoColor) ? "#dc2626" : logoColor.Trim();

        var cleanRef = (orderReference ?? "").Replace("#ORD-", "").Replace("#", "").Trim();
        var resolvedBookingId = !string.IsNullOrWhiteSpace(bookingId) 
            ? bookingId.Replace("#ORD-", "").Replace("#", "").Trim().ToUpperInvariant()
            : (string.IsNullOrWhiteSpace(cleanRef) ? Guid.NewGuid().ToString("N")[..7].ToUpperInvariant() : cleanRef.ToUpperInvariant());

        var resolvedBookingNumber = !string.IsNullOrWhiteSpace(bookingNumber) 
            ? bookingNumber.Trim() 
            : (Math.Abs(cleanRef.GetHashCode()) % 90000 + 10000).ToString();

        var formattedFullDate = showtimeStart.ToString("dddd, d MMMM yyyy");
        var formattedTime = showtimeStart.ToString("h:mm tt");
        var greetingText = !string.IsNullOrWhiteSpace(customerName) ? $"Hello {customerName.Trim()}," : "Hello,";
        var rawFood = !string.IsNullOrWhiteSpace(foodAndBeverage) ? foodAndBeverage.Trim() : "--";
        var foodDisplay = System.Net.WebUtility.HtmlEncode(rawFood).Replace("\r\n", "<br>").Replace("\n", "<br>");

        var firstPass = passes.FirstOrDefault() ?? new TicketPassItem(Guid.NewGuid(), "A", 1, "Standard", "CINEMA-PASS", "#");
        
        // Group dynamically by seat type from database
        var seatGroups = passes
            .GroupBy(p => string.IsNullOrWhiteSpace(p.SeatType) ? "Standard" : p.SeatType)
            .Select(g => {
                var name = g.Key.Trim();
                var cap = char.ToUpperInvariant(name[0]) + (name.Length > 1 ? name[1..].ToLowerInvariant() : "");
                return $"Adult {cap} (x{g.Count()})";
            });
        var seatTypeSummary = string.Join(", ", seatGroups);
        var seatLabels = string.Join(", ", passes.Select(p => $"{p.RowLabel}{p.SeatNumber}"));

        // Pure dynamic cinema branch name directly from catalog.branches
        string cinemaDisplay = !string.IsNullOrWhiteSpace(branchName) ? branchName.Trim() : $"{resolvedLogoText} Cinema";

        string logoHtml;
        if (!string.IsNullOrWhiteSpace(logoUrl))
        {
            logoHtml = $@"<img src=""{logoUrl}"" alt=""{resolvedLogoText}"" style=""max-height: 48px; max-width: 190px; object-fit: contain; display: block;"" />";
        }
        else
        {
            logoHtml = $@"
            <table border=""0"" cellpadding=""0"" cellspacing=""0"">
                <tr>
                    <td>
                        <div style=""background-color: {resolvedLogoColor}; color: #ffffff; padding: 1px 5px; font-family: monospace; font-size: 9px; font-weight: 900; letter-spacing: 4px; display: inline-block; border-radius: 2px;"">
                            &bull;&bull;&bull;&bull;&bull;
                        </div>
                        <div style=""font-size: 21px; font-weight: 900; letter-spacing: 1.5px; color: #111827; font-family: Arial, Helvetica, sans-serif; text-transform: uppercase; margin-top: 2px;"">
                            {resolvedLogoText}
                        </div>
                        <div style=""font-size: 10px; font-weight: 700; letter-spacing: 3px; color: {resolvedLogoColor}; font-family: Arial, Helvetica, sans-serif; text-transform: uppercase;"">
                            CINEMA
                        </div>
                    </td>
                </tr>
            </table>";
        }

        // Master Order QR code for the entire reservation & concession collection (turnstile/kiosk/popcorn scan)
        string masterQrPayload = (orderId.HasValue && orderId.Value != Guid.Empty)
            ? $"CINEMA-ORDER:{orderId.Value:D}:{resolvedBookingId}"
            : (!string.IsNullOrWhiteSpace(firstPass.QrToken) ? firstPass.QrToken : $"CINEMA-ORDER:{resolvedBookingId}");

        var primaryQrSvg = QrCodeHelper.GenerateSvg(masterQrPayload, 160);

        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Your Reservation - {movieTitle}</title>
</head>
<body style=""margin: 0; padding: 0; background-color: #ffffff; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #111827;"">
    <table width=""100%"" border=""0"" cellspacing=""0"" cellpadding=""0"" style=""background-color: #ffffff; padding: 24px 16px;"">
        <tr>
            <td align=""center"">
                <table width=""100%"" border=""0"" cellspacing=""0"" cellpadding=""0"" style=""max-width: 440px; text-align: left;"">
                    <!-- Brand Logo -->
                    <tr>
                        <td style=""padding-bottom: 14px;"">
                            {logoHtml}
                        </td>
                    </tr>

                    <!-- Subtle Divider -->
                    <tr>
                        <td style=""border-bottom: 1px solid #e5e7eb; padding-bottom: 16px;""></td>
                    </tr>

                    <!-- Header & Greeting -->
                    <tr>
                        <td style=""padding-top: 18px;"">
                            <h1 style=""margin: 0 0 14px 0; font-size: 22px; font-weight: 800; color: #111827;"">
                                Your Reservation
                            </h1>
                            <p style=""margin: 0 0 10px 0; font-size: 14px; color: #111827;"">
                                {greetingText}
                            </p>
                            <p style=""margin: 0 0 24px 0; font-size: 13.5px; color: #374151; line-height: 1.5;"">
                                Thank you for choosing {resolvedLogoText} Cinema! Your reservation details are listed below. Enjoy the movie!
                            </p>
                        </td>
                    </tr>

                    <!-- Grey Ticket Card with 1 QR Code and Details -->
                    <tr>
                        <td>
                            <table width=""100%"" border=""0"" cellspacing=""0"" cellpadding=""0"" style=""background-color: #fafafa; border-radius: 6px; padding: 24px 18px 24px 18px;"">
                                <!-- Single QR Code -->
                                <tr>
                                    <td align=""center"" style=""padding-bottom: 24px;"">
                                        <div style=""font-size: 11px; color: #6b7280; text-transform: uppercase; letter-spacing: 1px; margin-bottom: 10px; font-weight: 700;"">
                                            Master Order &amp; Concession QR
                                        </div>
                                        <a href=""{firstPass.SignedPassUrl}"" target=""_blank"" style=""text-decoration: none; display: inline-block;"">
                                            <table border=""0"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #ffffff; padding: 10px; border-radius: 4px; box-shadow: 0 1px 3px rgba(0,0,0,0.06); margin: 0 auto;"">
                                                <tr>
                                                    <td align=""center"" valign=""middle"" style=""line-height: 0;"">
                                                        {primaryQrSvg}
                                                    </td>
                                                </tr>
                                            </table>
                                        </a>
                                        <div style=""font-size: 11px; color: #6b7280; margin-top: 8px;"">
                                            Scan at Popcorn Counter to collect snacks or Kiosk/Entrance
                                        </div>
                                    </td>
                                </tr>

                                <!-- Real Ticket Booking Details Table -->
                                <tr>
                                    <td>
                                        <table width=""100%"" border=""0"" cellspacing=""0"" cellpadding=""0"" style=""font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; font-size: 13.5px; line-height: 1.6;"">
                                            <tr>
                                                <td style=""padding: 5px 0; color: #111827; font-weight: 600; vertical-align: top; width: 44%;"">
                                                    Booking number:
                                                </td>
                                                <td style=""padding: 5px 0; color: #4b5563; font-weight: 400; vertical-align: top; width: 56%;"">
                                                    {resolvedBookingNumber}
                                                </td>
                                            </tr>
                                            <tr>
                                                <td style=""padding: 5px 0; color: #111827; font-weight: 600; vertical-align: top;"">
                                                    Booking ID:
                                                </td>
                                                <td style=""padding: 5px 0; color: #4b5563; font-weight: 400; vertical-align: top;"">
                                                    {resolvedBookingId}
                                                </td>
                                            </tr>
                                            <tr>
                                                <td style=""padding: 5px 0; color: #111827; font-weight: 600; vertical-align: top;"">
                                                    Cinema:
                                                </td>
                                                <td style=""padding: 5px 0; color: #4b5563; font-weight: 400; vertical-align: top;"">
                                                    {cinemaDisplay}
                                                </td>
                                            </tr>
                                            <tr>
                                                <td style=""padding: 5px 0; color: #111827; font-weight: 600; vertical-align: top;"">
                                                    Movie:
                                                </td>
                                                <td style=""padding: 5px 0; color: #4b5563; font-weight: 400; vertical-align: top;"">
                                                    {movieTitle}
                                                </td>
                                            </tr>
                                            <tr>
                                                <td style=""padding: 5px 0; color: #111827; font-weight: 600; vertical-align: top;"">
                                                    Screen Name:
                                                </td>
                                                <td style=""padding: 5px 0; color: #4b5563; font-weight: 400; vertical-align: top;"">
                                                    {auditoriumName}
                                                </td>
                                            </tr>
                                            <tr>
                                                <td style=""padding: 5px 0; color: #111827; font-weight: 600; vertical-align: top;"">
                                                    Seat number:
                                                </td>
                                                <td style=""padding: 5px 0; color: #4b5563; font-weight: 400; vertical-align: top;"">
                                                    {seatTypeSummary}<br>
                                                    <span style=""letter-spacing: 0.5px;"">{seatLabels}</span>
                                                </td>
                                            </tr>
                                            <tr>
                                                <td style=""padding: 5px 0; color: #111827; font-weight: 600; vertical-align: top;"">
                                                    Food &amp; Beverage:
                                                </td>
                                                <td style=""padding: 5px 0; color: #4b5563; font-weight: 400; vertical-align: top;"">
                                                    {foodDisplay}
                                                </td>
                                            </tr>
                                            <tr>
                                                <td style=""padding: 5px 0; color: #111827; font-weight: 600; vertical-align: top;"">
                                                    Date:
                                                </td>
                                                <td style=""padding: 5px 0; color: #4b5563; font-weight: 400; vertical-align: top;"">
                                                    {formattedFullDate}
                                                </td>
                                            </tr>
                                            <tr>
                                                <td style=""padding: 5px 0; color: #111827; font-weight: 600; vertical-align: top;"">
                                                    Time:
                                                </td>
                                                <td style=""padding: 5px 0; color: #4b5563; font-weight: 400; vertical-align: top;"">
                                                    {formattedTime}
                                                </td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>

                                <!-- Action Button: Open Digital Mobile Passes & Friend Sharing -->
                                <tr>
                                    <td align=""center"" style=""padding-top: 22px; padding-bottom: 6px;"">
                                        <a href=""{firstPass.SignedPassUrl}"" target=""_blank"" style=""background-color: {resolvedLogoColor}; color: #ffffff; text-decoration: none; font-size: 13px; font-weight: 700; padding: 12px 22px; border-radius: 4px; display: inline-block; letter-spacing: 0.5px;"">
                                            VIEW &amp; SHARE MOBILE PASSES &rarr;
                                        </a>
                                        <div style=""font-size: 11px; color: #6b7280; margin-top: 8px;"">
                                            Tap to view individual seat QR codes for turnstiles &amp; share tickets with friends
                                        </div>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
    }
}
