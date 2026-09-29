using System.Security.Cryptography;
using System.Text;
using Cinema.Foundation.Email;
using Cinema.Foundation.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Ticket.Api.Models;
using Ticket.Api.Repositories;

namespace Ticket.Api.Services;

public class TicketQueryService : ITicketQueryService
{
    private readonly ITicketRepository _repository;
    private readonly IConfiguration _config;

    public TicketQueryService(ITicketRepository repository, IConfiguration config)
    {
        _repository = repository;
        _config = config;
    }

    public async Task<IResult> GetTicketDetailsAsync(Guid ticketId)
    {
        var row = await _repository.GetTicketDetailsAsync(ticketId);
        if (row == null)
        {
            return Results.NotFound(new { error = $"Ticket {ticketId} not found." });
        }

        return Results.Ok(new
        {
            ticketId = (Guid)row.ticket_id,
            reservationId = (Guid)row.reservation_id,
            showtimeId = (Guid)row.showtime_id,
            seatId = (Guid)row.seat_id,
            status = (string)row.status,
            isPrinted = (bool)row.is_printed,
            printedAt = (DateTimeOffset?)row.printed_at,
            redeemedAt = (DateTimeOffset?)row.redeemed_at,
            movie = (string?)row.movie_title ?? Constants.DefaultMovieTitle,
            auditorium = (string?)row.auditorium_name ?? Constants.DefaultAuditorium,
            seat = row.row_label != null ? $"Row {row.row_label}, Seat {row.seat_number}" : Constants.DefaultSeat,
            branch = (string?)row.branch_name ?? Constants.DefaultBranch,
            createdAt = (DateTimeOffset)row.created_at
        });
    }

    public async Task<IResult> GenerateETicketAsync(Guid ticketId)
    {
        var row = await _repository.GetETicketInfoAsync(ticketId);
        if (row == null)
        {
            return Results.NotFound(new { error = $"Ticket {ticketId} not found." });
        }

        string orderId = row.order_id != null 
            ? $"ORD-{((Guid)row.order_id).ToString("N")[..4].ToUpperInvariant()}" 
            : $"ORD-{ticketId.ToString("N")[..4].ToUpperInvariant()}";
            
        string seatInfo = $"{row.auditorium_name}, Row {row.row_label}, Seat {row.seat_number}";

        var secretKey = SignedTicketUrlService.GetSecretKey(_config);
        var rawPayload = $"{orderId}:{seatInfo}:{ticketId}";
        
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
        var signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawPayload));
        var signature = Convert.ToHexString(signatureBytes).ToLowerInvariant();

        return Results.Ok(new
        {
            orderId,
            seatInfo,
            sig = $"HMAC-SHA256({signature})"
        });
    }

    public async Task<IResult> GetSignedETicketPassAsync(Guid ticketId, long exp, string? sig)
    {
        var secretKey = SignedTicketUrlService.GetSecretKey(_config);
        bool isValid = SignedTicketUrlService.ValidateSignature(ticketId, exp, sig, secretKey);
        if (!isValid)
        {
            return Results.Json(new 
            { 
                error = "Access denied. The ticket pass link signature is invalid, expired, or tampered with.",
                ticketId,
                status = "unauthorized"
            }, statusCode: StatusCodes.Status403Forbidden);
        }

        var row = await _repository.GetTicketDetailsAsync(ticketId);
        if (row == null)
        {
            return Results.NotFound(new { error = $"Ticket {ticketId} not found." });
        }

        string rawQr = $"CINEMA-TICKET:{ticketId}:{(Guid)row.reservation_id}:{(Guid)row.seat_id}";
        string qrSvg = QrCodeHelper.GenerateSvg(rawQr, 8);
        string qrPngDataUri = QrCodeHelper.GeneratePngBase64DataUri(rawQr, 8);

        var reservationId = (Guid)row.reservation_id;
        var siblingTickets = await _repository.GetTicketsByReservationIdAsync(reservationId);
        var publicGatewayUrl = SignedTicketUrlService.GetPublicGatewayUrl(_config);

        var partyPasses = siblingTickets.Select(t =>
        {
            var tid = (Guid)t.ticket_id;
            var sid = (Guid)t.seat_id;
            string rawPassQr = $"CINEMA-TICKET:{tid}:{reservationId}:{sid}";
            string seatLabel = t.row_label != null ? $"Row {t.row_label}, Seat {t.seat_number}" : Constants.DefaultSeat;
            string seatType = (string?)t.seat_type ?? "standard";
            string passUrl = SignedTicketUrlService.BuildSignedETicketUrl(publicGatewayUrl, tid, TimeSpan.FromDays(7), secretKey);

            return new
            {
                ticketId = tid,
                seat = seatLabel,
                seatType,
                status = (string)t.status,
                qrToken = rawPassQr,
                qrSvg = QrCodeHelper.GenerateSvg(rawPassQr, 8),
                qrPngDataUri = QrCodeHelper.GeneratePngBase64DataUri(rawPassQr, 8),
                passUrl,
                isCurrentPass = tid == ticketId
            };
        }).ToList();

        return Results.Ok(new
        {
            ticketId = (Guid)row.ticket_id,
            reservationId,
            status = (string)row.status,
            movie = (string?)row.movie_title ?? Constants.DefaultMovieTitle,
            auditorium = (string?)row.auditorium_name ?? Constants.DefaultAuditorium,
            branch = (string?)row.branch_name ?? Constants.DefaultBranch,
            seat = row.row_label != null ? $"Row {row.row_label}, Seat {row.seat_number}" : Constants.DefaultSeat,
            totalSeatsInParty = partyPasses.Count,
            partySeats = string.Join(", ", partyPasses.Select(p => p.seat)),
            qrToken = rawQr,
            qrSvg,
            qrPngDataUri,
            expiresAt = DateTimeOffset.FromUnixTimeSeconds(exp),
            admitStatus = ((string)row.status) == "active" ? "READY_FOR_ADMISSION" : ((string)row.status).ToUpperInvariant(),
            partyPasses,
            instructions = "Present this QR code on your mobile screen to the turnstile scanner at cinema entrance. Swipe through partyPasses to admit other members of your booking."
        });
    }

    public async Task<IResult> GetSignedReservationPassAsync(Guid reservationId, long exp, string? sig)
    {
        var secretKey = SignedTicketUrlService.GetSecretKey(_config);
        bool isValid = SignedTicketUrlService.ValidateReservationSignature(reservationId, exp, sig, secretKey);
        if (!isValid)
        {
            return Results.Json(new 
            { 
                error = "Access denied. The reservation pass link signature is invalid, expired, or tampered with.",
                reservationId,
                status = "unauthorized"
            }, statusCode: StatusCodes.Status403Forbidden);
        }

        var tickets = await _repository.GetTicketsByReservationIdAsync(reservationId);
        if (tickets == null || tickets.Count == 0)
        {
            return Results.NotFound(new { error = $"No tickets found for reservation {reservationId}." });
        }

        var publicGatewayUrl = SignedTicketUrlService.GetPublicGatewayUrl(_config);
        var passes = tickets.Select(t =>
        {
            var tid = (Guid)t.ticket_id;
            var sid = (Guid)t.seat_id;
            string rawPassQr = $"CINEMA-TICKET:{tid}:{reservationId}:{sid}";
            string seatLabel = t.row_label != null ? $"Row {t.row_label}, Seat {t.seat_number}" : Constants.DefaultSeat;
            string seatType = (string?)t.seat_type ?? "standard";
            string passUrl = SignedTicketUrlService.BuildSignedETicketUrl(publicGatewayUrl, tid, TimeSpan.FromDays(7), secretKey);

            return new
            {
                ticketId = tid,
                seat = seatLabel,
                seatType,
                status = (string)t.status,
                qrToken = rawPassQr,
                qrSvg = QrCodeHelper.GenerateSvg(rawPassQr, 8),
                qrPngDataUri = QrCodeHelper.GeneratePngBase64DataUri(rawPassQr, 8),
                passUrl
            };
        }).ToList();

        var first = tickets[0];
        return Results.Ok(new
        {
            reservationId,
            totalTickets = passes.Count,
            movie = (string?)first.movie_title ?? Constants.DefaultMovieTitle,
            auditorium = (string?)first.auditorium_name ?? Constants.DefaultAuditorium,
            branch = (string?)first.branch_name ?? Constants.DefaultBranch,
            showtime = (DateTimeOffset?)first.starts_at,
            expiresAt = DateTimeOffset.FromUnixTimeSeconds(exp),
            passes,
            instructions = "Present each QR code to the turnstile scanner at cinema entrance for admission."
        });
    }
}

