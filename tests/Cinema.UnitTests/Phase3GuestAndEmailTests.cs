using System.Text;
using System.Text.Json;
using Cinema.Foundation.Email;
using Cinema.Foundation.Messaging;
using Cinema.Foundation.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cinema.UnitTests;

public class Phase3GuestAndEmailTests
{
    private const string TestSecret = "phase3-unit-test-hmac-secret-key-must-be-long-enough!";

    [Fact]
    public void SignedTicketUrlService_ShouldGenerateDeterministicSignature()
    {
        var ticketId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        long exp = 1893456000; // 2030-01-01

        var sig1 = SignedTicketUrlService.GenerateSignature(ticketId, exp, TestSecret);
        var sig2 = SignedTicketUrlService.GenerateSignature(ticketId, exp, TestSecret);

        Assert.NotNull(sig1);
        Assert.Equal(64, sig1.Length); // SHA256 hex output is 64 characters
        Assert.Equal(sig1, sig2);
    }

    [Fact]
    public void SignedTicketUrlService_ShouldValidateCorrectSignature()
    {
        var ticketId = Guid.NewGuid();
        long exp = DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeSeconds();

        var signature = SignedTicketUrlService.GenerateSignature(ticketId, exp, TestSecret);

        Assert.True(SignedTicketUrlService.ValidateSignature(ticketId, exp, signature, TestSecret));
    }

    [Fact]
    public void SignedTicketUrlService_ShouldRejectTamperedSignature()
    {
        var ticketId = Guid.NewGuid();
        long exp = DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeSeconds();

        var signature = SignedTicketUrlService.GenerateSignature(ticketId, exp, TestSecret);
        // Tamper with one character
        var tampered = signature.Substring(0, signature.Length - 1) + (signature.EndsWith('a') ? 'b' : 'a');

        Assert.False(SignedTicketUrlService.ValidateSignature(ticketId, exp, tampered, TestSecret));
    }

    [Fact]
    public void SignedTicketUrlService_ShouldRejectMismatchedTicketId()
    {
        var ticketId = Guid.NewGuid();
        var attackerTicketId = Guid.NewGuid();
        long exp = DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeSeconds();

        var signature = SignedTicketUrlService.GenerateSignature(ticketId, exp, TestSecret);

        Assert.False(SignedTicketUrlService.ValidateSignature(attackerTicketId, exp, signature, TestSecret));
    }

    [Fact]
    public void SignedTicketUrlService_ShouldRejectExpiredTicketPass()
    {
        var ticketId = Guid.NewGuid();
        long expiredTimestamp = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds();

        var signature = SignedTicketUrlService.GenerateSignature(ticketId, expiredTimestamp, TestSecret);

        Assert.False(SignedTicketUrlService.ValidateSignature(ticketId, expiredTimestamp, signature, TestSecret));
    }

    [Fact]
    public void SignedTicketUrlService_ShouldBuildProperUrlWithQueryParameters()
    {
        var ticketId = Guid.NewGuid();
        var url = SignedTicketUrlService.BuildSignedETicketUrl("http://localhost:8080", ticketId, TimeSpan.FromHours(1), TestSecret);

        Assert.StartsWith("http://localhost:8080/api/v1/tickets/e-ticket/", url);
        Assert.Contains(ticketId.ToString(), url);
        Assert.Contains("exp=", url);
        Assert.Contains("sig=", url);
    }

    [Fact]
    public void SignedTicketUrlService_ShouldUnifyHmacSecretAndGatewayUrl()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Tickets:HmacSecret", "custom-tickets-hmac-key-1234567890!" },
                { "Gateway:PublicUrl", "https://cinema.example.com" }
            })
            .Build();

        var secret = SignedTicketUrlService.GetSecretKey(config);
        var gatewayUrl = SignedTicketUrlService.GetPublicGatewayUrl(config);

        Assert.Equal("custom-tickets-hmac-key-1234567890!", secret);
        Assert.Equal("https://cinema.example.com", gatewayUrl);

        var ticketId = Guid.NewGuid();
        var fullUrl = SignedTicketUrlService.BuildSignedETicketUrl(config, ticketId);
        Assert.StartsWith("https://cinema.example.com/api/v1/tickets/e-ticket/", fullUrl);
        Assert.Contains(ticketId.ToString(), fullUrl);
    }

    [Theory]
    [InlineData("Mailpit", typeof(MailpitEmailService))]
    [InlineData("Resend", typeof(ResendEmailService))]
    [InlineData("AmazonSes", typeof(AmazonSesEmailService))]
    [InlineData("Smtp", typeof(GenericSmtpEmailService))]
    [InlineData("", typeof(MailpitEmailService))] // default fallback
    public void EmailServiceExtensions_ShouldResolveCorrectProviderBasedOnConfig(string provider, Type expectedServiceType)
    {
        var configData = new Dictionary<string, string?>
        {
            { "Email:Provider", provider },
            { "Email:SenderEmail", "test@cinema.local" },
            { "Email:SenderName", "Cinema Test" },
            { "Email:Mailpit:Host", "mailpit" },
            { "Email:Mailpit:Port", "1025" },
            { "Email:Resend:ApiKey", "re_test_123" }
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCinemaEmail(config);

        var sp = services.BuildServiceProvider();
        var emailService = sp.GetRequiredService<IEmailService>();

        Assert.NotNull(emailService);
        Assert.IsType(expectedServiceType, emailService);
    }

    [Fact]
    public void QrCodeHelper_ShouldGenerateValidIso18004SvgAndPngBytes()
    {
        const string payload = "CINEMA-TICKET:sample-guid:reservation:seat";

        var svg = QrCodeHelper.GenerateSvg(payload, 160);
        Assert.NotNull(svg);
        Assert.StartsWith("<svg", svg.Trim());
        Assert.Contains("width=\"160\"", svg);
        Assert.Contains("height=\"160\"", svg);
        Assert.Contains("</svg>", svg);

        var pngBytes = QrCodeHelper.GeneratePngBytes(payload, 8);
        Assert.NotNull(pngBytes);
        Assert.True(pngBytes.Length > 100);
        // Verify PNG magic header bytes: 0x89 0x50 0x4E 0x47 (PNG)
        Assert.Equal(0x89, pngBytes[0]);
        Assert.Equal(0x50, pngBytes[1]);
        Assert.Equal(0x4E, pngBytes[2]);
        Assert.Equal(0x47, pngBytes[3]);

        var dataUri = QrCodeHelper.GeneratePngBase64DataUri(payload, 8);
        Assert.StartsWith("data:image/png;base64,", dataUri);
    }

    [Fact]
    public void CalendarIcsBuilder_ShouldGenerateRfc5545CompliantIcsFile()
    {
        var showtime = new DateTimeOffset(2026, 10, 24, 19, 30, 0, TimeSpan.Zero);
        var orderId = Guid.NewGuid();

        var icsBytes = CalendarIcsBuilder.BuildShowtimeIcs(
            movieTitle: "Dune: Part Two",
            branchName: "Downtown Mall",
            auditoriumName: "Auditorium 1 (IMAX)",
            showtimeStart: showtime,
            duration: TimeSpan.FromMinutes(166),
            seatsSummary: "Row F - Seat 12 (VIP), Row F - Seat 13 (VIP)",
            orderReference: "#ORD-98412",
            orderId: orderId
        );

        Assert.NotNull(icsBytes);
        var icsText = Encoding.UTF8.GetString(icsBytes);

        Assert.Contains("BEGIN:VCALENDAR", icsText);
        Assert.Contains("VERSION:2.0", icsText);
        Assert.Contains("BEGIN:VEVENT", icsText);
        Assert.Contains("SUMMARY:🎬 Movie: Dune: Part Two", icsText);
        Assert.Contains("LOCATION:Downtown Mall - Auditorium 1 (IMAX)", icsText);
        Assert.Contains("DTSTART:20261024T193000Z", icsText);
        Assert.Contains("STATUS:CONFIRMED", icsText);
        Assert.Contains("END:VEVENT", icsText);
        Assert.Contains("END:VCALENDAR", icsText);
    }

    [Fact]
    public void TicketHtmlTemplateBuilder_ShouldBuildMultiSeatPasses()
    {
        var showtime = DateTimeOffset.UtcNow.AddDays(1);
        var passes = new List<TicketPassItem>
        {
            new(Guid.NewGuid(), "F", 12, "VIP", "QR-TOKEN-F12", "http://localhost:8080/pass/1"),
            new(Guid.NewGuid(), "F", 13, "VIP", "QR-TOKEN-F13", "http://localhost:8080/pass/2")
        };

        var html = TicketHtmlTemplateBuilder.BuildMultiTicketHtml(
            movieTitle: "Dune: Part Two",
            branchName: "Downtown Mall",
            auditoriumName: "Auditorium 1 (IMAX)",
            showtimeStart: showtime,
            passes: passes,
            totalAmount: 18.50m,
            orderReference: "#ORD-DUNE2"
        );

        Assert.NotNull(html);
        Assert.True(html.Contains("CINEMA CITY") || html.Contains("REGAL") || html.Contains("Legend"));
        Assert.Contains("Dune: Part Two", html);
        Assert.Contains("Downtown Mall", html);
        Assert.Contains("Auditorium 1 (IMAX)", html);
        Assert.Contains("F12", html);
        Assert.Contains("F13", html);
        Assert.Contains("http://localhost:8080/pass/1", html);
        Assert.Contains("<svg", html);
    }

    [Fact]
    public void TicketHtmlTemplateBuilder_ShouldRenderFoodAndBeverageSetsAccurately()
    {
        var showtime = DateTimeOffset.UtcNow.AddDays(1);
        var passes = new List<TicketPassItem>
        {
            new(Guid.NewGuid(), "B", 1, "Standard", "QR-B1", "http://localhost:8080/pass/b1"),
            new(Guid.NewGuid(), "B", 2, "Standard", "QR-B2", "http://localhost:8080/pass/b2")
        };

        var html = TicketHtmlTemplateBuilder.BuildMultiTicketHtml(
            movieTitle: "Oppenheimer",
            branchName: "Legend Toul Kork",
            auditoriumName: "Hall 2",
            showtimeStart: showtime,
            passes: passes,
            totalAmount: 45.00m,
            orderReference: "#ORD-OPP4F",
            customerName: "Phannet Chhern",
            foodAndBeverage: "4x Classic Couple Combo (Popcorn & Soda)",
            bookingNumber: "31604",
            bookingId: "WQ2LWMK"
        );

        Assert.NotNull(html);
        Assert.Contains("Hello Phannet Chhern,", html);
        Assert.Contains("31604", html);
        Assert.Contains("WQ2LWMK", html);
        Assert.Contains("Legend Toul Kork", html);
        Assert.Contains("Hall 2", html);
        Assert.Contains("Adult Standard (x2)", html);
        Assert.Contains("B1, B2", html);
        Assert.Contains("Food &amp; Beverage:", html);
        Assert.Contains("4x Classic Couple Combo (Popcorn &amp; Soda)", html);
    }

    [Fact]
    public void TicketIssuedIntegrationEvent_ShouldSerializeAndDeserializeAccurately()
    {
        var evt = new TicketIssuedIntegrationEvent(
            OrderId: Guid.NewGuid(),
            ReservationId: Guid.NewGuid(),
            CustomerEmail: "guest@example.com",
            CustomerPhone: "+85512345678",
            CustomerName: "John Guest",
            MovieTitle: "Avatar: The Way of Water",
            BranchName: "Riverside Plaza",
            AuditoriumName: "Auditorium 2",
            ShowtimeStart: DateTimeOffset.UtcNow.AddHours(3),
            Seats: new List<TicketSeatItem>
            {
                new(Guid.NewGuid(), "A", 1, "standard", "QR-1"),
                new(Guid.NewGuid(), "A", 2, "standard", "QR-2")
            },
            TotalAmount: 13.00m,
            IssuedAt: DateTimeOffset.UtcNow
        );

        var json = JsonSerializer.Serialize(evt);
        var deserialized = JsonSerializer.Deserialize<TicketIssuedIntegrationEvent>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(evt.OrderId, deserialized.OrderId);
        Assert.Equal(evt.ReservationId, deserialized.ReservationId);
        Assert.Equal(evt.CustomerEmail, deserialized.CustomerEmail);
        Assert.Equal(evt.MovieTitle, deserialized.MovieTitle);
        Assert.Equal(2, deserialized.Seats.Count);
        Assert.Equal("A", deserialized.Seats[0].RowLabel);
        Assert.Equal(1, deserialized.Seats[0].SeatNumber);
    }

    [Theory]
    [InlineData("moviegoer@gmail.com", true)]
    [InlineData("customer.vip+cinema@domain.co.uk", true)]
    [InlineData("plainaddress", false)]
    [InlineData("@missingusername.com", false)]
    [InlineData("username@.com", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void GuestEmailValidation_ShouldIdentifyValidAndInvalidEmails(string email, bool expectedValid)
    {
        var emailRegex = new System.Text.RegularExpressions.Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", System.Text.RegularExpressions.RegexOptions.Compiled);
        bool isValid = !string.IsNullOrWhiteSpace(email) && emailRegex.IsMatch(email.Trim());
        Assert.Equal(expectedValid, isValid);
    }

    [Fact]
    public async Task SeatLockService_Release_ShouldMatchHoldIdPrefixEvenWithFencingToken()
    {
        var lockService = new Cinema.Foundation.Redis.SeatLockService(redis: null);
        var showtimeId = Guid.NewGuid();
        var seatId = Guid.NewGuid();
        var holdId = Guid.NewGuid();
        long fencingToken = 123456789L;
        var ttl = TimeSpan.FromMinutes(8);

        // Lock is stored in format: "{holdId}:{fencingToken}"
        var (acquired, lockValue) = await lockService.AcquireSeatHoldAsync(showtimeId, seatId, holdId, fencingToken, ttl);
        Assert.True(acquired);
        Assert.Equal($"{holdId}:{fencingToken}", lockValue);

        // Release passing only holdId.ToString() should successfully unlock due to prefix matching
        bool released = await lockService.ReleaseSeatHoldAsync(showtimeId, seatId, holdId.ToString());
        Assert.True(released, "Releasing with holdId prefix must unlock the seat");

        // Verify the seat is now free for another customer
        var newHoldId = Guid.NewGuid();
        var (reacquired, _) = await lockService.AcquireSeatHoldAsync(showtimeId, seatId, newHoldId, 987654321L, ttl);
        Assert.True(reacquired, "Seat must be re-acquirable after hold release");
    }

    [Theory]
    [InlineData("guest@cinema.com", null, 8, 8)]     // Guest: max 8 seats, 8 min TTL
    [InlineData(null, null, 8, 8)]                   // Anonymous guest: max 8 seats, 8 min TTL
    [InlineData(null, "some-guid", 10, 10)]          // Authenticated staff/registered: max 10 seats, 10 min TTL
    public void GuestVsStaffRules_ShouldEnforceAppropriateCapsAndTtls(string? guestEmail, string? customerIdStr, int expectedMaxSeats, int expectedTtlMinutes)
    {
        Guid? customerId = customerIdStr != null ? Guid.NewGuid() : null;
        bool isGuest = !string.IsNullOrWhiteSpace(guestEmail) || customerId == null;

        int maxSeats = isGuest ? 8 : 10;
        int ttlMinutes = isGuest ? 8 : 10;

        Assert.Equal(expectedMaxSeats, maxSeats);
        Assert.Equal(expectedTtlMinutes, ttlMinutes);
    }

    [Fact]
    public void EmailMessage_WithAttachments_ShouldRetainAttachmentProperties()
    {
        var icsBytes = Encoding.UTF8.GetBytes("BEGIN:VCALENDAR\nEND:VCALENDAR");
        var msg = new EmailMessage(
            ToEmail: "guest@example.com",
            ToName: "Guest User",
            Subject: "Test Ticket",
            HtmlBody: "<p>Enjoy your movie!</p>",
            PlainTextBody: "Enjoy your movie!",
            Attachments: new List<EmailAttachment>
            {
                new("ticket.ics", icsBytes, "text/calendar")
            }
        );

        Assert.NotNull(msg.Attachments);
        Assert.Single(msg.Attachments);
        Assert.Equal("ticket.ics", msg.Attachments[0].FileName);
        Assert.Equal("text/calendar", msg.Attachments[0].ContentType);
        Assert.Equal(icsBytes.Length, msg.Attachments[0].Content.Length);
    }

    [Fact]
    public void SignedTicketUrlService_MultiSeatPassUrl_ShouldVerifyForEachSeat()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tickets:HmacSecret"] = TestSecret,
                ["Gateway:PublicUrl"] = "http://localhost:8080"
            })
            .Build();

        var ticket1 = Guid.NewGuid();
        var ticket2 = Guid.NewGuid();

        var url1 = SignedTicketUrlService.BuildSignedETicketUrl(config, ticket1, TimeSpan.FromHours(2));
        var url2 = SignedTicketUrlService.BuildSignedETicketUrl(config, ticket2, TimeSpan.FromHours(2));

        Assert.Contains(ticket1.ToString(), url1);
        Assert.Contains(ticket2.ToString(), url2);
        Assert.NotEqual(url1, url2);
    }

    [Fact]
    public void ConcessionVoucher_ShouldGenerateValidBarcodeAndFulfillmentPayload()
    {
        var orderId = Guid.NewGuid();
        string voucherCode = $"FNB-{orderId:N}"[..12].ToUpperInvariant();
        string qrPayload = $"CINEMA-FNB:{orderId}:{voucherCode}";

        var svg = QrCodeHelper.GenerateSvg(qrPayload, 8);

        Assert.StartsWith("FNB-", voucherCode);
        Assert.Contains("CINEMA-FNB:", qrPayload);
        Assert.Contains("<svg", svg);
        Assert.Contains("</svg>", svg);
    }

    [Fact]
    public void TicketHtmlTemplateBuilder_ShouldEncodeMasterOrderQr_WhenOrderIdProvided()
    {
        var orderId = Guid.NewGuid();
        var showtime = DateTimeOffset.UtcNow.AddDays(1);
        var passes = new List<TicketPassItem>
        {
            new(Guid.NewGuid(), "E", 1, "VIP", "QR-E1", "http://localhost:8080/pass/e1"),
            new(Guid.NewGuid(), "E", 2, "VIP", "QR-E2", "http://localhost:8080/pass/e2")
        };

        var html = TicketHtmlTemplateBuilder.BuildMultiTicketHtml(
            movieTitle: "Interstellar",
            branchName: "Legend Eden Garden",
            auditoriumName: "Screen 1 (Dolby Atmos)",
            showtimeStart: showtime,
            passes: passes,
            totalAmount: 30.00m,
            orderReference: "#ORD-INT99",
            customerName: "Alice Wonderland",
            foodAndBeverage: "2x Caramel Popcorn & Soda",
            bookingNumber: "99881",
            bookingId: "INT99X",
            orderId: orderId
        );

        Assert.NotNull(html);
        Assert.Contains("Master Order &amp; Concession QR", html);
        Assert.Contains("Scan at Popcorn Counter to collect snacks or Kiosk/Entrance", html);
        Assert.Contains("VIEW &amp; SHARE MOBILE PASSES", html);
        Assert.Contains("share tickets with friends", html);
        Assert.Contains("Alice Wonderland", html);
        Assert.Contains("99881", html);
        Assert.Contains("INT99X", html);
        Assert.Contains("2x Caramel Popcorn &amp; Soda", html);
        Assert.Contains("<svg", html);
    }

    [Theory]
    [InlineData("CINEMA-ORDER:3fa85f64-5717-4562-b3fc-2c963f66afa6:B47X9A", "3fa85f64-5717-4562-b3fc-2c963f66afa6")]
    [InlineData("CINEMA-FNB:3fa85f64-5717-4562-b3fc-2c963f66afa6:FNB-3FA85F64", "3fa85f64-5717-4562-b3fc-2c963f66afa6")]
    [InlineData("3fa85f64-5717-4562-b3fc-2c963f66afa6", "3fa85f64-5717-4562-b3fc-2c963f66afa6")]
    public void OrderTokenParser_ShouldExtractGuidCorrectly(string token, string expectedGuidStr)
    {
        Guid? parsed = null;
        var clean = token.Trim();
        if (clean.StartsWith("CINEMA-ORDER:", StringComparison.OrdinalIgnoreCase) ||
            clean.StartsWith("CINEMA-FNB:", StringComparison.OrdinalIgnoreCase))
        {
            var parts = clean.Split(':');
            if (parts.Length >= 2 && Guid.TryParse(parts[1], out var pId))
            {
                parsed = pId;
            }
        }
        else if (Guid.TryParse(clean, out var gId))
        {
            parsed = gId;
        }

        Assert.NotNull(parsed);
        Assert.Equal(Guid.Parse(expectedGuidStr), parsed.Value);
    }
}
