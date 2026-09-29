using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cinema.Foundation.Messaging;
using Microsoft.AspNetCore.Http;
using Ticket.Api.Models;
using Ticket.Api.Repositories;

namespace Ticket.Api.Services;

public class TicketRedemptionService : ITicketRedemptionService
{
    private readonly ITicketRepository _repository;
    private readonly IEventBus _eventBus;

    public TicketRedemptionService(ITicketRepository repository, IEventBus eventBus)
    {
        _repository = repository;
        _eventBus = eventBus;
    }

    public async Task<IResult> RedeemTicketAsync(RedeemTicketRequest request)
    {
        string hashCandidate = request.QrToken.Length == 64 
            ? request.QrToken.ToUpperInvariant() 
            : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.QrToken)));

        var ticket = await _repository.GetTicketForRedemptionAsync(hashCandidate, request.QrToken);
        if (ticket == null)
        {
            return Results.NotFound(new { error = "Ticket not found with provided barcode/QR token." });
        }

        Guid ticketId = (Guid)ticket.ticket_id;
        string currentStatus = (string)ticket.status;

        if (currentStatus.Equals("used", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Conflict(new
            {
                error = "Ticket has already been redeemed.",
                ticketId,
                status = "used",
                redeemedAt = (DateTimeOffset?)ticket.redeemed_at
            });
        }

        if (currentStatus.Equals("void", StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new { error = "Ticket has been voided and cannot be used for admission.", ticketId });
        }

        var success = await _repository.MarkTicketRedeemedAsync(ticketId);
        if (!success)
        {
            return Results.Conflict(new
            {
                error = "Ticket was redeemed concurrently.",
                ticketId,
                status = "used"
            });
        }

        string terminalCode = request.TerminalCode ?? Constants.DefaultGateId;
        string metadata = JsonSerializer.Serialize(new { redeemedAt = DateTimeOffset.UtcNow, gateId = request.GateId });
        await _repository.LogAuditAsync(ticketId, "gate_redemption", terminalCode, metadata);

        await _eventBus.PublishAsync("ticket.redeemed", new TicketRedeemedIntegrationEvent(
            ticketId,
            terminalCode,
            DateTimeOffset.UtcNow));

        return Results.Ok(new
        {
            redemptionId = Guid.NewGuid(),
            ticketId,
            request.QrToken,
            request.TerminalCode,
            status = "admitted",
            message = "Ticket validated. Gate turnstile open signal dispatched.",
            redeemedAt = DateTimeOffset.UtcNow
        });
    }
}
