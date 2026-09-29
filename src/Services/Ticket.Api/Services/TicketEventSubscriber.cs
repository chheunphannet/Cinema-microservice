using Cinema.Foundation.Data;
using Cinema.Foundation.Email;
using Cinema.Foundation.Messaging;
using Cinema.Foundation.Security;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ticket.Api.Services;

public class TicketEventSubscriber : BackgroundService
{
    private readonly IEventBus _eventBus;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TicketEventSubscriber> _logger;

    public TicketEventSubscriber(
        IEventBus eventBus, 
        IServiceScopeFactory scopeFactory, 
        ILogger<TicketEventSubscriber> logger)
    {
        _eventBus = eventBus;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(1000, stoppingToken);

        // 1. Subscribe to ReservationConfirmedIntegrationEvent
        await _eventBus.SubscribeAsync<ReservationConfirmedIntegrationEvent>(
            "ticket.reservation-confirmed",
            "reservation.confirmed",
            async (@event) =>
            {
                _logger.LogInformation("Ticket Service received ReservationConfirmed event: ReservationId={ReservationId}, Showtime={ShowtimeId}, Seats={SeatsCount}",
                    @event.ReservationId, @event.ShowtimeId, @event.SeatIds.Count);
                await Task.CompletedTask;
            });

        // 2. Subscribe to PaymentCapturedIntegrationEvent
        await _eventBus.SubscribeAsync<PaymentCapturedIntegrationEvent>(
            "ticket.payment-success",
            "payment.*",
            async (@event) =>
            {
                _logger.LogInformation("Ticket Service received PaymentCaptured event for PaymentId={PaymentId}, OrderId={OrderId}",
                    @event.PaymentId, @event.OrderId);
                await Task.CompletedTask;
            });

        // 3. Subscribe to TicketIssuedIntegrationEvent (Phase 3 Email & Digital Ticket Dispatcher)
        await _eventBus.SubscribeAsync<TicketIssuedIntegrationEvent>(
            "ticket.email-notifications",
            "ticket.issued",
            async (@event) =>
            {
                _logger.LogInformation("Ticket Notification Worker processing TicketIssued event for Order {OrderId}, Recipient {Email}",
                    @event.OrderId, @event.CustomerEmail);

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
                    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

                    var publicGatewayUrl = SignedTicketUrlService.GetPublicGatewayUrl(configuration);
                    var hmacSecret = SignedTicketUrlService.GetSecretKey(configuration);

                    var orderRef = @event.BookingReference ?? $"#ORD-{@event.OrderId.ToString("N")[..6].ToUpperInvariant()}";

                    // Build multi-seat passes with individual QR tokens & signed URLs
                    var passes = new List<TicketPassItem>();
                    foreach (var s in @event.Seats)
                    {
                        var signedPassUrl = SignedTicketUrlService.BuildSignedETicketUrl(
                            publicGatewayUrl,
                            s.TicketId,
                            TimeSpan.FromDays(7),
                            hmacSecret
                        );

                        passes.Add(new TicketPassItem(
                            TicketId: s.TicketId,
                            RowLabel: s.RowLabel,
                            SeatNumber: s.SeatNumber,
                            SeatType: s.SeatType,
                            QrToken: s.QrToken,
                            SignedPassUrl: signedPassUrl
                        ));
                    }

                    var logoUrl = configuration["Ticket:LogoUrl"];
                    var logoText = configuration["Ticket:LogoText"] ?? "Legend";
                    var logoColor = configuration["Ticket:LogoColor"] ?? "#dc2626";
                    var slogan = configuration["Ticket:Slogan"] ?? "CINEMA";
                    var backgroundColor = configuration["Ticket:BackgroundColor"] ?? "#fafafa";

                    var htmlBody = TicketHtmlTemplateBuilder.BuildMultiTicketHtml(
                        @event.MovieTitle,
                        @event.BranchName,
                        @event.AuditoriumName,
                        @event.ShowtimeStart,
                        passes,
                        @event.TotalAmount,
                        orderRef,
                        logoUrl,
                        logoText,
                        logoColor,
                        slogan,
                        backgroundColor,
                        customerName: @event.CustomerName,
                        foodAndBeverage: @event.FoodAndBeverage,
                        bookingNumber: @event.BookingNumber?.ToString(),
                        bookingId: @event.BookingReference,
                        orderId: @event.OrderId
                    );

                    var seatDescriptions = string.Join(", ", passes.Select(p => $"Row {p.RowLabel} Seat {p.SeatNumber} ({p.SeatType})"));
                    var plainText = $"Your tickets for {@event.MovieTitle} on {@event.ShowtimeStart:g} at {@event.BranchName} ({@event.AuditoriumName}).\n" +
                                    $"Seats: {seatDescriptions}\n" +
                                    $"Digital Pass Links:\n" +
                                    string.Join("\n", passes.Select(p => $"- Row {p.RowLabel} Seat {p.SeatNumber}: {p.SignedPassUrl}"));

                    // Generate standard RFC 5545 .ics Calendar Attachment
                    var icsBytes = CalendarIcsBuilder.BuildShowtimeIcs(
                        movieTitle: @event.MovieTitle,
                        branchName: @event.BranchName,
                        auditoriumName: @event.AuditoriumName,
                        showtimeStart: @event.ShowtimeStart,
                        duration: TimeSpan.FromHours(2),
                        seatsSummary: seatDescriptions,
                        orderReference: orderRef,
                        orderId: @event.OrderId
                    );

                    var attachments = new List<EmailAttachment>
                    {
                        new EmailAttachment($"{orderRef}-calendar.ics", icsBytes, "text/calendar")
                    };

                    var emailMsg = new EmailMessage(
                        ToEmail: @event.CustomerEmail,
                        ToName: @event.CustomerName ?? "Cinema Guest",
                        Subject: $"🎟️ Your Cinema City E-Tickets: {@event.MovieTitle}",
                        HtmlBody: htmlBody,
                        PlainTextBody: plainText,
                        Attachments: attachments
                    );

                    var sendResult = await emailService.SendAsync(emailMsg, stoppingToken);

                    if (sendResult.Success)
                    {
                        var ticketIds = @event.Seats.Select(s => s.TicketId).ToArray();
                        using var conn = dbFactory.CreateConnection();
                        const string updateSql = @"
                            UPDATE tickets.tickets 
                            SET email_sent = true, 
                                email_sent_at = now(), 
                                email_recipient = @Recipient,
                                email_message_id = @MessageId
                            WHERE ticket_id = ANY(@TicketIds)";

                        await conn.ExecuteAsync(updateSql, new
                        {
                            Recipient = @event.CustomerEmail,
                            MessageId = sendResult.MessageId,
                            TicketIds = ticketIds
                        });

                        _logger.LogInformation("Email notification dispatched via {Provider} to {Recipient} for Order {OrderId}. MessageId: {MessageId}",
                            emailService.ProviderName, @event.CustomerEmail, @event.OrderId, sendResult.MessageId);
                    }
                    else
                    {
                        _logger.LogWarning("Email delivery failed for Order {OrderId} via {Provider}: {Error}",
                            @event.OrderId, emailService.ProviderName, sendResult.ErrorMessage);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error dispatching ticket notification for Order {OrderId}", @event.OrderId);
                }
            });

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("TicketEventSubscriber shutting down gracefully.");
        }
    }
}
