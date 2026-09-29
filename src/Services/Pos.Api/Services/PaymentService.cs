using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Cinema.Foundation.Data;
using Cinema.Foundation.Messaging;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using Pos.Api.Models;
using Pos.Api.Repositories;

namespace Pos.Api.Services;

public class PaymentService : IPaymentService
{
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly IPosRepository _repository;
    private readonly IDbConnectionFactory _dbFactory;
    private readonly IEventBus _eventBus;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        IPosRepository repository, 
        IDbConnectionFactory dbFactory, 
        IEventBus eventBus, 
        IConfiguration configuration,
        ILogger<PaymentService> logger)
    {
        _repository = repository;
        _dbFactory = dbFactory;
        _eventBus = eventBus;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<IResult> ProcessPaymentAsync(Guid orderId, ProcessPaymentRequest request)
    {
        using var conn = (NpgsqlConnection)_dbFactory.CreateConnection();
        await conn.OpenAsync();
        using var tx = await conn.BeginTransactionAsync();

        var order = await _repository.GetOrderByIdAsync(orderId, conn, tx);
        if (order == null)
        {
            await tx.RollbackAsync();
            return Results.NotFound(new { error = $"Order {orderId} not found." });
        }

        if ((string)order.status == Constants.OrderStatuses.Paid)
        {
            await tx.RollbackAsync();
            return Results.BadRequest(new { error = "Order is already paid." });
        }

        var paymentId = Guid.NewGuid();
        string method = request.Method.ToLowerInvariant();
        if (!Constants.PaymentMethods.ValidMethods.Contains(method))
        {
            method = Constants.PaymentMethods.Other;
        }

        try
        {
            decimal previouslyPaid = await _repository.GetTotalCapturedPaymentsAsync(orderId, conn, tx);
            decimal orderTotal = (decimal)order.total_amount;
            decimal remainingBeforePayment = Math.Max(0, orderTotal - previouslyPaid);
            
            decimal chargeAmount = request.Amount;
            if (method != Constants.PaymentMethods.Cash && chargeAmount > remainingBeforePayment)
            {
                chargeAmount = remainingBeforePayment; 
            }

            await _repository.InsertPaymentAsync(paymentId, orderId, method, chargeAmount, request.ProviderReference, conn, tx);

            decimal totalPaid = previouslyPaid + chargeAmount;
            bool orderFullyPaid = totalPaid >= orderTotal;
            decimal changeGiven = 0;
            decimal remainingBalance = Math.Max(0, orderTotal - totalPaid);

            if (orderFullyPaid)
            {
                changeGiven = Math.Max(0, totalPaid - orderTotal);
                await _repository.UpdateOrderStatusToPaidAsync(orderId, conn, tx);
            }

            await _repository.InsertOutboxMessageAsync("payment.captured", new {
                OrderId = orderId,
                Amount = chargeAmount,
                Method = method,
                PaymentId = paymentId
            }, conn, tx);
            
            if (orderFullyPaid && order.customer_email != null)
            {
                await _repository.InsertOutboxMessageAsync("order.completed", new {
                    OrderId = orderId,
                    CustomerEmail = (string)order.customer_email,
                    TotalAmount = orderTotal
                }, conn, tx);
            }

            await tx.CommitAsync();

            bool drawerKickSignal = method == Constants.PaymentMethods.Cash && orderFullyPaid;
            if (drawerKickSignal)
            {
                _logger.LogInformation("Sending signal to kick cash drawer for terminal.");
            }

            return Results.Ok(new
            {
                orderId,
                status = orderFullyPaid ? Constants.OrderStatuses.Paid : (string)order.status,
                totalAmount = orderTotal,
                paidAmount = totalPaid,
                remainingBalance,
                changeGiven,
                drawerKick = drawerKickSignal
            });
        }
        catch (Exception)
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<IResult> ProcessGuestCheckoutAsync(GuestCheckoutRequest request, Guid? idempotencyKeyHeader)
    {
        var effectiveKey = idempotencyKeyHeader ?? request.IdempotencyKey ?? Guid.NewGuid();
        var existingOrder = await _repository.GetOrderByIdempotencyKeyAsync(effectiveKey);
        if (existingOrder != null)
        {
            return Results.Ok(new { message = "Idempotent replay", orderId = existingOrder.order_id, status = existingOrder.status });
        }

        using var conn = (NpgsqlConnection)_dbFactory.CreateConnection();
        await conn.OpenAsync();
        using var tx = await conn.BeginTransactionAsync();

        try
        {
            var res = await _repository.GetReservationForGuestCheckoutAsync(request.ReservationId, conn, tx);
            if (res == null)
            {
                await tx.RollbackAsync();
                return Results.NotFound(new { error = $"Reservation {request.ReservationId} not found." });
            }

            string resStatus = (string)res.status;
            if (resStatus == "confirmed")
            {
                await tx.RollbackAsync();
                return Results.Conflict(new { error = "Reservation is already confirmed and paid." });
            }
            if (resStatus != "hold")
            {
                await tx.RollbackAsync();
                return Results.BadRequest(new { error = $"Reservation is in status '{resStatus}' and cannot be paid." });
            }

            if (res.hold_expires_at != null && (DateTimeOffset)res.hold_expires_at < DateTimeOffset.UtcNow)
            {
                await tx.RollbackAsync();
                return Results.BadRequest(new { error = "Seat hold has expired. Please select seats again." });
            }

            var seats = await _repository.GetReservationSeatsAsync(request.ReservationId, conn, tx);
            if (seats.Count == 0 && request.SeatIds != null && request.SeatIds.Count > 0)
            {
                const string querySeatsSql = @"SELECT s.seat_id, s.row_label, s.seat_number, s.seat_type FROM catalog.seats s JOIN catalog.showtimes st ON st.showtime_id = @ShowtimeId WHERE s.seat_id = ANY(@SeatIds)";
                var fallback = (await Dapper.SqlMapper.QueryAsync<dynamic>(conn, querySeatsSql, new { ShowtimeId = (Guid)res.showtime_id, SeatIds = request.SeatIds.ToArray() }, tx)).ToList();
                seats = fallback;
            }

            if (seats.Count == 0)
            {
                const string firstSeatSql = @"SELECT s.seat_id, s.row_label, s.seat_number, s.seat_type FROM catalog.seats s JOIN catalog.showtimes st ON st.showtime_id = @ShowtimeId LIMIT 1";
                var defaultSeats = (await Dapper.SqlMapper.QueryAsync<dynamic>(conn, firstSeatSql, new { ShowtimeId = (Guid)res.showtime_id }, tx)).ToList();
                seats = defaultSeats;
            }

            if (seats.Count == 0)
            {
                await tx.RollbackAsync();
                return Results.BadRequest(new { error = "Reservation has no assigned seats and no seats found for showtime." });
            }

            Guid showtimeId = (Guid)res.showtime_id;
            Guid branchId = request.BranchId ?? (Guid)res.branch_id;
            decimal basePrice = (decimal)res.base_price;
            List<Guid> seatIds = seats.Select(s => (Guid)s.seat_id).ToList();

            decimal totalAmount = basePrice * seats.Count;

            var processedConcessionLines = new List<OrderLineRequest>();
            string? resolvedFood = null;

            if (request.Concessions != null && request.Concessions.Count > 0)
            {
                var productIds = request.Concessions
                    .Where(l => l.ProductId.HasValue)
                    .Select(l => l.ProductId!.Value)
                    .ToList();

                var dbProducts = productIds.Count > 0 
                    ? (await _repository.GetProductsByIdsAsync(productIds, conn, tx)).ToDictionary(p => p.ProductId)
                    : new Dictionary<Guid, ProductDto>();

                var foodItemsSummary = new List<string>();

                foreach (var line in request.Concessions)
                {
                    if (line.ProductId.HasValue && dbProducts.TryGetValue(line.ProductId.Value, out var prod))
                    {
                        decimal lineTotal = (prod.UnitPrice * line.Quantity) - line.DiscountAmount;
                        totalAmount += lineTotal;
                        processedConcessionLines.Add(line with { UnitPrice = prod.UnitPrice, Description = prod.Name });
                        foodItemsSummary.Add($"{line.Quantity}x {prod.Name}");
                    }
                }
                
                if (foodItemsSummary.Count > 0)
                {
                    resolvedFood = string.Join(", ", foodItemsSummary);
                }
            }

            if (request.Amount > 0 && Math.Abs(request.Amount - totalAmount) > 0.01m)
            {
                await tx.RollbackAsync();
                return Results.BadRequest(new { error = $"Amount mismatch. Expected {totalAmount}, received {request.Amount}" });
            }

            Guid orderId = Guid.NewGuid();
            Guid paymentId = Guid.NewGuid();
            string method = request.PaymentMethod.ToLowerInvariant();
            
            string bookingRef = $"#ORD-{orderId.ToString("N")[..6].ToUpperInvariant()}";
            int bookingNumber = await _repository.InsertGuestOrderAsync(orderId, branchId, request.ReservationId, totalAmount, effectiveKey, (request.GuestEmail ?? string.Empty).Trim(), request.GuestPhone, bookingRef, request.CustomerId, conn, tx);

            if (processedConcessionLines.Count > 0)
            {
                foreach (var line in processedConcessionLines)
                {
                    await _repository.InsertOrderLineAsync(orderId, line, conn, tx);
                }
            }

            await _repository.InsertPaymentAsync(paymentId, orderId, method, totalAmount, request.ProviderReference ?? "GUEST-PAY", conn, tx);
            await _repository.UpdateOrderStatusToPaidAsync(orderId, conn, tx);
            await _repository.ConfirmReservationAndSeatsAsync(request.ReservationId, showtimeId, seatIds, (request.GuestEmail ?? string.Empty).Trim(), request.GuestPhone, request.GuestName, conn, tx);
            var createdTickets = await _repository.CreateTicketsAndReturnDetailsAsync(request.ReservationId, showtimeId, seatIds, (request.GuestEmail ?? string.Empty).Trim(), conn, tx);

            var ticketSeatItems = new List<TicketSeatItem>();
            var ticketResults = new List<object>();

            foreach (var t in createdTickets)
            {
                Guid tid = (Guid)t.ticket_id;
                Guid sid = (Guid)t.seat_id;
                var seatObj = seats.FirstOrDefault(s => (Guid)s.seat_id == sid);
                string rowLabel = seatObj?.row_label ?? "A";
                int seatNum = seatObj != null ? (int)seatObj.seat_number : 1;
                string seatType = seatObj?.seat_type ?? "standard";
                string rawQr = (string)t.qr_token;

                ticketSeatItems.Add(new TicketSeatItem(tid, rowLabel, seatNum, seatType, rawQr));

                var signedUrl = Cinema.Foundation.Security.SignedTicketUrlService.BuildSignedETicketUrl(
                    _configuration,
                    tid,
                    TimeSpan.FromDays(7)
                );

                ticketResults.Add(new
                {
                    ticketId = tid,
                    seat = $"Row {rowLabel}, Seat {seatNum}",
                    seatType,
                    status = "active",
                    eTicketUrl = signedUrl
                });
            }

            var ticketEvent = new TicketIssuedIntegrationEvent(
                OrderId: orderId,
                ReservationId: request.ReservationId,
                CustomerEmail: (request.GuestEmail ?? string.Empty).Trim(),
                CustomerPhone: request.GuestPhone,
                CustomerName: request.GuestName,
                MovieTitle: (string)res.movie_title,
                BranchName: (string)res.branch_name,
                AuditoriumName: (string)res.auditorium_name,
                ShowtimeStart: (DateTimeOffset)res.starts_at,
                Seats: ticketSeatItems,
                TotalAmount: totalAmount,
                IssuedAt: DateTimeOffset.UtcNow,
                FoodAndBeverage: resolvedFood,
                BookingNumber: bookingNumber,
                BookingReference: bookingRef
            );

            await _repository.InsertOutboxMessageAsync("ticket.issued", ticketEvent, conn, tx);
            
            if (!string.IsNullOrWhiteSpace(request.GuestEmail))
            {
                await _repository.InsertOutboxMessageAsync("order.completed", new {
                    OrderId = orderId,
                    CustomerEmail = request.GuestEmail.Trim(),
                    TotalAmount = totalAmount
                }, conn, tx);
            }

            await tx.CommitAsync();

            return Results.Ok(new
            {
                orderId,
                reservationId = request.ReservationId,
                customerEmail = request.GuestEmail?.Trim(),
                status = "paid",
                totalAmount,
                paymentMethod = method,
                tickets = ticketResults,
                message = "Guest checkout successful. Digital e-tickets have been issued and dispatched via email."
            });
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(ex, "Failed to process guest checkout for Reservation {ReservationId}", request.ReservationId);
            return Results.BadRequest(new { error = ex.Message });
        }
    }
}


