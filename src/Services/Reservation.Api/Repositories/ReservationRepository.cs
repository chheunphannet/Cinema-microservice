using System.Data.Common;
using Cinema.Foundation.Data;
using Dapper;
using Reservation.Api.Models;

namespace Reservation.Api.Repositories;

public class ReservationRepository : IReservationRepository
{
    private readonly IDbConnectionFactory _dbFactory;

    public ReservationRepository(IDbConnectionFactory dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<Guid>> GetAlreadyBookedSeatsAsync(Guid showtimeId, IEnumerable<Guid> seatIds)
    {
        using var conn = _dbFactory.CreateReadConnection();
        const string checkSql = @"
            SELECT seat_id 
            FROM reservations.confirmed_seats 
            WHERE showtime_id = @ShowtimeId AND seat_id = ANY(@SeatIds)";

        var alreadyBooked = await conn.QueryAsync<Guid>(checkSql, new 
        { 
            ShowtimeId = showtimeId, 
            SeatIds = seatIds.ToArray() 
        });
        
        return alreadyBooked.ToList();
    }

    public async Task<List<Guid>> GetBlockedSeatsAsync(Guid showtimeId, IEnumerable<Guid> seatIds)
    {
        using var conn = _dbFactory.CreateReadConnection();
        const string checkSql = @"
            SELECT sb.seat_id 
            FROM catalog.seat_blocks sb
            JOIN catalog.showtimes st ON st.showtime_id = @ShowtimeId
            WHERE (sb.showtime_id = @ShowtimeId OR (sb.showtime_id IS NULL AND sb.auditorium_id = st.auditorium_id))
              AND sb.seat_id = ANY(@SeatIds)";

        var blocked = await conn.QueryAsync<Guid>(checkSql, new 
        { 
            ShowtimeId = showtimeId, 
            SeatIds = seatIds.ToArray() 
        });
        
        return blocked.ToList();
    }

    public async Task<List<dynamic>> GetRowSeatsContextAsync(Guid showtimeId, IEnumerable<Guid> requestedSeatIds)
    {
        using var conn = _dbFactory.CreateReadConnection();
        const string sql = @"
            WITH TargetRows AS (
                SELECT DISTINCT row_label 
                FROM catalog.seats 
                WHERE seat_id = ANY(@SeatIds)
            )
            SELECT s.seat_id as SeatId, s.row_label as RowLabel, s.seat_number as SeatNumber, s.is_active as IsActive,
                   CASE WHEN cs.seat_id IS NOT NULL OR sb.seat_id IS NOT NULL THEN true ELSE false END as IsBooked
            FROM catalog.seats s
            JOIN catalog.showtimes st ON st.showtime_id = @ShowtimeId AND st.auditorium_id = s.auditorium_id
            JOIN TargetRows tr ON s.row_label = tr.row_label
            LEFT JOIN reservations.confirmed_seats cs ON cs.showtime_id = st.showtime_id AND cs.seat_id = s.seat_id
            LEFT JOIN catalog.seat_blocks sb ON (sb.showtime_id = st.showtime_id OR (sb.showtime_id IS NULL AND sb.auditorium_id = st.auditorium_id)) AND sb.seat_id = s.seat_id
            ORDER BY s.row_label, s.seat_number";

        var seats = await conn.QueryAsync<dynamic>(sql, new { ShowtimeId = showtimeId, SeatIds = requestedSeatIds.ToArray() });
        return seats.ToList();
    }

    public async Task<Dictionary<Guid, decimal>> GetSeatPricesAsync(Guid showtimeId, Dictionary<Guid, Guid> seatTicketTypes)
    {
        using var conn = _dbFactory.CreateReadConnection();
        
        // We will fetch the pricing matrix for this showtime
        const string matrixSql = @"
            SELECT s.base_price as BasePrice, pce.ticket_type_id as TicketTypeId, pce.seat_type as SeatType, pce.price as Price
            FROM catalog.showtimes s
            LEFT JOIN catalog.price_card_entries pce ON s.price_card_id = pce.price_card_id
            WHERE s.showtime_id = @ShowtimeId";

        var matrix = (await conn.QueryAsync<dynamic>(matrixSql, new { ShowtimeId = showtimeId })).ToList();
        var dictMatrix = new Dictionary<string, decimal>();
        decimal basePrice = 0.00m;
        foreach(var m in matrix)
        {
            if (m.baseprice != null)
            {
                basePrice = (decimal)m.baseprice;
            }
            if (m.tickettypeid != null && m.seattype != null && m.price != null)
            {
                dictMatrix[$"{m.tickettypeid}:{m.seattype}".ToLower()] = (decimal)m.price;
            }
        }

        // Fetch seat types
        var seatIds = seatTicketTypes.Keys.ToArray();
        const string seatsSql = @"
            SELECT seat_id as SeatId, seat_type as SeatType 
            FROM catalog.seats 
            WHERE seat_id = ANY(@SeatIds)";
        var seats = await conn.QueryAsync<dynamic>(seatsSql, new { SeatIds = seatIds });

        var dict = new Dictionary<Guid, decimal>();
        foreach(var s in seats)
        {
            var sId = (Guid)s.seatid;
            var sType = ((string)s.seattype).ToLower();
            var tId = seatTicketTypes.TryGetValue(sId, out var tid) ? tid : Guid.Empty;

            string key = $"{tId}:{sType}".ToLower();
            if (dictMatrix.TryGetValue(key, out var price))
            {
                dict[sId] = price;
            }
            else if (basePrice > 0)
            {
                dict[sId] = basePrice;
            }
            else
            {
                // Fallback if matrix is incomplete (should not happen in prod if configured correctly)
                dict[sId] = 0.00m;
            }
        }
        return dict;
    }

    public async Task CreateHoldAsync(Guid holdId, Guid showtimeId, Guid? customerId, DateTimeOffset expiresAt, Guid idempotencyKey, long fencingToken, string? guestEmail = null, string? guestPhone = null, string? guestName = null)
    {
        using var conn = _dbFactory.CreateConnection();
        const string sql = @"
            INSERT INTO reservations.reservations 
                (reservation_id, showtime_id, customer_id, status, hold_expires_at, idempotency_key, fencing_token, is_guest, guest_email, guest_phone, guest_name, created_at)
            VALUES 
                (@HoldId, @ShowtimeId, @CustomerId, 'hold'::reservation_status, @ExpiresAt, @IdempotencyKey, @FencingToken, @IsGuest, @GuestEmail, @GuestPhone, @GuestName, now())
            ON CONFLICT (idempotency_key) DO NOTHING";
        
        bool isGuest = !string.IsNullOrWhiteSpace(guestEmail) || customerId == null;

        await conn.ExecuteAsync(sql, new 
        { 
            HoldId = holdId, 
            ShowtimeId = showtimeId, 
            CustomerId = customerId, 
            ExpiresAt = expiresAt, 
            IdempotencyKey = idempotencyKey,
            FencingToken = fencingToken,
            IsGuest = isGuest,
            GuestEmail = guestEmail,
            GuestPhone = guestPhone,
            GuestName = guestName
        });
    }

    public async Task InsertHoldSeatsAsync(Guid reservationId, Guid showtimeId, List<Guid> seatIds, Dictionary<Guid, Guid> seatTicketTypes)
    {
        using var conn = _dbFactory.CreateConnection();
        var prices = await GetSeatPricesAsync(showtimeId, seatTicketTypes);
        const string sql = "INSERT INTO reservations.reservation_seats (reservation_id, seat_id, price) VALUES (@ResId, @SeatId, @Price) ON CONFLICT DO NOTHING";
        foreach (var s in seatIds)
        {
            var p = prices.TryGetValue(s, out var price) ? price : 0.00m;
            await conn.ExecuteAsync(sql, new { ResId = reservationId, SeatId = s, Price = p });
        }
    }

    public async Task CancelHoldAsync(Guid holdId)
    {
        using var conn = _dbFactory.CreateConnection();
        const string cancelHoldSql = @"
            UPDATE reservations.reservations 
            SET status = 'cancelled'::reservation_status 
            WHERE reservation_id = @HoldId AND status = 'hold'::reservation_status";
        await conn.ExecuteAsync(cancelHoldSql, new { HoldId = holdId });
    }

    public async Task<dynamic?> GetReservationByIdempotencyKeyOrHoldIdAsync(Guid idempotencyKey, Guid holdId, DbConnection conn, DbTransaction? tx = null)
    {
        const string idempSql = @"
            SELECT reservation_id, showtime_id, status, confirmed_at, fencing_token
            FROM reservations.reservations
            WHERE idempotency_key = @IdempotencyKey OR reservation_id = @HoldId";

        return await conn.QueryFirstOrDefaultAsync<dynamic>(idempSql, new 
        { 
            IdempotencyKey = idempotencyKey,
            HoldId = holdId
        }, tx);
    }

    public async Task<long> GetMaxFencingTokenForSeatsAsync(Guid showtimeId, IEnumerable<Guid> seatIds, DbConnection conn, DbTransaction? tx = null)
    {
        const string checkTokenSql = @"
            SELECT COALESCE(MAX(r.fencing_token), 0)
            FROM reservations.reservations r
            JOIN reservations.confirmed_seats cs ON r.reservation_id = cs.reservation_id
            WHERE cs.showtime_id = @ShowtimeId AND cs.seat_id = ANY(@SeatIds)";

        return await conn.ExecuteScalarAsync<long>(checkTokenSql, new
        {
            ShowtimeId = showtimeId,
            SeatIds = seatIds.ToArray()
        }, tx);
    }

    public async Task<int> ConfirmExistingReservationAsync(Guid reservationId, long fencingToken, DbConnection conn, DbTransaction tx)
    {
        const string updateResSql = @"
            UPDATE reservations.reservations 
            SET status = 'confirmed'::reservation_status,
                fencing_token = @FencingToken,
                confirmed_at = now()
            WHERE reservation_id = @ReservationId AND fencing_token <= @FencingToken";

        return await conn.ExecuteAsync(updateResSql, new
        {
            ReservationId = reservationId,
            FencingToken = fencingToken
        }, tx);
    }

    public async Task InsertNewConfirmedReservationAsync(Guid reservationId, Guid showtimeId, Guid idempotencyKey, long fencingToken, DbConnection conn, DbTransaction tx)
    {
        const string insertResSql = @"
            INSERT INTO reservations.reservations 
                (reservation_id, showtime_id, status, idempotency_key, fencing_token, confirmed_at)
            VALUES 
                (@ReservationId, @ShowtimeId, 'confirmed'::reservation_status, @IdempotencyKey, @FencingToken, now())";

        await conn.ExecuteAsync(insertResSql, new
        {
            ReservationId = reservationId,
            ShowtimeId = showtimeId,
            IdempotencyKey = idempotencyKey,
            FencingToken = fencingToken
        }, tx);
    }

    public async Task InsertReservationSeatAsync(Guid reservationId, Guid seatId, decimal price, DbConnection conn, DbTransaction tx)
    {
        const string insertResSeatSql = @"
            INSERT INTO reservations.reservation_seats (reservation_id, seat_id, price)
            VALUES (@ReservationId, @SeatId, @Price)
            ON CONFLICT (reservation_id, seat_id) DO NOTHING";
        
        await conn.ExecuteAsync(insertResSeatSql, new { ReservationId = reservationId, SeatId = seatId, Price = price }, tx);
    }

    public async Task InsertConfirmedSeatAsync(Guid showtimeId, Guid seatId, Guid reservationId, DbConnection conn, DbTransaction tx)
    {
        const string insertConfirmedSql = @"
            INSERT INTO reservations.confirmed_seats (showtime_id, seat_id, reservation_id, booked_at)
            VALUES (@ShowtimeId, @SeatId, @ReservationId, now())";
            
        await conn.ExecuteAsync(insertConfirmedSql, new { ShowtimeId = showtimeId, SeatId = seatId, ReservationId = reservationId }, tx);
    }

    public async Task<List<dynamic>> GetReservationDetailsAsync(Guid reservationId)
    {
        using var conn = _dbFactory.CreateConnection();
        const string sql = @"
            SELECT r.reservation_id as ReservationId, r.showtime_id as ShowtimeId, 
                   r.status as Status, r.confirmed_at as ConfirmedAt, r.created_at as CreatedAt,
                   rs.seat_id as SeatId, rs.price as Price
            FROM reservations.reservations r
            LEFT JOIN reservations.reservation_seats rs ON r.reservation_id = rs.reservation_id
            WHERE r.reservation_id = @ReservationId";

        var rows = await conn.QueryAsync<dynamic>(sql, new { ReservationId = reservationId });
        return rows.ToList();
    }

    public async Task<dynamic?> FindActiveReservationByIdempotencyKeyAsync(Guid idempotencyKey)
    {
        using var conn = _dbFactory.CreateConnection();
        const string sql = @"
            SELECT r.reservation_id, r.showtime_id, r.status, r.hold_expires_at, r.fencing_token, r.idempotency_key
            FROM reservations.reservations r
            WHERE r.idempotency_key = @IdempotencyKey";
        return await conn.QueryFirstOrDefaultAsync<dynamic>(sql, new { IdempotencyKey = idempotencyKey });
    }

    public async Task<List<Guid>> GetReservationSeatsAsync(Guid reservationId)
    {
        using var conn = _dbFactory.CreateConnection();
        const string sql = @"
            SELECT seat_id FROM reservations.reservation_seats WHERE reservation_id = @ReservationId";
        var seats = await conn.QueryAsync<Guid>(sql, new { ReservationId = reservationId });
        return seats.ToList();
    }

    public async Task<int> CleanupExpiredHoldsAsync()
    {
        using var conn = _dbFactory.CreateConnection();
        const string cleanupSql = @"
            UPDATE reservations.reservations 
            SET status = 'expired'::reservation_status 
            WHERE status = 'hold'::reservation_status 
              AND hold_expires_at < now()";
              
        return await conn.ExecuteAsync(cleanupSql);
    }
}
