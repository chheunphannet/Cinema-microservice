using Cinema.Foundation.Data;
using Dapper;
using Ticket.Api.Models;

namespace Ticket.Api.Repositories;

public class TicketRepository : ITicketRepository
{
    private readonly IDbConnectionFactory _dbFactory;

    public TicketRepository(IDbConnectionFactory dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<TicketDto> CreateTicketAsync(Guid ticketId, Guid reservationId, Guid showtimeId, Guid seatId, string qrHash)
    {
        using var conn = _dbFactory.CreateConnection();
        const string insertSql = @"
            INSERT INTO tickets.tickets 
                (ticket_id, reservation_id, showtime_id, seat_id, qr_token_hash, status, is_printed, created_at)
            VALUES 
                (@TicketId, @ReservationId, @ShowtimeId, @SeatId, @QrTokenHash, 'active'::ticket_status, false, now())
            ON CONFLICT (reservation_id, seat_id) DO UPDATE 
                SET reservation_id = EXCLUDED.reservation_id
            RETURNING ticket_id, reservation_id, showtime_id, seat_id, qr_token_hash, status, is_printed, printed_at, redeemed_at, created_at";

        var row = await conn.QuerySingleAsync<dynamic>(insertSql, new
        {
            TicketId = ticketId,
            ReservationId = reservationId,
            ShowtimeId = showtimeId,
            SeatId = seatId,
            QrTokenHash = qrHash
        });

        return new TicketDto(
            TicketId: (Guid)row.ticket_id,
            ReservationId: (Guid)row.reservation_id,
            ShowtimeId: (Guid)row.showtime_id,
            SeatId: (Guid)row.seat_id,
            QrTokenHash: (string)row.qr_token_hash,
            Status: (string)row.status,
            IsPrinted: (bool)row.is_printed,
            PrintedAt: (DateTimeOffset?)row.printed_at,
            RedeemedAt: (DateTimeOffset?)row.redeemed_at,
            CreatedAt: (DateTimeOffset)row.created_at
        );
    }

    public async Task<dynamic?> GetTicketForPrintAsync(Guid ticketId)
    {
        using var conn = _dbFactory.CreateConnection();
        const string checkSql = "SELECT ticket_id, status, is_printed, printed_at FROM tickets.tickets WHERE ticket_id = @TicketId";
        return await conn.QueryFirstOrDefaultAsync<dynamic>(checkSql, new { TicketId = ticketId });
    }

    public async Task<IEnumerable<string>> GetSupervisorPinHashesAsync()
    {
        using var conn = _dbFactory.CreateConnection();
        const string supSql = @"
            SELECT u.pin_hash 
            FROM identity.users u
            INNER JOIN identity.user_roles ur ON u.user_id = ur.user_id
            INNER JOIN identity.roles r ON ur.role_id = r.role_id
            WHERE r.name IN ('supervisor', 'branch_manager', 'system_admin') AND u.is_active = true";

        return await conn.QueryAsync<string>(supSql);
    }

    public async Task MarkTicketPrintedAsync(Guid ticketId)
    {
        using var conn = _dbFactory.CreateConnection();
        const string updateSql = @"
            UPDATE tickets.tickets 
            SET is_printed = true, printed_at = COALESCE(printed_at, now()) 
            WHERE ticket_id = @TicketId";
        await conn.ExecuteAsync(updateSql, new { TicketId = ticketId });
    }

    public async Task LogAuditAsync(Guid ticketId, string action, string terminalCode, string metadataJson)
    {
        using var conn = _dbFactory.CreateConnection();
        const string auditSql = @"
            INSERT INTO tickets.redemption_audit (ticket_id, action, terminal_code, metadata)
            VALUES (@TicketId, @Action, @TerminalCode, @Metadata::jsonb)";
        
        await conn.ExecuteAsync(auditSql, new
        {
            TicketId = ticketId,
            Action = action,
            TerminalCode = terminalCode,
            Metadata = metadataJson
        });
    }

    public async Task<dynamic?> GetTicketForRedemptionAsync(string qrHash, string rawToken)
    {
        using var conn = _dbFactory.CreateConnection();

        Guid? resolvedOrderId = null;
        if (!string.IsNullOrWhiteSpace(rawToken) && rawToken.StartsWith("CINEMA-ORDER:", StringComparison.OrdinalIgnoreCase))
        {
            var parts = rawToken.Split(':');
            if (parts.Length >= 2 && Guid.TryParse(parts[1], out var oid))
            {
                resolvedOrderId = oid;
            }
        }

        const string selectSql = @"
            SELECT t.ticket_id, t.status, t.redeemed_at, t.is_printed
            FROM tickets.tickets t
            WHERE t.qr_token_hash = @Hash 
               OR t.ticket_id::text = @RawToken
               OR (@OrderId IS NOT NULL AND t.reservation_id IN (SELECT o.reservation_id FROM pos.orders o WHERE o.order_id = @OrderId) AND t.status = 'active')
            ORDER BY (CASE WHEN t.status = 'active' THEN 0 ELSE 1 END), t.ticket_id
            LIMIT 1";

        return await conn.QueryFirstOrDefaultAsync<dynamic>(selectSql, new { Hash = qrHash, RawToken = rawToken, OrderId = resolvedOrderId });
    }

    public async Task<bool> MarkTicketRedeemedAsync(Guid ticketId)
    {
        using var conn = _dbFactory.CreateConnection();
        const string updateSql = "UPDATE tickets.tickets SET status = 'used'::ticket_status, redeemed_at = now() WHERE ticket_id = @TicketId AND status = 'active'::ticket_status";
        var rows = await conn.ExecuteAsync(updateSql, new { TicketId = ticketId });
        return rows > 0;
    }

    public async Task<dynamic?> GetTicketDetailsAsync(Guid ticketId)
    {
        using var conn = _dbFactory.CreateConnection();
        const string sql = @"
            SELECT t.ticket_id, t.reservation_id, t.showtime_id, t.seat_id, t.status, 
                   t.is_printed, t.printed_at, t.redeemed_at, t.created_at,
                   m.title as movie_title, a.name as auditorium_name, 
                   s.row_label, s.seat_number, b.name as branch_name
            FROM tickets.tickets t
            LEFT JOIN catalog.showtimes st ON t.showtime_id = st.showtime_id
            LEFT JOIN catalog.movies m ON st.movie_id = m.movie_id
            LEFT JOIN catalog.auditoriums a ON st.auditorium_id = a.auditorium_id
            LEFT JOIN catalog.branches b ON a.branch_id = b.branch_id
            LEFT JOIN catalog.seats s ON t.seat_id = s.seat_id
            WHERE t.ticket_id = @TicketId";

        return await conn.QueryFirstOrDefaultAsync<dynamic>(sql, new { TicketId = ticketId });
    }

    public async Task<dynamic?> GetETicketInfoAsync(Guid ticketId)
    {
        using var conn = _dbFactory.CreateConnection();
        const string sql = @"
            SELECT t.ticket_id, t.reservation_id, a.name as auditorium_name, s.row_label, s.seat_number, o.order_id
            FROM tickets.tickets t
            JOIN catalog.showtimes st ON t.showtime_id = st.showtime_id
            JOIN catalog.auditoriums a ON st.auditorium_id = a.auditorium_id
            JOIN catalog.seats s ON t.seat_id = s.seat_id
            LEFT JOIN pos.orders o ON t.reservation_id = o.reservation_id
            WHERE t.ticket_id = @TicketId";

        return await conn.QueryFirstOrDefaultAsync<dynamic>(sql, new { TicketId = ticketId });
    }

    public async Task<List<dynamic>> GetTicketsByReservationIdAsync(Guid reservationId)
    {
        using var conn = _dbFactory.CreateConnection();
        const string sql = @"
            SELECT t.ticket_id, t.reservation_id, t.showtime_id, t.seat_id, t.status, 
                   t.is_printed, t.printed_at, t.redeemed_at, t.created_at,
                   s.row_label, s.seat_number, s.seat_type
            FROM tickets.tickets t
            LEFT JOIN catalog.seats s ON t.seat_id = s.seat_id
            WHERE t.reservation_id = @ReservationId
            ORDER BY s.row_label, s.seat_number";

        var rows = await conn.QueryAsync<dynamic>(sql, new { ReservationId = reservationId });
        return rows.ToList();
    }

    public async Task VoidTicketsByReservationAsync(Guid reservationId, IEnumerable<Guid>? seatIds = null)
    {
        using var conn = _dbFactory.CreateConnection();
        if (seatIds != null && seatIds.Any())
        {
            await conn.ExecuteAsync(
                "UPDATE tickets.tickets SET status = 'void' WHERE reservation_id = @ReservationId AND seat_id = ANY(@SeatIds)", 
                new { ReservationId = reservationId, SeatIds = seatIds.ToArray() });
        }
        else
        {
            await conn.ExecuteAsync(
                "UPDATE tickets.tickets SET status = 'void' WHERE reservation_id = @ReservationId", 
                new { ReservationId = reservationId });
        }
    }
}
