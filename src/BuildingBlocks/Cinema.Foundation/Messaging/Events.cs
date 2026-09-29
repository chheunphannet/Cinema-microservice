namespace Cinema.Foundation.Messaging;

public sealed record ReservationConfirmedIntegrationEvent(
    Guid ReservationId,
    Guid ShowtimeId,
    List<Guid> SeatIds,
    decimal TotalAmount,
    DateTimeOffset OccurredAt
);

public sealed record PaymentCapturedIntegrationEvent(
    Guid PaymentId,
    Guid OrderId,
    string Method,
    decimal Amount,
    DateTimeOffset OccurredAt
);

public sealed record TicketRedeemedIntegrationEvent(
    Guid TicketId,
    string TerminalCode,
    DateTimeOffset OccurredAt
);

public sealed record TicketIssuedIntegrationEvent(
    Guid OrderId,
    Guid ReservationId,
    string CustomerEmail,
    string? CustomerPhone,
    string? CustomerName,
    string MovieTitle,
    string BranchName,
    string AuditoriumName,
    DateTimeOffset ShowtimeStart,
    List<TicketSeatItem> Seats,
    decimal TotalAmount,
    DateTimeOffset IssuedAt,
    string? FoodAndBeverage = null,
    int? BookingNumber = null,
    string? BookingReference = null
);

public sealed record TicketSeatItem(
    Guid TicketId,
    string RowLabel,
    int SeatNumber,
    string SeatType,
    string QrToken
);

public sealed record CustomerRegisteredIntegrationEvent(
    Guid CustomerId,
    string Email,
    string FirstName,
    string LastName,
    string? Phone,
    DateTimeOffset RegisteredAt
);

