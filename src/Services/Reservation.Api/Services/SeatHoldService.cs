using Cinema.Foundation.Redis;
using Microsoft.AspNetCore.Http;
using Reservation.Api.Models;
using Reservation.Api.Repositories;

namespace Reservation.Api.Services;

public class SeatHoldService : ISeatHoldService
{
    private readonly IReservationRepository _repository;
    private readonly ISeatLockService _lockService;

    public SeatHoldService(IReservationRepository repository, ISeatLockService lockService)
    {
        _repository = repository;
        _lockService = lockService;
    }

    public async Task<IResult> AcquireHoldAsync(AcquireHoldRequest request, Guid? idempotencyKeyHeader)
    {
        if (request.SeatIds == null || request.SeatIds.Count == 0)
        {
            return Results.BadRequest(new { error = "At least one seat must be selected." });
        }

        bool isGuest = !string.IsNullOrWhiteSpace(request.GuestEmail) || request.CustomerId == null;
        int maxAllowedSeats = isGuest ? 8 : 10;
        if (request.SeatIds.Count > maxAllowedSeats)
        {
            return Results.BadRequest(new 
            { 
                error = isGuest 
                    ? "Guest seat hold cannot exceed 8 seats per booking." 
                    : "SeatIds count must be between 1 and 10." 
            });
        }

        var effectiveKey = idempotencyKeyHeader ?? request.IdempotencyKey ?? Guid.NewGuid();

        // 1. Idempotency Check: Return existing active hold if already created for this key
        var existing = await _repository.FindActiveReservationByIdempotencyKeyAsync(effectiveKey);
        if (existing != null)
        {
            string existingStatus = (string)existing.status;
            DateTimeOffset? existingExpiry = (DateTimeOffset?)existing.hold_expires_at;

            if (existingStatus == "hold" && (existingExpiry == null || existingExpiry > DateTimeOffset.UtcNow))
            {
                var existingSeats = await _repository.GetReservationSeatsAsync((Guid)existing.reservation_id);
                return Results.Ok(new HoldResponse(
                    HoldId: (Guid)existing.reservation_id,
                    ShowtimeId: (Guid)existing.showtime_id,
                    SeatIds: existingSeats.Count > 0 ? existingSeats : request.SeatIds,
                    IdempotencyKey: effectiveKey,
                    FencingToken: (long)existing.fencing_token,
                    HoldExpiresAt: existingExpiry ?? DateTimeOffset.UtcNow.AddMinutes(isGuest ? 8 : 10),
                    Status: "held",
                    Message: "Existing hold retrieved via idempotency key"
                ));
            }

            if (existingStatus == "confirmed")
            {
                return Results.Conflict(new { error = "Reservation for this idempotency key is already confirmed." });
            }
        }

        var holdId = Guid.NewGuid();
        var fencingToken = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        // Phase 3 Spec: 8-minute TTL for guest holds, 10-minute for staff POS
        var ttl = isGuest ? TimeSpan.FromMinutes(8) : TimeSpan.FromMinutes(10);
        var expiresAt = DateTimeOffset.UtcNow.Add(ttl);

        var alreadyBooked = await _repository.GetAlreadyBookedSeatsAsync(request.ShowtimeId, request.SeatIds);
        if (alreadyBooked.Count > 0)
        {
            return Results.Conflict(new 
            { 
                error = "One or more requested seats are already confirmed/sold.",
                conflictingSeats = alreadyBooked
            });
        }

        var blockedSeats = await _repository.GetBlockedSeatsAsync(request.ShowtimeId, request.SeatIds);
        if (blockedSeats.Count > 0)
        {
            return Results.Conflict(new 
            { 
                error = "One or more requested seats are blocked for maintenance or reserved by management.",
                blockedSeats = blockedSeats
            });
        }

        // --- PHASE 2: ORPHAN SEAT PREVENTION ---
        var rowContext = await _repository.GetRowSeatsContextAsync(request.ShowtimeId, request.SeatIds);
        var seatStates = new List<SeatState>();
        foreach (var r in rowContext)
        {
            Guid sid = (Guid)r.seatid;
            string rowLabel = (string)r.rowlabel;
            int number = (int)r.seatnumber;
            bool isActive = (bool)r.isactive;
            bool isBooked = (bool)r.isbooked;

            bool isOccupied = !isActive || isBooked;

            if (!isOccupied) 
            {
                var holdVal = await _lockService.GetSeatHoldAsync(request.ShowtimeId, sid);
                if (!string.IsNullOrEmpty(holdVal))
                {
                    isOccupied = true;
                }
            }

            seatStates.Add(new SeatState(sid, rowLabel, number, isOccupied));
        }

        bool leavesOrphan = SeatAllocationValidator.LeavesNewOrphanSeat(seatStates, request.SeatIds.ToHashSet());
        if (leavesOrphan)
        {
            return Results.BadRequest(new { error = "This selection leaves a single empty seat (orphan seat). Please adjust your selection.", code = "ORPHAN_SEAT_VIOLATION" });
        }
        // ---------------------------------------

        var acquiredSeats = new List<Guid>();
        foreach (var seatId in request.SeatIds)
        {
            var (success, lockValue) = await _lockService.AcquireSeatHoldAsync(
                request.ShowtimeId, seatId, holdId, fencingToken, ttl);

            if (!success)
            {
                foreach (var lockedSeat in acquiredSeats)
                {
                    await _lockService.ReleaseSeatHoldAsync(request.ShowtimeId, lockedSeat, $"{holdId}:{fencingToken}");
                }

                return Results.Conflict(new
                {
                    error = "Seat is currently held by another customer.",
                    conflictingSeatId = seatId,
                    existingLock = lockValue
                });
            }

            acquiredSeats.Add(seatId);
        }

        await _repository.CreateHoldAsync(holdId, request.ShowtimeId, request.CustomerId, expiresAt, effectiveKey, fencingToken, request.EffectiveEmail, request.GuestPhone, request.GuestName);
        
        var tickets = request.Tickets ?? new Dictionary<Guid, Guid>();
        // Fallback for older clients that don't pass TicketTypes
        if (request.Tickets == null)
        {
            var defaultAdultTicketId = Guid.Parse("a1111111-1111-1111-1111-111111111111");
            foreach(var s in request.SeatIds)
            {
                tickets[s] = defaultAdultTicketId;
            }
        }

        await _repository.InsertHoldSeatsAsync(holdId, request.ShowtimeId, request.SeatIds, tickets);

        return Results.Created($"/api/v1/reservations/holds/{holdId}", new HoldResponse(
            HoldId: holdId,
            ShowtimeId: request.ShowtimeId,
            SeatIds: request.SeatIds,
            IdempotencyKey: effectiveKey,
            FencingToken: fencingToken,
            HoldExpiresAt: expiresAt,
            Status: "held",
            Message: isGuest 
                ? "Guest seat lock acquired atomically via Redis Lua script (8-minute TTL)" 
                : "Seat lock acquired atomically via Redis Lua script (10-minute TTL)"
        ));
    }

    public async Task<IResult> ReleaseHoldAsync(Guid holdId, Guid? showtimeId, Guid? seatId, string? reason)
    {
        bool anyReleased = false;

        // Also release specific seat if explicitly provided in query parameters
        if (showtimeId.HasValue && seatId.HasValue)
        {
            var r = await _lockService.ReleaseSeatHoldAsync(showtimeId.Value, seatId.Value, holdId.ToString());
            if (r) anyReleased = true;
        }

        try
        {
            var details = await _repository.GetReservationDetailsAsync(holdId);
            if (details != null && details.Count > 0)
            {
                foreach (var d in details)
                {
                    IDictionary<string, object>? dict = d as IDictionary<string, object>;
                    Guid? stId = null;
                    Guid? sid = null;

                    if (dict != null)
                    {
                        foreach (var kvp in dict)
                        {
                            if (kvp.Key.Equals("ShowtimeId", StringComparison.OrdinalIgnoreCase) && kvp.Value is Guid g1)
                                stId = g1;
                            else if (kvp.Key.Equals("SeatId", StringComparison.OrdinalIgnoreCase) && kvp.Value is Guid g2)
                                sid = g2;
                        }
                    }

                    if (stId.HasValue && sid.HasValue)
                    {
                        var r = await _lockService.ReleaseSeatHoldAsync(stId.Value, sid.Value, holdId.ToString());
                        if (r) anyReleased = true;
                    }
                }
            }
        }
        catch (Exception)
        {
        }

        await _repository.CancelHoldAsync(holdId);

        return Results.Ok(new
        {
            holdId,
            released = true,
            status = "released",
            releasedLock = anyReleased,
            reason = reason ?? "User cancelled hold",
            releasedAt = DateTimeOffset.UtcNow
        });
    }
}
