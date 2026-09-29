using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Cinema.Foundation.Data;
using Dapper;
using Reservation.Api.Models;

namespace Reservation.Api.Repositories;

public class AdminBookingRepository : IAdminBookingRepository
{
    private readonly IDbConnectionFactory _dbFactory;

    public AdminBookingRepository(IDbConnectionFactory dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<AdminBookingSearchResponse> SearchBookingsAsync(AdminBookingSearchFilter filter)
    {
        using var conn = _dbFactory.CreateReadConnection();
        var parameters = new DynamicParameters();
        var conditions = new List<string>();

        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var q = filter.Query.Trim();
            if (Guid.TryParse(q, out var queryGuid))
            {
                conditions.Add("(r.reservation_id = @QueryGuid OR o.order_id = @QueryGuid OR r.customer_id = @QueryGuid)");
                parameters.Add("QueryGuid", queryGuid);
            }
            else
            {
                conditions.Add("(COALESCE(o.booking_reference, 'BKG-' || UPPER(SUBSTRING(r.reservation_id::text, 1, 8))) ILIKE @QueryLike OR r.guest_email ILIKE @QueryLike OR o.customer_email ILIKE @QueryLike OR c.email ILIKE @QueryLike OR r.guest_phone ILIKE @QueryLike OR o.customer_phone ILIKE @QueryLike OR c.phone ILIKE @QueryLike)");
                parameters.Add("QueryLike", $"%{q}%");
            }
        }

        if (filter.BranchId.HasValue)
        {
            conditions.Add("b.branch_id = @BranchId");
            parameters.Add("BranchId", filter.BranchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            conditions.Add("(r.status::text = @Status OR o.status::text = @Status)");
            parameters.Add("Status", filter.Status.Trim().ToLowerInvariant());
        }

        if (filter.FromDate.HasValue)
        {
            conditions.Add("r.created_at >= @FromDate");
            parameters.Add("FromDate", filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            conditions.Add("r.created_at <= @ToDate");
            parameters.Add("ToDate", filter.ToDate.Value);
        }

        var whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";

        var countSql = $@"
            SELECT COUNT(DISTINCT r.reservation_id)
            FROM reservations.reservations r
            LEFT JOIN pos.orders o ON o.reservation_id = r.reservation_id
            JOIN catalog.showtimes st ON st.showtime_id = r.showtime_id
            JOIN catalog.auditoriums a ON a.auditorium_id = st.auditorium_id
            JOIN catalog.branches b ON b.branch_id = a.branch_id
            JOIN catalog.movies m ON m.movie_id = st.movie_id
            LEFT JOIN identity.customers c ON c.customer_id = COALESCE(r.customer_id, o.customer_id)
            {whereClause}";

        var totalCount = await conn.ExecuteScalarAsync<int>(countSql, parameters);

        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        var offset = (page - 1) * pageSize;

        parameters.Add("Limit", pageSize);
        parameters.Add("Offset", offset);

        var itemsSql = $@"
            SELECT 
                r.reservation_id AS ReservationId,
                o.order_id AS OrderId,
                COALESCE(o.booking_reference, 'BKG-' || UPPER(SUBSTRING(r.reservation_id::text, 1, 8))) AS BookingReference,
                b.branch_id AS BranchId,
                b.name AS BranchName,
                m.title AS MovieTitle,
                a.name AS AuditoriumName,
                st.starts_at AS ShowtimeStart,
                COALESCE(seat_info.seats_count, 0)::int AS SeatsCount,
                COALESCE(o.total_amount, seat_info.seats_total, 0) AS TotalAmount,
                r.status::text AS Status,
                COALESCE(o.customer_email, r.guest_email, c.email) AS CustomerEmail,
                COALESCE(o.customer_phone, r.guest_phone, c.phone) AS CustomerPhone,
                r.created_at AS CreatedAt,
                r.confirmed_at AS ConfirmedAt
            FROM reservations.reservations r
            LEFT JOIN pos.orders o ON o.reservation_id = r.reservation_id
            JOIN catalog.showtimes st ON st.showtime_id = r.showtime_id
            JOIN catalog.auditoriums a ON a.auditorium_id = st.auditorium_id
            JOIN catalog.branches b ON b.branch_id = a.branch_id
            JOIN catalog.movies m ON m.movie_id = st.movie_id
            LEFT JOIN identity.customers c ON c.customer_id = COALESCE(r.customer_id, o.customer_id)
            LEFT JOIN LATERAL (
                SELECT COUNT(*) AS seats_count, COALESCE(SUM(rs.price), 0) AS seats_total
                FROM reservations.reservation_seats rs
                WHERE rs.reservation_id = r.reservation_id
            ) seat_info ON true
            {whereClause}
            ORDER BY r.created_at DESC
            LIMIT @Limit OFFSET @Offset";

        var items = (await conn.QueryAsync<AdminBookingSearchResultItem>(itemsSql, parameters)).ToList();
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        return new AdminBookingSearchResponse(
            Items: items,
            TotalCount: totalCount,
            Page: page,
            PageSize: pageSize,
            TotalPages: totalPages
        );
    }

    public async Task<(Guid BranchId, string Status, Guid? OrderId)?> GetBookingSummaryAsync(Guid reservationId)
    {
        using var conn = _dbFactory.CreateReadConnection();
        const string sql = @"
            SELECT 
                b.branch_id AS BranchId,
                r.status::text AS Status,
                o.order_id AS OrderId
            FROM reservations.reservations r
            LEFT JOIN pos.orders o ON o.reservation_id = r.reservation_id
            JOIN catalog.showtimes st ON st.showtime_id = r.showtime_id
            JOIN catalog.auditoriums a ON a.auditorium_id = st.auditorium_id
            JOIN catalog.branches b ON b.branch_id = a.branch_id
            WHERE r.reservation_id = @ReservationId";

        var row = await conn.QuerySingleOrDefaultAsync(sql, new { ReservationId = reservationId });
        if (row == null) return null;

        return ((Guid)row.branchid, (string)row.status, (Guid?)row.orderid);
    }

    public async Task<AdminBookingDetailResponse?> GetBookingDetailAsync(Guid reservationId)
    {
        using var conn = _dbFactory.CreateReadConnection();

        // Single multi-statement query to eliminate N+1 DB roundtrips (was 6 separate queries)
        const string combinedSql = @"
            -- 1. Header
            SELECT 
                r.reservation_id AS ReservationId,
                o.order_id AS OrderId,
                COALESCE(o.booking_reference, 'BKG-' || UPPER(SUBSTRING(r.reservation_id::text, 1, 8))) AS BookingReference,
                r.status::text AS Status,
                r.created_at AS CreatedAt,
                r.confirmed_at AS ConfirmedAt,
                r.customer_id AS CustomerId,
                r.is_guest AS IsGuest,
                COALESCE(r.guest_name, NULLIF(TRIM(CONCAT(c.first_name, ' ', c.last_name)), '')) AS CustomerName,
                COALESCE(o.customer_email, r.guest_email, c.email) AS CustomerEmail,
                COALESCE(o.customer_phone, r.guest_phone, c.phone) AS CustomerPhone,
                b.branch_id AS BranchId,
                b.code AS BranchCode,
                b.name AS BranchName,
                b.address AS BranchAddress,
                b.timezone AS BranchTimezone,
                a.auditorium_id AS AuditoriumId,
                a.name AS AuditoriumName,
                m.movie_id AS MovieId,
                m.title AS MovieTitle,
                m.duration_minutes AS MovieDuration,
                m.genre AS MovieGenre,
                m.classification AS MovieClassification,
                m.poster_url AS MoviePosterUrl,
                st.showtime_id AS ShowtimeId,
                st.starts_at AS ShowtimeStartsAt,
                st.ends_at AS ShowtimeEndsAt,
                st.base_price AS ShowtimeBasePrice,
                COALESCE(o.subtotal, 0) AS OrderSubtotal,
                COALESCE(o.discount_amount, 0) AS OrderDiscount,
                COALESCE(o.total_amount, 0) AS OrderTotal
            FROM reservations.reservations r
            LEFT JOIN pos.orders o ON o.reservation_id = r.reservation_id
            JOIN catalog.showtimes st ON st.showtime_id = r.showtime_id
            JOIN catalog.auditoriums a ON a.auditorium_id = st.auditorium_id
            JOIN catalog.branches b ON b.branch_id = a.branch_id
            JOIN catalog.movies m ON m.movie_id = st.movie_id
            LEFT JOIN identity.customers c ON (c.customer_id = r.customer_id OR c.customer_id = o.customer_id)
            WHERE r.reservation_id = @ReservationId;

            -- 2. Seats
            SELECT 
                rs.seat_id AS SeatId,
                s.row_label AS RowLabel,
                s.seat_number AS SeatNumber,
                s.seat_type AS SeatType,
                rs.price AS Price,
                COALESCE(rs.status, 'confirmed') AS Status
            FROM reservations.reservation_seats rs
            JOIN catalog.seats s ON s.seat_id = rs.seat_id
            WHERE rs.reservation_id = @ReservationId
            ORDER BY s.row_label, s.seat_number;

            -- 3. Tickets
            SELECT 
                t.ticket_id AS TicketId,
                t.seat_id AS SeatId,
                t.qr_token_hash AS QrTokenHash,
                t.status::text AS Status,
                t.is_printed AS IsPrinted,
                t.printed_at AS PrintedAt,
                t.redeemed_at AS RedeemedAt,
                t.email_sent AS EmailSent,
                t.email_sent_at AS EmailSentAt,
                t.email_recipient AS EmailRecipient
            FROM tickets.tickets t
            WHERE t.reservation_id = @ReservationId
            ORDER BY t.created_at;

            -- 4. Order Lines (concessions) — returns empty if no order exists
            SELECT 
                ol.order_line_id AS OrderLineId,
                ol.product_id AS ProductId,
                ol.description AS Description,
                ol.quantity AS Quantity,
                ol.unit_price AS UnitPrice,
                ol.line_total AS LineTotal,
                ol.is_fulfilled AS IsFulfilled,
                ol.fulfilled_at AS FulfilledAt,
                COALESCE(ol.fulfillment_status, 'pending') AS FulfillmentStatus
            FROM pos.order_lines ol
            WHERE ol.order_id = (SELECT o2.order_id FROM pos.orders o2 WHERE o2.reservation_id = @ReservationId LIMIT 1)
            ORDER BY ol.order_line_id;

            -- 5. Payments — returns empty if no order exists
            SELECT 
                p.payment_id AS PaymentId,
                p.method::text AS Method,
                p.amount AS Amount,
                p.provider_reference AS ProviderReference,
                p.status AS Status,
                p.paid_at AS PaidAt
            FROM pos.payments p
            WHERE p.order_id = (SELECT o3.order_id FROM pos.orders o3 WHERE o3.reservation_id = @ReservationId LIMIT 1)
            ORDER BY p.paid_at;

            -- 6. Refunds
            SELECT 
                rf.refund_id AS RefundId,
                rf.order_id AS OrderId,
                rf.reservation_id AS ReservationId,
                rf.refund_amount AS RefundAmount,
                rf.reason_code AS ReasonCode,
                rf.notes AS Notes,
                rf.authorized_by AS AuthorizedBy,
                rf.refunded_at AS RefundedAt
            FROM pos.refunds rf
            WHERE rf.reservation_id = @ReservationId 
               OR rf.order_id = (SELECT o4.order_id FROM pos.orders o4 WHERE o4.reservation_id = @ReservationId LIMIT 1)
            ORDER BY rf.refunded_at DESC;

            -- 7. Disputes
            SELECT 
                d.dispute_id AS DisputeId,
                d.order_id AS OrderId,
                d.reservation_id AS ReservationId,
                d.provider_dispute_id AS ProviderDisputeId,
                d.status AS Status,
                d.amount AS Amount,
                d.evidence_notes AS EvidenceNotes,
                d.created_at AS CreatedAt,
                d.resolved_at AS ResolvedAt
            FROM pos.disputes d
            WHERE d.reservation_id = @ReservationId 
               OR d.order_id = (SELECT o5.order_id FROM pos.orders o5 WHERE o5.reservation_id = @ReservationId LIMIT 1)
            ORDER BY d.created_at DESC;";

        using var multi = await conn.QueryMultipleAsync(combinedSql, new { ReservationId = reservationId });

        // 1. Header
        var header = await multi.ReadSingleOrDefaultAsync();
        if (header == null) return null;

        Guid? orderId = (Guid?)header.orderid;

        // 2. Seats
        var seats = (await multi.ReadAsync<AdminBookingSeatDto>()).ToList();

        // 3. Tickets
        var tickets = (await multi.ReadAsync<AdminBookingTicketDto>()).ToList();

        // 4. Concessions
        var concessions = (await multi.ReadAsync<AdminBookingConcessionDto>()).ToList();

        // 5. Payments
        var payments = (await multi.ReadAsync<AdminBookingPaymentDto>()).ToList();

        // 6. Refunds
        var refunds = (await multi.ReadAsync<AdminBookingRefundDto>()).ToList();

        // 7. Disputes
        var disputes = (await multi.ReadAsync<AdminBookingDisputeDto>()).ToList();

        // Calculate Financials
        decimal totalPaid = payments.Where(p => p.Status.Equals("captured", StringComparison.OrdinalIgnoreCase)).Sum(p => p.Amount);
        if (totalPaid == 0)
        {
            totalPaid = (decimal)header.ordertotal > 0 ? (decimal)header.ordertotal : seats.Sum(s => s.Price);
        }
        decimal totalRefunded = refunds.Sum(r => r.RefundAmount);
        decimal netRevenue = totalPaid - totalRefunded;

        var financials = new AdminBookingFinancialsDto(
            Subtotal: (decimal)header.ordersubtotal > 0 ? (decimal)header.ordersubtotal : seats.Sum(s => s.Price),
            DiscountAmount: (decimal)header.orderdiscount,
            TotalAmount: (decimal)header.ordertotal > 0 ? (decimal)header.ordertotal : seats.Sum(s => s.Price),
            TotalPaid: totalPaid,
            TotalRefunded: totalRefunded,
            NetRevenue: netRevenue,
            Payments: payments
        );

        return new AdminBookingDetailResponse(
            ReservationId: (Guid)header.reservationid,
            OrderId: orderId,
            BookingReference: (string?)header.bookingreference,
            Status: (string)header.status,
            CreatedAt: (DateTimeOffset)header.createdat,
            ConfirmedAt: (DateTimeOffset?)header.confirmedat,
            Branch: new AdminBookingBranchDto(
                BranchId: (Guid)header.branchid,
                Code: (string)header.branchcode,
                Name: (string)header.branchname,
                Address: (string?)header.branchaddress,
                Timezone: (string)header.branchtimezone
            ),
            Auditorium: new AdminBookingAuditoriumDto(
                AuditoriumId: (Guid)header.auditoriumid,
                Name: (string)header.auditoriumname
            ),
            Movie: new AdminBookingMovieDto(
                MovieId: (Guid)header.movieid,
                Title: (string)header.movietitle,
                DurationMinutes: (int)header.movieduration,
                Genre: (string?)header.moviegenre,
                Classification: (string?)header.movieclassification,
                PosterUrl: (string?)header.movieposterurl
            ),
            Showtime: new AdminBookingShowtimeDto(
                ShowtimeId: (Guid)header.showtimeid,
                StartsAt: (DateTimeOffset)header.showtimestartsat,
                EndsAt: (DateTimeOffset)header.showtimeendsat,
                BasePrice: (decimal)header.showtimebaseprice
            ),
            Customer: new AdminBookingCustomerDto(
                CustomerId: (Guid?)header.customerid,
                Name: (string?)header.customername,
                Email: (string?)header.customeremail,
                Phone: (string?)header.customerphone,
                IsGuest: (bool)header.isguest
            ),
            Seats: seats,
            Tickets: tickets,
            Concessions: concessions,
            Financials: financials,
            Refunds: refunds,
            Disputes: disputes
        );
    }

    public async Task<RefundBookingResponse> ExecuteRefundAsync(
        Guid reservationId,
        List<Guid>? seatIds,
        List<Guid>? orderLineIds,
        decimal? refundAmount,
        string reasonCode,
        string? notes,
        Guid authorizedBy)
    {
        var validReasons = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "customer_request", "cancelled_showtime", "technical_issue", "chargeback", "duplicate_booking", "other"
        };

        if (!validReasons.Contains(reasonCode))
        {
            throw new ArgumentException($"Invalid refund reason code '{reasonCode}'. Allowed values: {string.Join(", ", validReasons)}.");
        }

        using var conn = (DbConnection)_dbFactory.CreateConnection();
        await conn.OpenAsync();
        using var tx = await conn.BeginTransactionAsync();

        try
        {
            // 1. Fetch Reservation and Order details with row-level locking to guard against race conditions
            const string resQuery = @"
                SELECT 
                    r.reservation_id,
                    r.showtime_id,
                    r.status::text AS status,
                    o.order_id,
                    COALESCE(o.total_amount, 0) AS total_amount
                FROM reservations.reservations r
                LEFT JOIN pos.orders o ON o.reservation_id = r.reservation_id
                WHERE r.reservation_id = @ReservationId
                FOR UPDATE OF r";

            var resInfo = await conn.QuerySingleOrDefaultAsync(resQuery, new { ReservationId = reservationId }, tx);
            if (resInfo == null)
            {
                throw new KeyNotFoundException($"Reservation {reservationId} not found.");
            }

            string currentStatus = (string)resInfo.status;
            if (currentStatus != "confirmed" && currentStatus != "partially_refunded")
            {
                throw new InvalidOperationException($"Reservation status is '{currentStatus}'. Only 'confirmed' or 'partially_refunded' bookings can be refunded.");
            }

            Guid? orderId = (Guid?)resInfo.order_id;
            Guid showtimeId = (Guid)resInfo.showtime_id;

            // 2. Fetch seats currently in reservation_seats
            const string seatsQuery = @"
                SELECT seat_id, price, COALESCE(status, 'confirmed') AS status
                FROM reservations.reservation_seats
                WHERE reservation_id = @ReservationId";

            var allSeats = (await conn.QueryAsync(seatsQuery, new { ReservationId = reservationId }, tx)).ToList();
            var activeSeats = allSeats.Where(s => (string)s.status == "confirmed").ToList();

            // Determine seats to refund
            List<Guid> refundedSeatIds;
            if (seatIds != null && seatIds.Count > 0)
            {
                var requestedSet = new HashSet<Guid>(seatIds);
                var activeSet = new HashSet<Guid>(activeSeats.Select(s => (Guid)s.seat_id));
                var missing = requestedSet.Except(activeSet).ToList();
                if (missing.Count > 0)
                {
                    throw new InvalidOperationException($"Seats {string.Join(", ", missing)} are not active or not part of this booking.");
                }
                refundedSeatIds = seatIds;
            }
            else if (orderLineIds == null || orderLineIds.Count == 0)
            {
                // Full refund: all currently active seats
                refundedSeatIds = activeSeats.Select(s => (Guid)s.seat_id).ToList();
            }
            else
            {
                // Only concessions being refunded
                refundedSeatIds = new List<Guid>();
            }

            // Concessions to refund
            List<Guid> refundedOrderLineIds = orderLineIds ?? new List<Guid>();

            // 3. Calculate financial totals
            decimal totalPaid = 0;
            if (orderId.HasValue)
            {
                totalPaid = await conn.ExecuteScalarAsync<decimal>(
                    "SELECT COALESCE(SUM(amount), 0) FROM pos.payments WHERE order_id = @OrderId AND status = 'captured'",
                    new { OrderId = orderId.Value },
                    tx);
                if (totalPaid == 0)
                {
                    totalPaid = (decimal)resInfo.total_amount;
                }
            }
            if (totalPaid == 0)
            {
                totalPaid = allSeats.Sum(s => (decimal)s.price);
            }

            decimal alreadyRefunded = await conn.ExecuteScalarAsync<decimal>(
                "SELECT COALESCE(SUM(refund_amount), 0) FROM pos.refunds WHERE reservation_id = @ReservationId OR (order_id IS NOT NULL AND order_id = @OrderId)",
                new { ReservationId = reservationId, OrderId = orderId },
                tx);

            decimal maxRefundable = totalPaid - alreadyRefunded;
            if (maxRefundable <= 0)
            {
                throw new InvalidOperationException("Booking has already been fully refunded.");
            }

            decimal effectiveRefundAmount;
            if (refundAmount.HasValue && refundAmount.Value > 0)
            {
                effectiveRefundAmount = refundAmount.Value;
            }
            else
            {
                decimal calculated = 0;
                foreach (var sid in refundedSeatIds)
                {
                    var sMatch = activeSeats.FirstOrDefault(s => (Guid)s.seat_id == sid);
                    if (sMatch != null) calculated += (decimal)sMatch.price;
                }

                if (refundedOrderLineIds.Count > 0 && orderId.HasValue)
                {
                    var lineTotals = await conn.QueryAsync<decimal>(
                        "SELECT line_total FROM pos.order_lines WHERE order_id = @OrderId AND order_line_id = ANY(@LineIds)",
                        new { OrderId = orderId.Value, LineIds = refundedOrderLineIds.ToArray() },
                        tx);
                    calculated += lineTotals.Sum();
                }

                if (calculated == 0 && (seatIds == null || seatIds.Count == 0))
                {
                    calculated = maxRefundable;
                }

                effectiveRefundAmount = calculated;
            }

            if (effectiveRefundAmount <= 0)
            {
                throw new InvalidOperationException("Refund amount must be greater than zero.");
            }

            if (effectiveRefundAmount > maxRefundable)
            {
                throw new InvalidOperationException($"Refund amount ({effectiveRefundAmount:F2}) exceeds maximum refundable balance ({maxRefundable:F2}).");
            }

            // 4. Perform atomic transactional writes
            var refundId = Guid.NewGuid();
            List<Guid> remainingSeatIds;
            string newResStatus;

            if (refundedSeatIds.Count > 0)
            {
                // Atomically release confirmed seats, mark reservation_seats as refunded, and void tickets in a single batched query
                const string batchRefundSeatsSql = @"
                    DELETE FROM reservations.confirmed_seats WHERE reservation_id = @ReservationId AND seat_id = ANY(@SeatIds);
                    UPDATE reservations.reservation_seats SET status = 'refunded' WHERE reservation_id = @ReservationId AND seat_id = ANY(@SeatIds);
                    UPDATE tickets.tickets SET status = 'void'::ticket_status WHERE reservation_id = @ReservationId AND seat_id = ANY(@SeatIds);";

                await conn.ExecuteAsync(batchRefundSeatsSql,
                    new { ReservationId = reservationId, SeatIds = refundedSeatIds.ToArray() },
                    tx);
            }

            // If concessions were refunded, cancel their fulfillment status on pos.order_lines
            if (refundedOrderLineIds.Count > 0 && orderId.HasValue)
            {
                await conn.ExecuteAsync(
                    "UPDATE pos.order_lines SET fulfillment_status = 'cancelled' WHERE order_id = @OrderId AND order_line_id = ANY(@LineIds)",
                    new { OrderId = orderId.Value, LineIds = refundedOrderLineIds.ToArray() },
                    tx);
            }

            // Determine remaining active seats
            remainingSeatIds = (await conn.QueryAsync<Guid>(
                "SELECT seat_id FROM reservations.reservation_seats WHERE reservation_id = @ReservationId AND status = 'confirmed'",
                new { ReservationId = reservationId },
                tx)).ToList();

            if (refundedSeatIds.Count > 0)
            {
                newResStatus = remainingSeatIds.Count == 0 ? "refunded" : "partially_refunded";
                await conn.ExecuteAsync(
                    "UPDATE reservations.reservations SET status = @Status::reservation_status WHERE reservation_id = @ReservationId",
                    new { Status = newResStatus, ReservationId = reservationId },
                    tx);
            }
            else
            {
                // Concession-only refund: keep reservation status as-is
                newResStatus = currentStatus;
            }

            // Update order status if present
            if (orderId.HasValue)
            {
                var remainingOrderBalance = maxRefundable - effectiveRefundAmount;
                var newOrderStatus = remainingOrderBalance <= 0 ? "refunded" : "partially_refunded";

                await conn.ExecuteAsync(
                    "UPDATE pos.orders SET status = @Status::order_status WHERE order_id = @OrderId",
                    new { Status = newOrderStatus, OrderId = orderId.Value },
                    tx);
            }

            // Insert pos.refunds record
            await conn.ExecuteAsync(@"
                INSERT INTO pos.refunds (refund_id, order_id, reservation_id, refund_amount, reason_code, notes, authorized_by, refunded_at)
                VALUES (@RefundId, @OrderId, @ReservationId, @RefundAmount, @ReasonCode, @Notes, @AuthorizedBy, now())",
                new
                {
                    RefundId = refundId,
                    OrderId = orderId,
                    ReservationId = reservationId,
                    RefundAmount = effectiveRefundAmount,
                    ReasonCode = reasonCode,
                    Notes = notes,
                    AuthorizedBy = authorizedBy
                },
                tx);

            // Insert audit log
            await conn.ExecuteAsync(@"
                INSERT INTO public.audit_log (service_name, actor_id, action, entity_type, entity_id, before_data, after_data)
                VALUES ('reservation', @AuthorizedBy, 'REFUND_PROCESSED', 'reservation', @ReservationId, 
                        jsonb_build_object('status', @OldStatus, 'maxRefundable', @MaxRefundable),
                        jsonb_build_object('refundId', @RefundId, 'amount', @RefundAmount, 'remainingSeats', @RemainingCount))",
                new
                {
                    AuthorizedBy = authorizedBy,
                    ReservationId = reservationId,
                    OldStatus = currentStatus,
                    MaxRefundable = maxRefundable,
                    RefundId = refundId,
                    RefundAmount = effectiveRefundAmount,
                    RemainingCount = remainingSeatIds.Count
                },
                tx);

            await tx.CommitAsync();

            return new RefundBookingResponse(
                RefundId: refundId,
                ReservationId: reservationId,
                OrderId: orderId,
                RefundAmount: effectiveRefundAmount,
                TotalRefunded: alreadyRefunded + effectiveRefundAmount,
                RemainingAmount: maxRefundable - effectiveRefundAmount,
                ReservationStatus: newResStatus,
                RefundedSeatIds: refundedSeatIds,
                RemainingSeatIds: remainingSeatIds,
                RefundedOrderLineIds: refundedOrderLineIds,
                RefundedAt: DateTimeOffset.UtcNow,
                AuthorizedBy: authorizedBy,
                ShowtimeId: showtimeId
            );
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<(bool Success, int TicketsCount, List<TicketReissuePassItem> Passes, string? CustomerEmail, string? CustomerName, string MovieTitle, string BranchName, string AuditoriumName, DateTimeOffset ShowtimeStart, decimal TotalAmount, string? BookingRef, Guid? OrderId)> PrepareTicketReissueAsync(Guid reservationId, bool regenerateQrTokens)
    {
        using var conn = (DbConnection)_dbFactory.CreateConnection();
        await conn.OpenAsync();

        // 1. Fetch reservation & header info
        const string headerSql = @"
            SELECT 
                r.reservation_id,
                r.status::text AS status,
                o.order_id,
                COALESCE(o.booking_reference, 'BKG-' || UPPER(SUBSTRING(r.reservation_id::text, 1, 8))) AS booking_reference,
                COALESCE(o.customer_email, r.guest_email, c.email) AS customer_email,
                COALESCE(r.guest_name, NULLIF(TRIM(CONCAT(c.first_name, ' ', c.last_name)), '')) AS customer_name,
                m.title AS movie_title,
                b.name AS branch_name,
                a.name AS auditorium_name,
                st.starts_at AS showtime_start,
                COALESCE(o.total_amount, 0) AS total_amount
            FROM reservations.reservations r
            LEFT JOIN pos.orders o ON o.reservation_id = r.reservation_id
            JOIN catalog.showtimes st ON st.showtime_id = r.showtime_id
            JOIN catalog.auditoriums a ON a.auditorium_id = st.auditorium_id
            JOIN catalog.branches b ON b.branch_id = a.branch_id
            JOIN catalog.movies m ON m.movie_id = st.movie_id
            LEFT JOIN identity.customers c ON (c.customer_id = r.customer_id OR c.customer_id = o.customer_id)
            WHERE r.reservation_id = @ReservationId";

        var header = await conn.QuerySingleOrDefaultAsync(headerSql, new { ReservationId = reservationId });
        if (header == null)
        {
            throw new KeyNotFoundException($"Reservation {reservationId} not found.");
        }

        // 2. Fetch active tickets
        const string activeTicketsSql = @"
            SELECT 
                t.ticket_id,
                t.seat_id,
                t.qr_token_hash,
                s.row_label,
                s.seat_number,
                s.seat_type
            FROM tickets.tickets t
            JOIN catalog.seats s ON s.seat_id = t.seat_id
            WHERE t.reservation_id = @ReservationId AND t.status = 'active'
            ORDER BY s.row_label, s.seat_number";

        var ticketRows = (await conn.QueryAsync(activeTicketsSql, new { ReservationId = reservationId })).ToList();
        if (ticketRows.Count == 0)
        {
            throw new InvalidOperationException($"No active tickets found for reservation {reservationId}.");
        }

        // 3. If regenerateQrTokens is true, generate and update new QR hashes
        var passes = new List<TicketReissuePassItem>();
        if (regenerateQrTokens)
        {
            using var tx = await conn.BeginTransactionAsync();
            var ticketIds = new List<Guid>(ticketRows.Count);
            var qrHashes = new List<string>(ticketRows.Count);

            foreach (var tr in ticketRows)
            {
                var tid = (Guid)tr.ticket_id;
                var sid = (Guid)tr.seat_id;
                var newRawQr = $"CINEMA-TICKET:{tid}:{reservationId}:{sid}:{Guid.NewGuid():N}";
                var newQrHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(newRawQr)));

                ticketIds.Add(tid);
                qrHashes.Add(newQrHash);

                passes.Add(new TicketReissuePassItem(
                    TicketId: tid,
                    SeatId: sid,
                    RowLabel: (string)tr.row_label,
                    SeatNumber: (int)tr.seat_number,
                    SeatType: (string)tr.seat_type,
                    QrTokenHash: newQrHash
                ));
            }

            const string batchUpdateSql = @"
                UPDATE tickets.tickets AS t
                SET qr_token_hash = u.qr_hash,
                    is_printed = false,
                    printed_at = NULL
                FROM (
                    SELECT unnest(@TicketIds) AS ticket_id,
                           unnest(@QrHashes) AS qr_hash
                ) AS u
                WHERE t.ticket_id = u.ticket_id;";

            await conn.ExecuteAsync(batchUpdateSql, new
            {
                TicketIds = ticketIds.ToArray(),
                QrHashes = qrHashes.ToArray()
            }, tx);

            await conn.ExecuteAsync(@"
                INSERT INTO public.audit_log (service_name, actor_id, action, entity_type, entity_id, before_data, after_data)
                VALUES ('ticket', gen_random_uuid(), 'TICKET_REISSUED', 'reservation', @ReservationId,
                        jsonb_build_object('qrRegenerated', true, 'ticketsCount', @TicketsCount),
                        jsonb_build_object('customerEmail', @CustomerEmail))",
                new { ReservationId = reservationId, TicketsCount = passes.Count, CustomerEmail = (string?)header.customer_email },
                tx);

            await tx.CommitAsync();
        }
        else
        {
            foreach (var tr in ticketRows)
            {
                passes.Add(new TicketReissuePassItem(
                    TicketId: (Guid)tr.ticket_id,
                    SeatId: (Guid)tr.seat_id,
                    RowLabel: (string)tr.row_label,
                    SeatNumber: (int)tr.seat_number,
                    SeatType: (string)tr.seat_type,
                    QrTokenHash: (string)tr.qr_token_hash
                ));
            }
        }

        return (
            Success: true,
            TicketsCount: passes.Count,
            Passes: passes,
            CustomerEmail: (string?)header.customer_email,
            CustomerName: (string?)header.customer_name,
            MovieTitle: (string)header.movie_title,
            BranchName: (string)header.branch_name,
            AuditoriumName: (string)header.auditorium_name,
            ShowtimeStart: (DateTimeOffset)header.showtime_start,
            TotalAmount: (decimal)header.total_amount,
            BookingRef: (string?)header.booking_reference,
            OrderId: (Guid?)header.order_id
        );
    }

    public async Task MarkTicketsEmailSentAsync(Guid reservationId, string recipientEmail)
    {
        using var conn = _dbFactory.CreateConnection();
        const string sql = @"
            UPDATE tickets.tickets 
            SET email_sent = true, 
                email_sent_at = now(), 
                email_recipient = @Recipient
            WHERE reservation_id = @ReservationId AND status = 'active'";

        await conn.ExecuteAsync(sql, new { ReservationId = reservationId, Recipient = recipientEmail });
    }

    public async Task<FlagDisputeResponse> FlagDisputeAsync(Guid reservationId, FlagDisputeRequest request)
    {
        using var conn = (DbConnection)_dbFactory.CreateConnection();
        await conn.OpenAsync();

        // 1. Fetch reservation & associated order
        const string resSql = @"
            SELECT r.reservation_id, o.order_id
            FROM reservations.reservations r
            LEFT JOIN pos.orders o ON o.reservation_id = r.reservation_id
            WHERE r.reservation_id = @ReservationId";

        var res = await conn.QuerySingleOrDefaultAsync(resSql, new { ReservationId = reservationId });
        if (res == null)
        {
            throw new KeyNotFoundException($"Reservation {reservationId} not found.");
        }

        Guid? orderId = (Guid?)res.order_id;
        var disputeId = Guid.NewGuid();

        var validStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "open", "under_review", "won", "lost", "accepted"
        };

        if (!string.IsNullOrWhiteSpace(request.Status) && !validStatuses.Contains(request.Status.Trim()))
        {
            throw new ArgumentException($"Invalid dispute status '{request.Status}'. Allowed values: {string.Join(", ", validStatuses)}.");
        }

        var status = !string.IsNullOrWhiteSpace(request.Status)
            ? request.Status.Trim().ToLowerInvariant()
            : "open";

        const string insertSql = @"
            INSERT INTO pos.disputes (dispute_id, order_id, reservation_id, provider_dispute_id, status, amount, evidence_notes, created_at)
            VALUES (@DisputeId, @OrderId, @ReservationId, @ProviderDisputeId, @Status, @Amount, @EvidenceNotes, now())";

        await conn.ExecuteAsync(insertSql, new
        {
            DisputeId = disputeId,
            OrderId = orderId,
            ReservationId = reservationId,
            ProviderDisputeId = request.ProviderDisputeId,
            Status = status,
            Amount = request.Amount,
            EvidenceNotes = request.EvidenceNotes
        });

        // Insert audit log
        await conn.ExecuteAsync(@"
            INSERT INTO public.audit_log (service_name, actor_id, action, entity_type, entity_id, before_data, after_data)
            VALUES ('pos', gen_random_uuid(), 'DISPUTE_FLAGGED', 'pos.disputes', @DisputeId,
                    NULL,
                    jsonb_build_object('reservationId', @ReservationId, 'orderId', @OrderId, 'amount', @Amount, 'status', @Status, 'providerDisputeId', @ProviderDisputeId))",
            new { DisputeId = disputeId, ReservationId = reservationId, OrderId = orderId, Amount = request.Amount, Status = status, ProviderDisputeId = request.ProviderDisputeId });

        return new FlagDisputeResponse(
            DisputeId: disputeId,
            ReservationId: reservationId,
            OrderId: orderId,
            ProviderDisputeId: request.ProviderDisputeId,
            Status: status,
            Amount: request.Amount,
            EvidenceNotes: request.EvidenceNotes,
            CreatedAt: DateTimeOffset.UtcNow
        );
    }
}
