using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Reservation.Api.Models;
using Reservation.Api.Services;

namespace Reservation.Api.Endpoints;

public static class ReservationEndpoints
{
    public static void MapReservationEndpoints(this IEndpointRouteBuilder routes)
    {
        var reservations = routes.MapGroup("/api/v1/reservations")
            .WithTags("Reservations & Seat Locking")
            .RequireAuthorization();

        reservations.MapGet("/health-contract", () => Results.Ok(new 
        { 
            schema = "reservations", 
            lockScript = "infra/redis/lua/acquire-seat-hold.lua", 
            releaseScript = "infra/redis/lua/release-seat-hold.lua",
            maxSeatsPerHold = 10, 
            holdTtlMinutes = 10,
            doubleBookingGuard = "PostgreSQL UNIQUE (showtime_id, seat_id) constraint in reservations.confirmed_seats"
        }))
        .WithSummary("Reservation Service Architectural Contract")
        .WithDescription("Defines concurrency rules: 10-minute hold TTL, Redis Lua locking script, idempotency keys, and monotonic fencing tokens.");

        reservations.MapPost("/holds", async (
            [FromHeader(Name = "X-Idempotency-Key")] Guid? idempotencyKeyHeader, 
            [FromBody] AcquireHoldRequest request,
            ISeatHoldService holdService) =>
        {
            return await holdService.AcquireHoldAsync(request, idempotencyKeyHeader);
        })
        .AllowAnonymous()
        .WithSummary("Acquire Atomic Seat Hold")
        .WithDescription("Acquires a 10-minute temporary seat hold for 1-10 seats using Redis Lua distributed locking.");

        reservations.MapDelete("/holds/{holdId:guid}", async (
            Guid holdId, 
            [FromQuery] Guid? showtimeId,
            [FromQuery] Guid? seatId,
            [FromQuery] string? reason,
            ISeatHoldService holdService) =>
        {
            return await holdService.ReleaseHoldAsync(holdId, showtimeId, seatId, reason);
        })
        .AllowAnonymous()
        .WithSummary("Release Seat Hold")
        .WithDescription("Releases held seats prior to expiration using release-seat-hold.lua, verifying hold ownership.");

        reservations.MapPost("/confirm", async (
            [FromBody] ConfirmReservationRequest request,
            IReservationConfirmationService confirmationService) =>
        {
            return await confirmationService.ConfirmReservationAsync(request);
        })
        .WithSummary("Confirm Seat Reservation (ACID Write)")
        .WithDescription("Finalizes seat booking in PostgreSQL primary database under transactional lock. Prevents double-booking via UNIQUE (showtime_id, seat_id) and enforces monotonic fencing tokens.");

        reservations.MapGet("/{reservationId:guid}", async (
            Guid reservationId,
            IReservationQueryService queryService) =>
        {
            return await queryService.GetReservationDetailsAsync(reservationId);
        })
        .WithSummary("Get Reservation Details")
        .WithDescription("Fetches booking information by reservation ID with Read-After-Write consistency routing.");
    }
}
