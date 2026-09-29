namespace Ticket.Api.Models;

public sealed record TicketDto(
    Guid TicketId, 
    Guid ReservationId, 
    Guid ShowtimeId, 
    Guid SeatId, 
    string QrTokenHash, 
    string Status, 
    bool IsPrinted, 
    DateTimeOffset? PrintedAt, 
    DateTimeOffset? RedeemedAt, 
    DateTimeOffset CreatedAt);
