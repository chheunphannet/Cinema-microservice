namespace Reservation.Api.Models;

public sealed record AcquireHoldRequest(
    Guid ShowtimeId, 
    List<Guid> SeatIds, 
    Dictionary<Guid, Guid>? Tickets = null,
    Guid? CustomerId = null, 
    Guid? IdempotencyKey = null,
    string? GuestEmail = null,
    string? GuestPhone = null,
    string? GuestName = null,
    string? CustomerEmail = null
)
{
    public string? EffectiveEmail => !string.IsNullOrWhiteSpace(GuestEmail) ? GuestEmail : CustomerEmail;
};
public sealed record ConfirmReservationRequest(Guid HoldId, Guid ShowtimeId, List<Guid> SeatIds, Dictionary<Guid, Guid>? Tickets, Guid IdempotencyKey, long FencingToken);
