using Cinema.Foundation.Data;
using Cinema.Foundation.Messaging;
using Cinema.Foundation.Redis;
using Microsoft.AspNetCore.Http;
using Npgsql;
using Reservation.Api.Models;
using Reservation.Api.Repositories;

namespace Reservation.Api.Services;

public class ReservationConfirmationService : IReservationConfirmationService
{
    private readonly IReservationRepository _repository;
    private readonly IDbConnectionFactory _dbFactory;
    private readonly ISeatLockService _lockService;
    private readonly IEventBus _eventBus;
    private readonly IConfiguration _configuration;

    public ReservationConfirmationService(
        IReservationRepository repository, 
        IDbConnectionFactory dbFactory,
        ISeatLockService lockService, 
        IEventBus eventBus,
        IConfiguration configuration)
    {
        _repository = repository;
        _dbFactory = dbFactory;
        _lockService = lockService;
        _eventBus = eventBus;
        _configuration = configuration;
    }

    public async Task<IResult> ConfirmReservationAsync(ConfirmReservationRequest request)
    {
        if (request.FencingToken <= 0)
        {
            return Results.BadRequest(new { error = "FencingToken must be a positive integer.", code = "INVALID_FENCING_TOKEN" });
        }

        using var conn = (NpgsqlConnection)_dbFactory.CreateConnection();
        await conn.OpenAsync();

        var existing = await _repository.GetReservationByIdempotencyKeyOrHoldIdAsync(request.IdempotencyKey, request.HoldId, conn);

        if (existing != null)
        {
            long lastRecordedToken = (long)(existing.fencing_token ?? 0L);

            if (request.FencingToken < lastRecordedToken)
            {
                return Results.Conflict(new
                {
                    error = "Stale fencing token. Write rejected because fencing token is lower than the last processed token for this resource.",
                    code = "STALE_FENCING_TOKEN",
                    lastRecordedToken,
                    providedToken = request.FencingToken
                });
            }

            if (string.Equals((string)existing.status, Constants.Statuses.Confirmed, StringComparison.OrdinalIgnoreCase))
            {
                decimal totalAmount = 0m;
                var details = await _repository.GetReservationDetailsAsync((Guid)existing.reservation_id);
                if (details != null && details.Count > 0)
                {
                    totalAmount = details.Where(r => r.price != null).Sum(r => (decimal)r.price);
                }

                return Results.Ok(new ReservationConfirmationResponse(
                    ReservationId: (Guid)existing.reservation_id,
                    ShowtimeId: (Guid)existing.showtime_id,
                    ConfirmedSeatIds: request.SeatIds,
                    Status: (string)existing.status,
                    ConfirmedAt: (DateTimeOffset)(existing.confirmed_at ?? DateTimeOffset.UtcNow),
                    TotalAmount: totalAmount
                ));
            }
        }

        long maxRecordedSeatToken = await _repository.GetMaxFencingTokenForSeatsAsync(request.ShowtimeId, request.SeatIds, conn);

        if (maxRecordedSeatToken > 0 && request.FencingToken <= maxRecordedSeatToken)
        {
            return Results.Conflict(new
            {
                error = "Stale fencing token. One or more seats were already processed with a newer or identical fencing token.",
                code = "STALE_FENCING_TOKEN",
                lastRecordedToken = maxRecordedSeatToken,
                providedToken = request.FencingToken
            });
        }

        using var tx = await conn.BeginTransactionAsync();
        try
        {
            var reservationId = request.HoldId != Guid.Empty ? request.HoldId : Guid.NewGuid();

            if (existing != null)
            {
                var affected = await _repository.ConfirmExistingReservationAsync((Guid)existing.reservation_id, request.FencingToken, conn, tx);
                if (affected == 0)
                {
                    await tx.RollbackAsync();
                    return Results.Conflict(new
                    {
                        error = "Stale fencing token. The reservation already has a newer fencing token recorded.",
                        code = "STALE_FENCING_TOKEN"
                    });
                }
            }
            else
            {
                await _repository.InsertNewConfirmedReservationAsync(reservationId, request.ShowtimeId, request.IdempotencyKey, request.FencingToken, conn, tx);
            }

            decimal totalAmount = 0m;
            var tickets = request.Tickets ?? new Dictionary<Guid, Guid>();
            if (request.Tickets == null)
            {
                var defaultAdultTicketId = Guid.Parse("a1111111-1111-1111-1111-111111111111");
                foreach(var s in request.SeatIds)
                {
                    tickets[s] = defaultAdultTicketId;
                }
            }

            var seatPrices = await _repository.GetSeatPricesAsync(request.ShowtimeId, tickets);

            foreach (var seatId in request.SeatIds)
            {
                decimal seatPrice = seatPrices.TryGetValue(seatId, out var price) ? price : 0m;
                if (seatPrice == 0m) 
                {
                    await tx.RollbackAsync();
                    return Results.BadRequest(new { error = $"Pricing could not be determined for seat {seatId}." });
                }

                totalAmount += seatPrice;

                await _repository.InsertReservationSeatAsync(reservationId, seatId, seatPrice, conn, tx);
                await _repository.InsertConfirmedSeatAsync(request.ShowtimeId, seatId, reservationId, conn, tx);
            }

            await tx.CommitAsync();

            foreach (var seatId in request.SeatIds)
            {
                await _lockService.ReleaseSeatHoldAsync(request.ShowtimeId, seatId, request.HoldId.ToString());
            }

            await _eventBus.PublishAsync("reservation.confirmed", new ReservationConfirmedIntegrationEvent(
                reservationId,
                request.ShowtimeId,
                request.SeatIds,
                totalAmount,
                DateTimeOffset.UtcNow));

            return Results.Ok(new ReservationConfirmationResponse(
                ReservationId: reservationId,
                ShowtimeId: request.ShowtimeId,
                ConfirmedSeatIds: request.SeatIds,
                Status: Constants.Statuses.Confirmed,
                ConfirmedAt: DateTimeOffset.UtcNow,
                TotalAmount: totalAmount
            ));
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await tx.RollbackAsync();
            return Results.Conflict(new
            {
                error = "Double-booking detected. One or more seats are already confirmed by another transaction.",
                code = "DOUBLE_BOOKING_PREVENTED"
                // Deliberately hiding raw ex.MessageText to prevent detail leakage
            });
        }
        catch (Exception)
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}
