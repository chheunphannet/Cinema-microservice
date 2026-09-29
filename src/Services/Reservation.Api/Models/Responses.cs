namespace Reservation.Api.Models;

public sealed record HoldResponse(Guid HoldId, Guid ShowtimeId, List<Guid> SeatIds, Guid IdempotencyKey, long FencingToken, DateTimeOffset HoldExpiresAt, string Status, string Message);
public sealed record ReservationConfirmationResponse(Guid ReservationId, Guid ShowtimeId, List<Guid> ConfirmedSeatIds, string Status, DateTimeOffset ConfirmedAt, decimal TotalAmount);
