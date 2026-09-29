using Microsoft.AspNetCore.Http;
using Reservation.Api.Models;

namespace Reservation.Api.Services;

public interface ISeatHoldService
{
    Task<IResult> AcquireHoldAsync(AcquireHoldRequest request, Guid? idempotencyKeyHeader);
    Task<IResult> ReleaseHoldAsync(Guid holdId, Guid? showtimeId, Guid? seatId, string? reason);
}

public interface IReservationConfirmationService
{
    Task<IResult> ConfirmReservationAsync(ConfirmReservationRequest request);
}

public interface IReservationQueryService
{
    Task<IResult> GetReservationDetailsAsync(Guid reservationId);
}
