using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Reservation.Api.Models;

namespace Reservation.Api.Repositories;

public interface IAdminBookingRepository
{
    Task<AdminBookingSearchResponse> SearchBookingsAsync(AdminBookingSearchFilter filter);
    Task<AdminBookingDetailResponse?> GetBookingDetailAsync(Guid reservationId);
    Task<(Guid BranchId, string Status, Guid? OrderId)?> GetBookingSummaryAsync(Guid reservationId);
    Task<RefundBookingResponse> ExecuteRefundAsync(
        Guid reservationId,
        List<Guid>? seatIds,
        List<Guid>? orderLineIds,
        decimal? refundAmount,
        string reasonCode,
        string? notes,
        Guid authorizedBy);
    Task<(bool Success, int TicketsCount, List<TicketReissuePassItem> Passes, string? CustomerEmail, string? CustomerName, string MovieTitle, string BranchName, string AuditoriumName, DateTimeOffset ShowtimeStart, decimal TotalAmount, string? BookingRef, Guid? OrderId)> PrepareTicketReissueAsync(Guid reservationId, bool regenerateQrTokens);
    Task MarkTicketsEmailSentAsync(Guid reservationId, string recipientEmail);
    Task<FlagDisputeResponse> FlagDisputeAsync(Guid reservationId, FlagDisputeRequest request);
}

public sealed record TicketReissuePassItem(
    Guid TicketId,
    Guid SeatId,
    string RowLabel,
    int SeatNumber,
    string SeatType,
    string QrTokenHash
);
