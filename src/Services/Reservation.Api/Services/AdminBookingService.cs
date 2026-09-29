using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Cinema.Foundation.Email;
using Cinema.Foundation.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Reservation.Api.Models;
using Reservation.Api.Repositories;
using StackExchange.Redis;

namespace Reservation.Api.Services;

public class AdminBookingService : IAdminBookingService
{
    private readonly IAdminBookingRepository _repository;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminBookingService> _logger;
    private readonly IConnectionMultiplexer? _redis;

    public AdminBookingService(
        IAdminBookingRepository repository,
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<AdminBookingService> logger,
        IConnectionMultiplexer? redis = null)
    {
        _repository = repository;
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
        _redis = redis;
    }

    public async Task<IResult> SearchBookingsAsync(AdminBookingSearchFilter filter, ClaimsPrincipal user)
    {
        var (callerBranchId, _, _, isGlobalSupport) = GetCallerContext(user);

        if (!isGlobalSupport)
        {
            if (!callerBranchId.HasValue)
            {
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Branch Manager must have an assigned branchId claim.");
            }

            if (filter.BranchId.HasValue && filter.BranchId.Value != callerBranchId.Value)
            {
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Cannot access bookings of a different branch.");
            }

            filter = filter with { BranchId = callerBranchId.Value };
        }

        var results = await _repository.SearchBookingsAsync(filter);
        return Results.Ok(results);
    }

    public async Task<IResult> GetBookingDetailAsync(Guid reservationId, ClaimsPrincipal user)
    {
        var (callerBranchId, _, _, isGlobalSupport) = GetCallerContext(user);

        if (!isGlobalSupport)
        {
            var summary = await _repository.GetBookingSummaryAsync(reservationId);
            if (summary == null)
            {
                return Results.NotFound(new { error = $"Booking {reservationId} not found." });
            }

            if (!callerBranchId.HasValue || summary.Value.BranchId != callerBranchId.Value)
            {
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Cannot access booking outside assigned branch.");
            }
        }

        var detail = await _repository.GetBookingDetailAsync(reservationId);
        if (detail == null)
        {
            return Results.NotFound(new { error = $"Booking {reservationId} not found." });
        }

        return Results.Ok(detail);
    }

    public async Task<IResult> RefundBookingAsync(Guid reservationId, RefundBookingRequest request, ClaimsPrincipal user)
    {
        var (callerBranchId, userId, _, isGlobalSupport) = GetCallerContext(user);

        if (!isGlobalSupport)
        {
            var summary = await _repository.GetBookingSummaryAsync(reservationId);
            if (summary == null)
            {
                return Results.NotFound(new { error = $"Booking {reservationId} not found." });
            }

            if (!callerBranchId.HasValue || summary.Value.BranchId != callerBranchId.Value)
            {
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Cannot refund booking outside assigned branch.");
            }
        }

        try
        {
            var refundResult = await _repository.ExecuteRefundAsync(
                reservationId: reservationId,
                seatIds: request.SeatIds,
                orderLineIds: request.OrderLineIds,
                refundAmount: request.RefundAmount,
                reasonCode: string.IsNullOrWhiteSpace(request.ReasonCode) ? "customer_request" : request.ReasonCode.Trim().ToLowerInvariant(),
                notes: request.Notes,
                authorizedBy: userId != Guid.Empty ? userId : Guid.NewGuid()
            );

            await InvalidateRedisShowtimeCacheAsync(reservationId, refundResult.ShowtimeId, refundResult.RefundedSeatIds);

            return Results.Ok(refundResult);
        }
        catch (KeyNotFoundException knf)
        {
            return Results.NotFound(new { error = knf.Message });
        }
        catch (ArgumentException ae)
        {
            return Results.BadRequest(new { error = ae.Message });
        }
        catch (InvalidOperationException ioe)
        {
            return Results.BadRequest(new { error = ioe.Message });
        }
    }

    public async Task<IResult> ReissueTicketAsync(Guid reservationId, ReissueTicketRequest request, ClaimsPrincipal user)
    {
        var (callerBranchId, _, _, isGlobalSupport) = GetCallerContext(user);

        if (!isGlobalSupport)
        {
            var summary = await _repository.GetBookingSummaryAsync(reservationId);
            if (summary == null)
            {
                return Results.NotFound(new { error = $"Booking {reservationId} not found." });
            }

            if (!callerBranchId.HasValue || summary.Value.BranchId != callerBranchId.Value)
            {
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Cannot reissue tickets outside assigned branch.");
            }
        }

        try
        {
            var prep = await _repository.PrepareTicketReissueAsync(reservationId, request.RegenerateQrTokens);
            var recipient = !string.IsNullOrWhiteSpace(request.Email) ? request.Email.Trim() : prep.CustomerEmail;

            if (string.IsNullOrWhiteSpace(recipient) || !recipient.Contains('@') || !recipient.Contains('.'))
            {
                return Results.BadRequest(new { error = "A valid customer email address is required for ticket re-issuance." });
            }

            var publicGatewayUrl = SignedTicketUrlService.GetPublicGatewayUrl(_configuration);
            var hmacSecret = SignedTicketUrlService.GetSecretKey(_configuration);

            var passes = new List<TicketPassItem>();
            foreach (var p in prep.Passes)
            {
                var signedUrl = SignedTicketUrlService.BuildSignedETicketUrl(
                    publicGatewayUrl,
                    p.TicketId,
                    TimeSpan.FromDays(7),
                    hmacSecret
                );

                passes.Add(new TicketPassItem(
                    TicketId: p.TicketId,
                    RowLabel: p.RowLabel,
                    SeatNumber: p.SeatNumber,
                    SeatType: p.SeatType,
                    QrToken: p.QrTokenHash,
                    SignedPassUrl: signedUrl
                ));
            }

            var logoUrl = _configuration["Ticket:LogoUrl"];
            var logoText = _configuration["Ticket:LogoText"] ?? "Legend";
            var logoColor = _configuration["Ticket:LogoColor"] ?? "#dc2626";
            var slogan = _configuration["Ticket:Slogan"] ?? "CINEMA";
            var backgroundColor = _configuration["Ticket:BackgroundColor"] ?? "#fafafa";
            var orderRef = prep.BookingRef ?? $"#BKG-{reservationId.ToString("N")[..8].ToUpperInvariant()}";

            var htmlBody = TicketHtmlTemplateBuilder.BuildMultiTicketHtml(
                prep.MovieTitle,
                prep.BranchName,
                prep.AuditoriumName,
                prep.ShowtimeStart,
                passes,
                prep.TotalAmount,
                orderRef,
                logoUrl,
                logoText,
                logoColor,
                slogan,
                backgroundColor,
                customerName: prep.CustomerName,
                foodAndBeverage: null,
                bookingNumber: null,
                bookingId: orderRef,
                orderId: prep.OrderId
            );

            var seatDescriptions = string.Join(", ", passes.Select(p => $"Row {p.RowLabel} Seat {p.SeatNumber} ({p.SeatType})"));
            var plainText = $"Your tickets for {prep.MovieTitle} on {prep.ShowtimeStart:g} at {prep.BranchName} ({prep.AuditoriumName}).\n" +
                            $"Seats: {seatDescriptions}\n" +
                            $"Digital Pass Links:\n" +
                            string.Join("\n", passes.Select(p => $"- Row {p.RowLabel} Seat {p.SeatNumber}: {p.SignedPassUrl}"));

            var icsBytes = CalendarIcsBuilder.BuildShowtimeIcs(
                movieTitle: prep.MovieTitle,
                branchName: prep.BranchName,
                auditoriumName: prep.AuditoriumName,
                showtimeStart: prep.ShowtimeStart,
                duration: TimeSpan.FromHours(2),
                seatsSummary: seatDescriptions,
                orderReference: orderRef,
                orderId: prep.OrderId ?? reservationId
            );

            var attachments = new List<EmailAttachment>
            {
                new EmailAttachment($"{orderRef}-calendar.ics", icsBytes, "text/calendar")
            };

            var emailMsg = new EmailMessage(
                ToEmail: recipient,
                ToName: prep.CustomerName ?? "Cinema Guest",
                Subject: $"🎟️ Re-Issued: Your Cinema City E-Tickets ({prep.MovieTitle})",
                HtmlBody: htmlBody,
                PlainTextBody: plainText,
                Attachments: attachments
            );

            var sendResult = await _emailService.SendAsync(emailMsg);
            if (sendResult.Success)
            {
                await _repository.MarkTicketsEmailSentAsync(reservationId, recipient);
            }

            return Results.Ok(new ReissueTicketResponse(
                Success: sendResult.Success,
                ReservationId: reservationId,
                RecipientEmail: recipient,
                TicketsCount: prep.TicketsCount,
                QrTokensRegenerated: request.RegenerateQrTokens,
                SentAt: DateTimeOffset.UtcNow,
                Message: sendResult.Success 
                    ? "Tickets successfully re-issued and dispatched via email." 
                    : $"Ticket re-issuance email failed: {sendResult.ErrorMessage}"
            ));
        }
        catch (KeyNotFoundException knf)
        {
            return Results.NotFound(new { error = knf.Message });
        }
        catch (InvalidOperationException ioe)
        {
            return Results.BadRequest(new { error = ioe.Message });
        }
    }

    public async Task<IResult> FlagDisputeAsync(Guid reservationId, FlagDisputeRequest request, ClaimsPrincipal user)
    {
        var (callerBranchId, _, _, isGlobalSupport) = GetCallerContext(user);

        if (!isGlobalSupport)
        {
            var summary = await _repository.GetBookingSummaryAsync(reservationId);
            if (summary == null)
            {
                return Results.NotFound(new { error = $"Booking {reservationId} not found." });
            }

            if (!callerBranchId.HasValue || summary.Value.BranchId != callerBranchId.Value)
            {
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Cannot flag dispute outside assigned branch.");
            }
        }

        if (request.Amount < 0)
        {
            return Results.BadRequest(new { error = "Dispute amount cannot be negative." });
        }

        try
        {
            var result = await _repository.FlagDisputeAsync(reservationId, request);
            return Results.Ok(result);
        }
        catch (KeyNotFoundException knf)
        {
            return Results.NotFound(new { error = knf.Message });
        }
        catch (ArgumentException ae)
        {
            return Results.BadRequest(new { error = ae.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flag dispute for reservation {ReservationId}", reservationId);
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Failed to record dispute.");
        }
    }

    private static (Guid? BranchId, Guid UserId, bool IsSuperAdmin, bool IsGlobalSupport) GetCallerContext(ClaimsPrincipal user)
    {
        var isSuperAdmin = user.IsInRole("super_admin") || user.IsInRole("system_admin");
        var isCustomerSupport = user.IsInRole("customer_support");
        var isFinanceManager = user.IsInRole("finance_manager");
        var isGlobalSupport = isSuperAdmin || isCustomerSupport || isFinanceManager;

        var branchClaim = user.FindFirst("branchId")?.Value ?? user.FindFirst("branch_id")?.Value;
        Guid? branchId = Guid.TryParse(branchClaim, out var b) ? b : null;

        var idClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
        Guid userId = Guid.TryParse(idClaim, out var uid) ? uid : Guid.Empty;

        return (branchId, userId, isSuperAdmin, isGlobalSupport);
    }

    private async Task InvalidateRedisShowtimeCacheAsync(Guid reservationId, Guid? showtimeId, IEnumerable<Guid> seatIds)
    {
        if (_redis == null || !_redis.IsConnected) return;

        try
        {
            var db = _redis.GetDatabase();
            Guid stId;
            if (showtimeId.HasValue && showtimeId.Value != Guid.Empty)
            {
                stId = showtimeId.Value;
            }
            else
            {
                var detail = await _repository.GetBookingDetailAsync(reservationId);
                if (detail == null) return;
                stId = detail.Showtime.ShowtimeId;
            }

            // Real cache keys used by Catalog.Api / SeatMapService & BlockbusterCacheService
            // Batch all key deletions into a single round-trip to avoid N+1 Redis calls
            var keysToDelete = new List<RedisKey>
            {
                new RedisKey($"catalog:seat-matrix:{stId}"),
                new RedisKey($"blockbuster:high-traffic:seat-matrix:{stId}"),
                new RedisKey($"cinema:seats:{stId}"),
                new RedisKey($"showtimes:{stId}:seats")
            };
            foreach (var sid in seatIds)
            {
                keysToDelete.Add(new RedisKey($"seat-hold:{stId}:{sid}"));
            }
            await db.KeyDeleteAsync(keysToDelete.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to invalidate Redis cache for refunded reservation {ReservationId}", reservationId);
        }
    }
}
