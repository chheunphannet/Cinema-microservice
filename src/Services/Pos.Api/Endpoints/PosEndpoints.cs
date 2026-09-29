using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Pos.Api.Models;
using Pos.Api.Services;

namespace Pos.Api.Endpoints;

public static class PosEndpoints
{
    public static void MapPosEndpoints(this IEndpointRouteBuilder routes)
    {
        var pos = routes.MapGroup("/api/v1/pos")
            .WithTags("Point of Sale (POS)")
            .RequireAuthorization();

        pos.MapGet("/health-contract", () => Results.Ok(new
        {
            schema = "pos",
            transactions = "Strict ACID guarantees via Npgsql BeginTransactionAsync",
            webhooks = "7-step Redis Idempotency State Machine to prevent duplicate payments"
        }))
        .WithSummary("POS Service Architectural Contract")
        .WithDescription("Defines the POS bounding context, strict transactional boundaries, and idempotent webhook safety.");

        pos.MapGet("/products", async (
            [FromQuery] Guid? branchId,
            IProductService productService) =>
        {
            return await productService.GetProductsAsync(branchId);
        })
        .AllowAnonymous()
        .WithSummary("Get POS Product Catalog")
        .WithDescription("Retrieves popcorn, drinks, and combos available at a specific branch.");

        pos.MapGet("/products/search", async (
            [AsParameters] ProductSearchRequest request,
            IProductService productService) =>
        {
            return await productService.SearchProductsAsync(request);
        })
        .AllowAnonymous()
        .WithSummary("Search & Filter F&B Concessions")
        .WithDescription("Multi-faceted F&B product search supporting categories, dietary tags, branch inventory, price ranges, and loyalty tier exclusivity.");

        pos.MapGet("/products/upsells", async (
            [FromQuery] Guid? branchId,
            [FromQuery] Guid[]? cartProductIds,
            IProductService productService) =>
        {
            return await productService.GetUpsellProductsAsync(branchId, cartProductIds ?? Array.Empty<Guid>());
        })
        .AllowAnonymous()
        .WithSummary("Get Cart Upsell Suggestions")
        .WithDescription("Recommends complementary high-margin F&B items based on items currently in cart.");

        pos.MapPost("/shifts/open", async (
            [FromBody] OpenShiftRequest request,
            IShiftService shiftService, HttpContext context) =>
        {
            var tokenBranch = context.User.FindFirst("branch_id")?.Value;
            if (tokenBranch != null && tokenBranch != request.BranchId.ToString()) return Results.Forbid();
            return await shiftService.OpenShiftAsync(request);
        })
        .WithSummary("Open Cashier Till Shift")
        .WithDescription("Records the opening cash float for a cashier terminal.");

        pos.MapPost("/shifts/close", async (
            [FromBody] CloseShiftRequest request,
            IShiftService shiftService) =>
        {
            return await shiftService.CloseShiftAsync(request);
        })
        .WithSummary("Close Cashier Till Shift")
        .WithDescription("Records closing cash and calculates drawer discrepancy.");

        pos.MapPost("/orders", async (
            [FromHeader(Name = "X-Idempotency-Key")] Guid? idempotencyKeyHeader,
            [FromBody] CreateOrderRequest request,
            IOrderService orderService, HttpContext context) =>
        {
            var tokenBranch = context.User.FindFirst("branch_id")?.Value;
            if (tokenBranch != null && tokenBranch != request.BranchId.ToString()) return Results.Forbid();
            return await orderService.CreateOrderAsync(request, idempotencyKeyHeader);
        })
        .WithSummary("Create POS Order")
        .WithDescription("Creates a pending order transactionally linking F&B products and/or a seat reservation.");

        pos.MapPost("/orders/{orderId:guid}/payments", async (
            Guid orderId,
            [FromBody] ProcessPaymentRequest request,
            [FromHeader(Name = "X-Idempotency-Key")] string? idempotencyKey,
            IPaymentService paymentService,
            StackExchange.Redis.IConnectionMultiplexer redis) =>
        {
            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                var db = redis.GetDatabase();
                var key = $"idempotency:payment:{idempotencyKey}";
                bool isNew = await db.StringSetAsync(key, "processing", TimeSpan.FromHours(24), StackExchange.Redis.When.NotExists);
                if (!isNew) return Results.Conflict(new { error = "Payment request with this idempotency key was already processed." });
            }
            return await paymentService.ProcessPaymentAsync(orderId, request);
        })
        .WithSummary("Process Payment (Local/Cash)")
        .WithDescription("Records a manual payment, verifies total, marks order paid, and publishes RabbitMQ payment.captured event.");

        pos.MapPost("/orders/{orderId:guid}/cancel", async (
            Guid orderId,
            IOrderService orderService) =>
        {
            return await orderService.CancelOrderAsync(orderId);
        })
        .WithSummary("Cancel Order")
        .WithDescription("Cancels an unpaid or partially paid order and prompts for physical refund.");

        pos.MapPost("/payments/webhook", async (
            [FromBody] PaymentWebhookPayload? payload,
            IWebhookIdempotencyService webhookService) =>
        {
            return await webhookService.HandleWebhookAsync(payload);
        })
        .AllowAnonymous()
        .WithSummary("Phase 2 Module 2.1: Payment Webhook with Distributed Idempotency")
        .WithDescription("Handles external payment provider webhooks (e.g., ABA Pay, Stripe). Implements 7-step idempotency state machine using Redis to prevent race conditions and duplicate processing.");

        pos.MapPost("/checkout/guest", async (
            [FromBody] GuestCheckoutRequest request,
            [FromHeader(Name = "X-Idempotency-Key")] Guid? idempotencyKeyHeader,
            IPaymentService paymentService) =>
        {
            return await paymentService.ProcessGuestCheckoutAsync(request, idempotencyKeyHeader);
        })
        .AllowAnonymous()
        .WithSummary("Phase 3: Frictionless Guest Checkout")
        .WithDescription("Allows public web customers to finalize booking and complete payment using only their email address. Issues digital tickets and triggers email dispatching.");

        routes.MapPost("/api/v1/checkout/guest", async (
            [FromBody] GuestCheckoutRequest request,
            [FromHeader(Name = "X-Idempotency-Key")] Guid? idempotencyKeyHeader,
            IPaymentService paymentService) =>
        {
            return await paymentService.ProcessGuestCheckoutAsync(request, idempotencyKeyHeader);
        })
        .AllowAnonymous()
        .WithTags("Point of Sale (POS)")
        .WithSummary("Phase 3: Guest Checkout Direct Gateway Alias");

        routes.MapPost("/api/v1/checkout/customer", async (
            [FromBody] GuestCheckoutRequest request,
            [FromHeader(Name = "X-Idempotency-Key")] Guid? idempotencyKeyHeader,
            HttpContext context,
            IPaymentService paymentService) =>
        {
            var customerIdClaim = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(customerIdClaim) || !Guid.TryParse(customerIdClaim, out var customerId))
            {
                return Results.Unauthorized();
            }

            var emailClaim = context.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;

            // Override guest info with authenticated user info
            var authRequest = request with { 
                CustomerId = customerId, 
                GuestEmail = emailClaim ?? request.GuestEmail 
            };
            
            return await paymentService.ProcessGuestCheckoutAsync(authRequest, idempotencyKeyHeader);
        })
        .RequireAuthorization(policy => policy.RequireRole("customer"))
        .WithTags("Customer Portal Auth")
        .WithSummary("Authenticated Customer Web Checkout");

        pos.MapGet("/orders/{orderId:guid}/concessions", async (
            Guid orderId,
            Pos.Api.Repositories.IPosRepository repository) =>
        {
            var lines = await repository.GetOrderLinesByOrderIdAsync(orderId);
            if (lines.Count == 0)
            {
                return Results.NotFound(new { error = $"No concessions found for order {orderId}." });
            }

            string voucherCode = $"FNB-{orderId:N}"[..12].ToUpperInvariant();
            string qrPayload = $"CINEMA-FNB:{orderId}:{voucherCode}";
            string qrSvg = Cinema.Foundation.Email.QrCodeHelper.GenerateSvg(qrPayload, 8);

            bool allCollected = lines.All(l => (string)l.fulfillment_status == "collected");

            return Results.Ok(new
            {
                orderId,
                voucherCode,
                qrPayload,
                qrSvg,
                overallStatus = allCollected ? "collected" : "pending",
                items = lines.Select(l => new
                {
                    orderLineId = (Guid)l.order_line_id,
                    description = (string)l.description,
                    quantity = (int)l.quantity,
                    unitPrice = (decimal)l.unit_price,
                    lineTotal = (decimal)l.line_total,
                    fulfillmentStatus = (string)l.fulfillment_status,
                    fulfilledAt = (DateTimeOffset?)l.fulfilled_at
                })
            });
        })
        .RequireAuthorization("Cashier")
        .WithSummary("Get Concession Pickup Voucher")
        .WithDescription("Retrieves F&B concession items, pickup barcode QR, and fulfillment status for cinema counter pickup.");

        pos.MapPost("/orders/{orderId:guid}/concessions/fulfill", async (
            Guid orderId,
            HttpContext context,
            Pos.Api.Repositories.IPosRepository repository) =>
        {
            var userIdClaim = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            Guid? staffId = null;
            if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var parsedStaffId))
            {
                staffId = parsedStaffId;
            }

            var updated = await repository.FulfillOrderConcessionsAsync(orderId, staffId);
            if (updated.Count == 0)
            {
                return Results.NotFound(new { error = $"No pending concessions found for order {orderId}." });
            }

            return Results.Ok(new
            {
                orderId,
                status = "collected",
                message = "Concession items successfully marked as fulfilled/collected.",
                fulfilledItemsCount = updated.Count,
                fulfilledAt = DateTimeOffset.UtcNow
            });
        })
        .RequireAuthorization("Cashier")
        .WithSummary("Fulfill Concession Order Lines")
        .WithDescription("Allows concession counter staff to scan customer barcode and mark F&B items as fulfilled/collected.");

        pos.MapPost("/orders/scan-concessions", async (
            [FromBody] ScanConcessionRequest request,
            HttpContext context,
            Pos.Api.Repositories.IPosRepository repository) =>
        {
            if (string.IsNullOrWhiteSpace(request.BarcodeOrToken))
            {
                return Results.BadRequest(new { error = "BarcodeOrToken is required." });
            }

            var orderId = await repository.FindOrderIdByReferenceOrTokenAsync(request.BarcodeOrToken);
            if (!orderId.HasValue)
            {
                return Results.NotFound(new { error = $"Could not find any order matching scan code '{request.BarcodeOrToken}'." });
            }

            var userIdClaim = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            Guid? staffId = null;
            if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var parsedStaffId))
            {
                staffId = parsedStaffId;
            }

            if (request.AutoFulfill == true)
            {
                await repository.FulfillOrderConcessionsAsync(orderId.Value, staffId);
            }

            var lines = await repository.GetOrderLinesByOrderIdAsync(orderId.Value);
            if (lines.Count == 0)
            {
                return Results.NotFound(new { error = $"No concessions found for order {orderId.Value}." });
            }

            string voucherCode = $"FNB-{orderId.Value:N}"[..12].ToUpperInvariant();
            bool allCollected = lines.All(l => (string)l.fulfillment_status == "collected");

            return Results.Ok(new
            {
                orderId = orderId.Value,
                voucherCode,
                overallStatus = allCollected ? "collected" : "pending",
                scannedToken = request.BarcodeOrToken,
                items = lines.Select(l => new
                {
                    orderLineId = (Guid)l.order_line_id,
                    description = (string)l.description,
                    quantity = (int)l.quantity,
                    unitPrice = (decimal)l.unit_price,
                    lineTotal = (decimal)l.line_total,
                    fulfillmentStatus = (string)l.fulfillment_status,
                    fulfilledAt = (DateTimeOffset?)l.fulfilled_at
                })
            });
        })
        .RequireAuthorization("Cashier")
        .WithSummary("Concession Counter Scanner Endpoint")
        .WithDescription("Allows popcorn/snack counter scanners to decode email master QR (CINEMA-ORDER:...), concession voucher (CINEMA-FNB:...), or booking reference to display or fulfill snacks.");

        routes.MapGet("/api/v1/orders/me", async (
            HttpContext context,
            Cinema.Foundation.Data.IDbConnectionFactory dbFactory) =>
        {
            var customerIdClaim = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(customerIdClaim) || !Guid.TryParse(customerIdClaim, out var customerId))
            {
                return Results.Unauthorized();
            }

            using var conn = dbFactory.CreateConnection();
            var sql = "SELECT order_id as OrderId, total_amount as TotalAmount, created_at as CreatedAt, status as Status, booking_reference as BookingReference FROM pos.orders WHERE customer_id = @CustomerId ORDER BY created_at DESC";
            var orders = await Dapper.SqlMapper.QueryAsync<dynamic>(conn, sql, new { CustomerId = customerId });

            return Results.Ok(orders);
        })
        .RequireAuthorization(policy => policy.RequireRole("customer"))
        .WithTags("Customer Portal Auth")
        .WithSummary("Get Customer Order History");
    }
}
