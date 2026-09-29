using System;
using System.Collections.Generic;

namespace Reservation.Api.Models;

public sealed record AdminBookingSearchFilter(
    string? Query = null,
    Guid? BranchId = null,
    string? Status = null,
    DateTimeOffset? FromDate = null,
    DateTimeOffset? ToDate = null,
    int Page = 1,
    int PageSize = 20
);

public class AdminBookingSearchResultItem
{
    public Guid ReservationId { get; set; }
    public Guid? OrderId { get; set; }
    public string? BookingReference { get; set; }
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public string MovieTitle { get; set; } = string.Empty;
    public string AuditoriumName { get; set; } = string.Empty;
    public DateTimeOffset ShowtimeStart { get; set; }
    public int SeatsCount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }

    public AdminBookingSearchResultItem() { }
}

public sealed record AdminBookingSearchResponse(
    List<AdminBookingSearchResultItem> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);

public sealed record AdminBookingBranchDto(
    Guid BranchId,
    string Code,
    string Name,
    string? Address,
    string Timezone
);

public sealed record AdminBookingAuditoriumDto(
    Guid AuditoriumId,
    string Name
);

public sealed record AdminBookingMovieDto(
    Guid MovieId,
    string Title,
    int DurationMinutes,
    string? Genre,
    string? Classification,
    string? PosterUrl
);

public sealed record AdminBookingShowtimeDto(
    Guid ShowtimeId,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    decimal BasePrice
);

public sealed record AdminBookingCustomerDto(
    Guid? CustomerId,
    string? Name,
    string? Email,
    string? Phone,
    bool IsGuest
);

public class AdminBookingSeatDto
{
    public Guid SeatId { get; set; }
    public string RowLabel { get; set; } = string.Empty;
    public int SeatNumber { get; set; }
    public string SeatType { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Status { get; set; } = string.Empty;

    public AdminBookingSeatDto() { }
}

public class AdminBookingTicketDto
{
    public Guid TicketId { get; set; }
    public Guid SeatId { get; set; }
    public string QrTokenHash { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsPrinted { get; set; }
    public DateTimeOffset? PrintedAt { get; set; }
    public DateTimeOffset? RedeemedAt { get; set; }
    public bool EmailSent { get; set; }
    public DateTimeOffset? EmailSentAt { get; set; }
    public string? EmailRecipient { get; set; }

    public AdminBookingTicketDto() { }
}

public class AdminBookingConcessionDto
{
    public Guid OrderLineId { get; set; }
    public Guid? ProductId { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public bool IsFulfilled { get; set; }
    public DateTimeOffset? FulfilledAt { get; set; }
    public string FulfillmentStatus { get; set; } = "pending";

    public AdminBookingConcessionDto() { }
}

public class AdminBookingPaymentDto
{
    public Guid PaymentId { get; set; }
    public string Method { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? ProviderReference { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset PaidAt { get; set; }

    public AdminBookingPaymentDto() { }
}

public sealed record AdminBookingFinancialsDto(
    decimal Subtotal,
    decimal DiscountAmount,
    decimal TotalAmount,
    decimal TotalPaid,
    decimal TotalRefunded,
    decimal NetRevenue,
    List<AdminBookingPaymentDto> Payments
);

public class AdminBookingRefundDto
{
    public Guid RefundId { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? ReservationId { get; set; }
    public decimal RefundAmount { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public Guid AuthorizedBy { get; set; }
    public DateTimeOffset RefundedAt { get; set; }

    public AdminBookingRefundDto() { }
}

public class AdminBookingDisputeDto
{
    public Guid DisputeId { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? ReservationId { get; set; }
    public string? ProviderDisputeId { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? EvidenceNotes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }

    public AdminBookingDisputeDto() { }
}

public sealed record AdminBookingDetailResponse(
    Guid ReservationId,
    Guid? OrderId,
    string? BookingReference,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ConfirmedAt,
    AdminBookingBranchDto Branch,
    AdminBookingAuditoriumDto Auditorium,
    AdminBookingMovieDto Movie,
    AdminBookingShowtimeDto Showtime,
    AdminBookingCustomerDto Customer,
    List<AdminBookingSeatDto> Seats,
    List<AdminBookingTicketDto> Tickets,
    List<AdminBookingConcessionDto> Concessions,
    AdminBookingFinancialsDto Financials,
    List<AdminBookingRefundDto> Refunds,
    List<AdminBookingDisputeDto> Disputes
);

public sealed record RefundBookingRequest(
    Guid? ReservationId = null,
    List<Guid>? SeatIds = null,
    List<Guid>? OrderLineIds = null,
    decimal? RefundAmount = null,
    string ReasonCode = "customer_request",
    string? Notes = null
);

public sealed record RefundBookingResponse(
    Guid RefundId,
    Guid ReservationId,
    Guid? OrderId,
    decimal RefundAmount,
    decimal TotalRefunded,
    decimal RemainingAmount,
    string ReservationStatus,
    List<Guid> RefundedSeatIds,
    List<Guid> RemainingSeatIds,
    List<Guid> RefundedOrderLineIds,
    DateTimeOffset RefundedAt,
    Guid AuthorizedBy,
    Guid? ShowtimeId = null
);

public sealed record ReissueTicketRequest(
    Guid? ReservationId = null,
    string? Email = null,
    bool RegenerateQrTokens = false,
    string? Reason = null
);

public sealed record ReissueTicketResponse(
    bool Success,
    Guid ReservationId,
    string RecipientEmail,
    int TicketsCount,
    bool QrTokensRegenerated,
    DateTimeOffset SentAt,
    string Message
);

public sealed record FlagDisputeRequest(
    Guid? ReservationId = null,
    string? ProviderDisputeId = null,
    decimal Amount = 0,
    string? EvidenceNotes = null,
    string Status = "open"
);

public sealed record FlagDisputeResponse(
    Guid DisputeId,
    Guid ReservationId,
    Guid? OrderId,
    string? ProviderDisputeId,
    string Status,
    decimal Amount,
    string? EvidenceNotes,
    DateTimeOffset CreatedAt
);
