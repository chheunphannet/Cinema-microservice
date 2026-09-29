using Cinema.Foundation.Data;
using Microsoft.AspNetCore.Http;
using Npgsql;
using Pos.Api.Models;
using Pos.Api.Repositories;

namespace Pos.Api.Services;

public class OrderService : IOrderService
{
    private readonly IPosRepository _repository;
    private readonly IDbConnectionFactory _dbFactory;
    private readonly IComboEngineService _comboEngine;
    private readonly IHttpClientFactory _httpClientFactory;

    public OrderService(IPosRepository repository, IDbConnectionFactory dbFactory, IComboEngineService comboEngine, IHttpClientFactory httpClientFactory)
    {
        _repository = repository;
        _dbFactory = dbFactory;
        _comboEngine = comboEngine;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IResult> CreateOrderAsync(CreateOrderRequest request, Guid? idempotencyKeyHeader)
    {
        var effectiveKey = idempotencyKeyHeader ?? Guid.NewGuid();

        var existing = await _repository.GetOrderByIdempotencyKeyAsync(effectiveKey);
        if (existing != null)
        {
            return Results.Ok(new OrderResponse(
                OrderId: (Guid)existing.order_id,
                BranchId: (Guid)existing.branch_id,
                CashierId: (Guid)existing.cashier_id,
                ReservationId: (Guid?)existing.reservation_id,
                Status: (string)existing.status,
                Subtotal: (decimal)existing.subtotal,
                DiscountAmount: (decimal)existing.discount_amount,
                TotalAmount: (decimal)existing.total_amount,
                IdempotencyKey: (Guid)existing.idempotency_key,
                CreatedAt: (DateTimeOffset)existing.created_at
            ));
        }

        var orderId = Guid.NewGuid();
        var subtotal = request.Lines.Sum(l => l.Quantity * l.UnitPrice);
        
        // Phase 2 Step 3: Run the Combo Engine on the cart items to determine applicable combo discounts
        var (comboDiscount, updatedLines) = await _comboEngine.ApplyComboDiscountsAsync(request.Lines);
        var totalDiscount = request.DiscountAmount + comboDiscount;

        // Phase 2 Step 4: Loyalty Voucher Validation
        if (!string.IsNullOrWhiteSpace(request.VoucherCode))
        {
            try 
            {
                var client = _httpClientFactory.CreateClient("LoyaltyClient");
                var voucherResponse = await client.PostAsJsonAsync("http://loyalty-api:8080/api/v1/loyalty/vouchers/validate", new { Code = request.VoucherCode });
                if (voucherResponse.IsSuccessStatusCode)
                {
                    var voucher = await voucherResponse.Content.ReadFromJsonAsync<VoucherDto>();
                    if (voucher != null && voucher.VoucherType == "fixed_discount")
                    {
                        totalDiscount += voucher.DiscountValue;
                    }
                }
            }
            catch (Exception ex)
            {
                // Fallback: If Identity API is down, rate-limited, or circuit is broken, 
                // we skip voucher validation and let the POS continue processing the order 
                // without crashing, although the discount won't be applied.
                Console.WriteLine($"[Resilience Fallback] Failed to reach Identity API for voucher validation. Error: {ex.Message}");
            }
        }
        
        var total = Math.Max(0m, subtotal - totalDiscount);

        using var conn = (NpgsqlConnection)_dbFactory.CreateConnection();
        await conn.OpenAsync();
        using var tx = await conn.BeginTransactionAsync();
        
        try
        {
            await _repository.InsertOrderAsync(orderId, request.BranchId, request.CashierId, request.ReservationId, subtotal, totalDiscount, total, effectiveKey, request.CustomerEmail, request.VoucherCode, conn, tx);

            foreach (var line in updatedLines)
            {
                await _repository.InsertOrderLineAsync(orderId, line, conn, tx);
            }

            await tx.CommitAsync();

            return Results.Created($"/api/v1/pos/orders/{orderId}", new OrderResponse(
                OrderId: orderId,
                BranchId: request.BranchId,
                CashierId: request.CashierId,
                ReservationId: request.ReservationId,
                Status: Constants.OrderStatuses.PendingPayment,
                Subtotal: subtotal,
                DiscountAmount: totalDiscount,
                TotalAmount: total,
                IdempotencyKey: effectiveKey,
                CreatedAt: DateTimeOffset.UtcNow
            ));
        }
        catch (Exception)
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<IResult> CancelOrderAsync(Guid orderId)
    {
        using var conn = (NpgsqlConnection)_dbFactory.CreateConnection();
        await conn.OpenAsync();
        using var tx = await conn.BeginTransactionAsync();

        try
        {
            var order = await _repository.GetOrderByIdAsync(orderId, conn, tx);
            if (order == null)
            {
                await tx.RollbackAsync();
                return Results.NotFound(new { error = $"Order {orderId} not found." });
            }

            if ((string)order.status == Constants.OrderStatuses.Paid)
            {
                await tx.RollbackAsync();
                return Results.BadRequest(new { error = "Cannot cancel a fully paid order. Use refund instead." });
            }

            // Mark order as cancelled
            await _repository.UpdateOrderStatusToCancelledAsync(orderId, conn, tx);

            // Fetch any partial payments that need to be refunded physically
            var totalPaid = await _repository.GetTotalCapturedPaymentsAsync(orderId, conn, tx);

            await tx.CommitAsync();

            return Results.Ok(new
            {
                orderId,
                status = "cancelled",
                refundRequired = totalPaid > 0,
                refundAmount = totalPaid,
                message = totalPaid > 0 
                    ? $"Order cancelled. Please refund {totalPaid:C2} to the customer." 
                    : "Order cancelled successfully."
            });
        }
        catch (Exception)
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}
