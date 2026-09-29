using System.Data.Common;
using Cinema.Foundation.Data;
using Dapper;
using Pos.Api.Models;

namespace Pos.Api.Repositories;

public class PosRepository : IPosRepository
{
    private readonly IDbConnectionFactory _dbFactory;

    public PosRepository(IDbConnectionFactory dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<IEnumerable<ProductDto>> GetActiveProductsAsync(Guid? branchId)
    {
        using var conn = _dbFactory.CreateReadConnection();
        const string sql = @"
            SELECT product_id as ProductId, sku as Sku, name as Name, 
                   category as Category, unit_price as UnitPrice, 
                   image_url as ImageUrl, badge_text as BadgeText, description as Description,
                   is_active as IsActive
            FROM pos.products
            WHERE (@BranchId IS NULL OR branch_id = @BranchId) AND is_active = true
            ORDER BY category, name";

        return await conn.QueryAsync<ProductDto>(sql, new { BranchId = branchId });
    }

    public async Task<List<ProductDto>> GetProductsByIdsAsync(IEnumerable<Guid> productIds, DbConnection conn, DbTransaction? tx = null)
    {
        var idList = productIds.ToList();
        if (idList.Count == 0) return new List<ProductDto>();

        const string sql = @"
            SELECT product_id as ProductId, sku as Sku, name as Name, 
                   category as Category, unit_price as UnitPrice, 
                   image_url as ImageUrl, badge_text as BadgeText, description as Description,
                   is_active as IsActive
            FROM pos.products
            WHERE product_id = ANY(@ProductIds)";

        var results = await conn.QueryAsync<ProductDto>(sql, new { ProductIds = idList.ToArray() }, tx);
        return results.ToList();
    }

    public async Task<List<ComboDto>> GetActiveCombosAsync()
    {
        using var conn = _dbFactory.CreateReadConnection();
        const string combosSql = @"
            SELECT combo_id, name, price 
            FROM catalog.combos 
            WHERE is_active = true";
            
        var combosRaw = await conn.QueryAsync<dynamic>(combosSql);
        
        const string itemsSql = @"
            SELECT combo_id, item_type, target_id, quantity 
            FROM catalog.combo_items";
            
        var itemsRaw = await conn.QueryAsync<dynamic>(itemsSql);
        
        var result = new List<ComboDto>();
        foreach(var c in combosRaw)
        {
            var comboId = (Guid)c.combo_id;
            var combo = new ComboDto 
            { 
                ComboId = comboId, 
                Name = (string)c.name, 
                Price = (decimal)c.price 
            };
            
            var items = itemsRaw.Where(i => (Guid)i.combo_id == comboId);
            foreach(var i in items)
            {
                combo.Items.Add(new ComboItemDto 
                {
                    TargetId = (Guid)i.target_id,
                    ItemType = (string)i.item_type,
                    Quantity = (int)i.quantity
                });
            }
            result.Add(combo);
        }
        
        return result;
    }

    public async Task<Guid> CreateProductAsync(CreateProductRequest req)
    {
        using var conn = _dbFactory.CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            var initialStock = req.InitialStock.HasValue && req.InitialStock.Value >= 0 ? req.InitialStock.Value : 100;
            var reorderThreshold = req.ReorderThreshold.HasValue && req.ReorderThreshold.Value >= 0 ? req.ReorderThreshold.Value : 20;

            var branchIds = new List<Guid>();
            if (req.BranchId.HasValue && req.BranchId.Value != Guid.Empty)
            {
                branchIds.Add(req.BranchId.Value);
            }
            else
            {
                var allBranches = (await conn.QueryAsync<Guid>("SELECT branch_id FROM catalog.branches WHERE is_active = true", transaction: tx)).ToList();
                branchIds.AddRange(allBranches);
                if (branchIds.Count == 0)
                {
                    var fallback = (await conn.QueryAsync<Guid>("SELECT branch_id FROM catalog.branches", transaction: tx)).ToList();
                    branchIds.AddRange(fallback);
                }
            }

            var skuBase = !string.IsNullOrWhiteSpace(req.Sku)
                ? req.Sku.Trim().ToUpperInvariant()
                : GenerateProductSku(req.Category, req.Name);

            Guid primaryId = Guid.Empty;

            const string insertProductSql = @"
                INSERT INTO pos.products (product_id, branch_id, sku, name, category, unit_price, image_url, is_active)
                VALUES (@Id, @BranchId, @Sku, @Name, @Category, @UnitPrice, @ImageUrl, true)
                ON CONFLICT (branch_id, sku) DO UPDATE 
                SET name = EXCLUDED.name, unit_price = EXCLUDED.unit_price, image_url = EXCLUDED.image_url, is_active = true
                RETURNING product_id;";

            const string insertInventorySql = @"
                INSERT INTO pos.branch_inventory (inventory_id, branch_id, product_id, stock_quantity, reorder_threshold, is_out_of_stock, last_restocked_at, updated_at)
                VALUES (gen_random_uuid(), @BranchId, @ProductId, @StockQuantity, @ReorderThreshold, false, now(), now())
                ON CONFLICT (branch_id, product_id) DO UPDATE
                SET stock_quantity = EXCLUDED.stock_quantity,
                    reorder_threshold = EXCLUDED.reorder_threshold,
                    updated_at = now();";

            foreach (var bId in branchIds)
            {
                var prodId = Guid.NewGuid();
                var insertedId = await conn.ExecuteScalarAsync<Guid>(insertProductSql, new
                {
                    Id = prodId,
                    BranchId = bId,
                    Sku = skuBase,
                    Name = req.Name.Trim(),
                    Category = req.Category.Trim(),
                    UnitPrice = req.UnitPrice,
                    ImageUrl = req.ImageUrl?.Trim()
                }, tx);

                if (primaryId == Guid.Empty) primaryId = insertedId;

                await conn.ExecuteAsync(insertInventorySql, new
                {
                    BranchId = bId,
                    ProductId = insertedId,
                    StockQuantity = initialStock,
                    ReorderThreshold = reorderThreshold
                }, tx);
            }

            tx.Commit();
            return primaryId;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private static string GenerateProductSku(string category, string name)
    {
        var catPrefix = !string.IsNullOrWhiteSpace(category) && category.Length >= 3
            ? category[..3].ToUpperInvariant()
            : "FNB";
        var namePrefix = !string.IsNullOrWhiteSpace(name)
            ? string.Concat(name.Where(char.IsLetterOrDigit)).ToUpperInvariant()
            : "ITEM";
        if (namePrefix.Length > 4) namePrefix = namePrefix[..4];
        var randSuffix = Random.Shared.Next(100, 999);
        return $"{catPrefix}-{namePrefix}-{randSuffix}";
    }

    public async Task<bool> UpdateProductAsync(Guid productId, UpdateProductRequest req)
    {
        using var conn = _dbFactory.CreateConnection();
        const string sql = @"
            UPDATE pos.products
            SET name = @Name, category = @Category, unit_price = @UnitPrice, image_url = @ImageUrl, is_active = @IsActive
            WHERE product_id = @Id";
        var rows = await conn.ExecuteAsync(sql, new { Id = productId, req.Name, req.Category, req.UnitPrice, ImageUrl = req.ImageUrl?.Trim(), req.IsActive });
        return rows > 0;
    }

    public async Task<bool> DeleteProductAsync(Guid productId)
    {
        using var conn = _dbFactory.CreateConnection();
        var orderCount = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM pos.order_lines WHERE product_id = @Id", new { Id = productId });

        if (orderCount > 0)
        {
            const string softSql = @"
                UPDATE pos.products SET is_active = false WHERE product_id = @Id;
                UPDATE pos.branch_inventory SET is_out_of_stock = true, stock_quantity = 0 WHERE product_id = @Id;";
            var affected = await conn.ExecuteAsync(softSql, new { Id = productId });
            return affected > 0;
        }

        const string sql = @"
            DELETE FROM pos.branch_inventory WHERE product_id = @Id;
            DELETE FROM pos.products WHERE product_id = @Id;";
        var rows = await conn.ExecuteAsync(sql, new { Id = productId });
        return rows > 0;
    }

    public async Task<Guid> CreateComboAsync(CreateComboRequest req)
    {
        using var conn = _dbFactory.CreateConnection();
        var id = Guid.NewGuid();
        const string sql = @"
            INSERT INTO catalog.combos (combo_id, name, price, is_active)
            VALUES (@Id, @Name, @Price, true)";
        await conn.ExecuteAsync(sql, new { Id = id, req.Name, req.Price });
        return id;
    }

    public async Task CreateTillShiftAsync(Guid shiftId, Guid branchId, Guid cashierId, string terminalCode, decimal openingFloat)
    {
        using var conn = _dbFactory.CreateConnection();
        const string sql = @"
            INSERT INTO pos.till_shifts 
                (shift_id, branch_id, cashier_id, terminal_code, opened_at, opening_float, status)
            VALUES 
                (@ShiftId, @BranchId, @CashierId, @TerminalCode, now(), @OpeningFloat, 'open')";

        await conn.ExecuteAsync(sql, new
        {
            ShiftId = shiftId,
            BranchId = branchId,
            CashierId = cashierId,
            TerminalCode = terminalCode,
            OpeningFloat = openingFloat
        });
    }

    public async Task<dynamic?> CloseTillShiftAsync(Guid shiftId, decimal closingCash)
    {
        using var conn = _dbFactory.CreateConnection();
        const string updateSql = @"
            UPDATE pos.till_shifts 
            SET status = 'closed', closed_at = now(), closing_cash = @ClosingCash 
            WHERE shift_id = @ShiftId
            RETURNING shift_id, opening_float, closing_cash, opened_at, closed_at";

        return await conn.QueryFirstOrDefaultAsync<dynamic>(updateSql, new
        {
            ShiftId = shiftId,
            ClosingCash = closingCash
        });
    }

    public async Task<dynamic?> GetOrderByIdempotencyKeyAsync(Guid idempotencyKey)
    {
        using var conn = _dbFactory.CreateReadConnection();
        const string checkSql = @"
            SELECT order_id, branch_id, cashier_id, reservation_id, status, subtotal, discount_amount, total_amount, idempotency_key, created_at
            FROM pos.orders
            WHERE idempotency_key = @IdempotencyKey";

        return await conn.QueryFirstOrDefaultAsync<dynamic>(checkSql, new { IdempotencyKey = idempotencyKey });
    }

    public async Task InsertOrderAsync(Guid orderId, Guid branchId, Guid? cashierId, Guid? reservationId, decimal subtotal, decimal discount, decimal total, Guid idempotencyKey, string? customerEmail, string? voucherCode, DbConnection conn, DbTransaction tx)
    {
        const string insertOrderSql = @"
            INSERT INTO pos.orders 
                (order_id, branch_id, cashier_id, reservation_id, status, subtotal, discount_amount, total_amount, idempotency_key, customer_email, voucher_code, created_at)
            VALUES 
                (@OrderId, @BranchId, @CashierId, @ReservationId, 'pending_payment'::order_status, @Subtotal, @Discount, @Total, @IdempotencyKey, @CustomerEmail, @VoucherCode, now())";

        await conn.ExecuteAsync(insertOrderSql, new
        {
            OrderId = orderId,
            BranchId = branchId,
            CashierId = cashierId,
            ReservationId = reservationId,
            Subtotal = subtotal,
            Discount = discount,
            Total = total,
            IdempotencyKey = idempotencyKey,
            CustomerEmail = customerEmail,
            VoucherCode = voucherCode
        }, tx);
    }

    public async Task InsertOrderLineAsync(Guid orderId, OrderLineRequest line, DbConnection conn, DbTransaction tx)
    {
        const string insertLineSql = @"
            INSERT INTO pos.order_lines 
                (order_line_id, order_id, product_id, description, quantity, unit_price, line_total)
            VALUES 
                (gen_random_uuid(), @OrderId, @ProductId, @Description, @Quantity, @UnitPrice, @LineTotal)";

        await conn.ExecuteAsync(insertLineSql, new
        {
            OrderId = orderId,
            ProductId = line.ProductId,
            Description = string.IsNullOrWhiteSpace(line.Description) ? "Concession Item" : line.Description,
            Quantity = line.Quantity,
            UnitPrice = line.UnitPrice,
            LineTotal = (line.Quantity * line.UnitPrice) - line.DiscountAmount
        }, tx);
    }

    public async Task<dynamic?> GetOrderByIdAsync(Guid orderId, DbConnection conn, DbTransaction? tx = null)
    {
        string checkOrderSql = "SELECT order_id, total_amount, status, customer_email FROM pos.orders WHERE order_id = @OrderId";
        if (tx != null) 
        {
            checkOrderSql += " FOR UPDATE";
        }
        return await conn.QueryFirstOrDefaultAsync<dynamic>(checkOrderSql, new { OrderId = orderId }, tx);
    }

    public async Task InsertPaymentAsync(Guid paymentId, Guid orderId, string method, decimal amount, string? providerRef, DbConnection conn, DbTransaction tx)
    {
        const string insertPaySql = @"
            INSERT INTO pos.payments 
                (payment_id, order_id, method, amount, provider_reference, status, paid_at)
            VALUES 
                (@PaymentId, @OrderId, @Method::payment_method, @Amount, @ProviderReference, 'captured', now())";

        await conn.ExecuteAsync(insertPaySql, new
        {
            PaymentId = paymentId,
            OrderId = orderId,
            Method = method,
            Amount = amount,
            ProviderReference = providerRef
        }, tx);
    }

    public async Task InsertOutboxMessageAsync(string routingKey, object payload, DbConnection conn, DbTransaction tx)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        var sql = "INSERT INTO messaging.outbox_messages (service_name, routing_key, payload) VALUES ('pos-api', @Key, @Payload::jsonb)";
        await Dapper.SqlMapper.ExecuteAsync(conn, sql, new { Key = routingKey, Payload = json }, tx);
    }

    public async Task<decimal> GetTotalCapturedPaymentsAsync(Guid orderId, DbConnection conn, DbTransaction tx)
    {
        const string sumSql = "SELECT COALESCE(SUM(amount), 0) FROM pos.payments WHERE order_id = @OrderId AND status = 'captured'";
        return await conn.ExecuteScalarAsync<decimal>(sumSql, new { OrderId = orderId }, tx);
    }

    public async Task UpdateOrderStatusToPaidAsync(Guid orderId, DbConnection conn, DbTransaction tx)
    {
        const string sql = "UPDATE pos.orders SET status = 'paid'::order_status, completed_at = now() WHERE order_id = @OrderId";
        await conn.ExecuteAsync(sql, new { OrderId = orderId }, tx);
    }

    public async Task UpdateOrderStatusToCancelledAsync(Guid orderId, DbConnection conn, DbTransaction tx)
    {
        // Cancelled orders release their items
        const string sql = "UPDATE pos.orders SET status = 'cancelled'::order_status, completed_at = now() WHERE order_id = @OrderId";
        await conn.ExecuteAsync(sql, new { OrderId = orderId }, tx);
    }

    public async Task EnsureWalkInOrderExistsAsync(Guid orderId, decimal amount, DbConnection conn, DbTransaction tx)
    {
        var order = await GetOrderByIdAsync(orderId, conn, tx);
        if (order == null)
        {
            const string insertOrderSql = @"
                INSERT INTO pos.orders (order_id, branch_id, status, subtotal, total_amount, idempotency_key, created_at)
                VALUES (@OrderId, @BranchId, 'pending_payment', @Amount, @Amount, gen_random_uuid(), now())";
                
            await conn.ExecuteAsync(insertOrderSql, new { OrderId = orderId, BranchId = Constants.DefaultWalkInBranchId, Amount = amount }, tx);
        }
    }

    public async Task<int> InsertGuestOrderAsync(Guid orderId, Guid branchId, Guid reservationId, decimal totalAmount, Guid idempotencyKey, string customerEmail, string? customerPhone, string bookingReference, Guid? customerId, DbConnection conn, DbTransaction tx)
    {
        const string sql = @"
            INSERT INTO pos.orders 
                (order_id, branch_id, cashier_id, reservation_id, status, subtotal, discount_amount, total_amount, idempotency_key, channel, customer_email, customer_phone, booking_reference, customer_id, created_at)
            VALUES 
                (@OrderId, @BranchId, NULL, @ReservationId, 'pending_payment'::order_status, @TotalAmount, 0, @TotalAmount, @IdempotencyKey, 'web', @CustomerEmail, @CustomerPhone, @BookingReference, @CustomerId, now())
            RETURNING booking_number";

        return await conn.ExecuteScalarAsync<int>(sql, new
        {
            OrderId = orderId,
            BranchId = branchId,
            ReservationId = reservationId,
            TotalAmount = totalAmount,
            IdempotencyKey = idempotencyKey,
            CustomerEmail = customerEmail,
            CustomerPhone = customerPhone,
            BookingReference = bookingReference,
            CustomerId = customerId
        }, tx);
    }

    public async Task<dynamic?> GetReservationForGuestCheckoutAsync(Guid reservationId, DbConnection conn, DbTransaction? tx = null)
    {
        const string sql = @"
            SELECT r.reservation_id, r.showtime_id, r.status, r.hold_expires_at, r.fencing_token,
                   r.guest_email, r.guest_phone, r.guest_name,
                   m.title as movie_title, b.branch_id, b.name as branch_name, a.name as auditorium_name,
                   st.starts_at, st.base_price
            FROM reservations.reservations r
            JOIN catalog.showtimes st ON r.showtime_id = st.showtime_id
            JOIN catalog.movies m ON st.movie_id = m.movie_id
            JOIN catalog.auditoriums a ON st.auditorium_id = a.auditorium_id
            JOIN catalog.branches b ON a.branch_id = b.branch_id
            WHERE r.reservation_id = @ReservationId";

        return await conn.QueryFirstOrDefaultAsync<dynamic>(sql, new { ReservationId = reservationId }, tx);
    }

    public async Task<List<dynamic>> GetReservationSeatsAsync(Guid reservationId, DbConnection conn, DbTransaction? tx = null)
    {
        const string sql = @"
            SELECT rs.seat_id, s.row_label, s.seat_number, s.seat_type, rs.price
            FROM reservations.reservation_seats rs
            JOIN catalog.seats s ON rs.seat_id = s.seat_id
            WHERE rs.reservation_id = @ReservationId";

        var seats = await conn.QueryAsync<dynamic>(sql, new { ReservationId = reservationId }, tx);
        return seats.ToList();
    }

    public async Task ConfirmReservationAndSeatsAsync(Guid reservationId, Guid showtimeId, IEnumerable<Guid> seatIds, string? guestEmail, string? guestPhone, string? guestName, DbConnection conn, DbTransaction tx)
    {
        const string checkSql = @"
            SELECT COUNT(1) 
            FROM reservations.confirmed_seats 
            WHERE showtime_id = @ShowtimeId 
              AND seat_id = ANY(@SeatIds) 
              AND reservation_id <> @ReservationId";

        var conflictCount = await conn.ExecuteScalarAsync<int>(checkSql, new
        {
            ShowtimeId = showtimeId,
            SeatIds = seatIds.ToArray(),
            ReservationId = reservationId
        }, tx);

        if (conflictCount > 0)
        {
            throw new InvalidOperationException("One or more seats have already been booked and confirmed by another transaction.");
        }

        const string updateResSql = @"
            UPDATE reservations.reservations 
            SET status = 'confirmed'::reservation_status, 
                confirmed_at = now(),
                guest_email = COALESCE(@GuestEmail, guest_email),
                guest_phone = COALESCE(@GuestPhone, guest_phone),
                guest_name = COALESCE(@GuestName, guest_name)
            WHERE reservation_id = @ReservationId";
        await conn.ExecuteAsync(updateResSql, new 
        { 
            ReservationId = reservationId,
            GuestEmail = string.IsNullOrWhiteSpace(guestEmail) ? null : guestEmail,
            GuestPhone = string.IsNullOrWhiteSpace(guestPhone) ? null : guestPhone,
            GuestName = string.IsNullOrWhiteSpace(guestName) ? null : guestName
        }, tx);

        const string confirmSeatsSql = @"
            INSERT INTO reservations.confirmed_seats (showtime_id, seat_id, reservation_id, booked_at)
            VALUES (@ShowtimeId, @SeatId, @ReservationId, now())
            ON CONFLICT (showtime_id, seat_id) DO NOTHING";

        foreach (var seatId in seatIds)
        {
            await conn.ExecuteAsync(confirmSeatsSql, new { ShowtimeId = showtimeId, SeatId = seatId, ReservationId = reservationId }, tx);
        }
    }

    public async Task<List<dynamic>> CreateTicketsAndReturnDetailsAsync(Guid reservationId, Guid showtimeId, IEnumerable<Guid> seatIds, string guestEmail, DbConnection conn, DbTransaction tx)
    {
        var list = new List<dynamic>();
        const string ticketSql = @"
            INSERT INTO tickets.tickets 
                (ticket_id, reservation_id, showtime_id, seat_id, qr_token_hash, status, is_printed, delivery_channel, email_recipient, email_sent, created_at)
            VALUES 
                (@TicketId, @ReservationId, @ShowtimeId, @SeatId, @QrTokenHash, 'active'::ticket_status, false, 'email', @Email, false, now())
            ON CONFLICT (reservation_id, seat_id) DO UPDATE 
                SET email_recipient = EXCLUDED.email_recipient
            RETURNING ticket_id, reservation_id, showtime_id, seat_id, qr_token_hash, status";

        foreach (var seatId in seatIds)
        {
            var ticketId = Guid.NewGuid();
            var rawQrData = $"CINEMA-TICKET:{ticketId}:{reservationId}:{seatId}";
            var qrHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawQrData)));

            var row = await conn.QuerySingleAsync<dynamic>(ticketSql, new
            {
                TicketId = ticketId,
                ReservationId = reservationId,
                ShowtimeId = showtimeId,
                SeatId = seatId,
                QrTokenHash = qrHash,
                Email = guestEmail
            }, tx);

            var persistedTicketId = (Guid)row.ticket_id;
            var persistedHash = (string)row.qr_token_hash;
            var effectiveRawQr = persistedTicketId == ticketId
                ? rawQrData
                : $"CINEMA-TICKET:{persistedTicketId}:{reservationId}:{seatId}";

            list.Add(new
            {
                ticket_id = persistedTicketId,
                seat_id = seatId,
                qr_token = effectiveRawQr,
                qr_hash = persistedHash
            });
        }

        return list;
    }

    public async Task<List<dynamic>> GetOrderLinesByOrderIdAsync(Guid orderId)
    {
        using var conn = _dbFactory.CreateConnection();
        const string sql = @"
            SELECT order_line_id, order_id, product_id, description, quantity, unit_price, line_total, fulfillment_status, fulfilled_at
            FROM pos.order_lines
            WHERE order_id = @OrderId
            ORDER BY order_line_id";
        var lines = await conn.QueryAsync<dynamic>(sql, new { OrderId = orderId });
        return lines.ToList();
    }

    public async Task<List<dynamic>> FulfillOrderConcessionsAsync(Guid orderId, Guid? staffId = null)
    {
        using var conn = _dbFactory.CreateConnection();
        const string sql = @"
            UPDATE pos.order_lines
            SET fulfillment_status = 'collected',
                fulfilled_at = now(),
                fulfillment_staff_id = @StaffId
            WHERE order_id = @OrderId AND fulfillment_status != 'cancelled'
            RETURNING order_line_id, description, quantity, fulfillment_status, fulfilled_at";
        var updated = await conn.QueryAsync<dynamic>(sql, new { OrderId = orderId, StaffId = staffId });
        return updated.ToList();
    }

    public async Task<Guid?> FindOrderIdByReferenceOrTokenAsync(string tokenOrRef)
    {
        if (string.IsNullOrWhiteSpace(tokenOrRef)) return null;

        var clean = tokenOrRef.Trim();
        if (clean.StartsWith("CINEMA-ORDER:", StringComparison.OrdinalIgnoreCase))
        {
            var parts = clean.Split(':');
            if (parts.Length >= 2 && Guid.TryParse(parts[1], out var parsedId))
            {
                return parsedId;
            }
        }

        if (clean.StartsWith("CINEMA-FNB:", StringComparison.OrdinalIgnoreCase))
        {
            var parts = clean.Split(':');
            if (parts.Length >= 2 && Guid.TryParse(parts[1], out var parsedId))
            {
                return parsedId;
            }
        }

        if (Guid.TryParse(clean, out var guidId))
        {
            return guidId;
        }

        var cleanRef = clean.Replace("#ORD-", "").Replace("#", "").Trim();
        using var conn = _dbFactory.CreateConnection();
        const string sql = @"
            SELECT order_id 
            FROM pos.orders 
            WHERE booking_reference = @RawRef
               OR booking_reference = @PrefixedRef
               OR REPLACE(REPLACE(booking_reference, '#ORD-', ''), '#', '') = @CleanRef
               OR booking_number::text = @CleanRef
               OR reservation_id::text = @CleanRef
            LIMIT 1";

        return await conn.QueryFirstOrDefaultAsync<Guid?>(sql, new 
        { 
            RawRef = clean, 
            PrefixedRef = $"#ORD-{cleanRef}", 
            CleanRef = cleanRef 
        });
    }

    public async Task<PagedResult<ProductDto>> SearchProductsAsync(ProductSearchRequest request)
    {
        using var conn = _dbFactory.CreateReadConnection();
        var dynamicParams = new DynamicParameters();
        var conditions = new List<string> { "p.is_active = true", "p.stock_quantity > 0" };

        if (request.BranchId.HasValue && request.BranchId.Value != Guid.Empty)
        {
            dynamicParams.Add("BranchId", request.BranchId.Value);
            conditions.Add("p.branch_id = @BranchId");
        }

        if (!string.IsNullOrWhiteSpace(request.Q))
        {
            dynamicParams.Add("QPattern", $"%{request.Q.Trim()}%");
            conditions.Add("(p.name ILIKE @QPattern OR coalesce(p.description, '') ILIKE @QPattern)");
        }

        if (request.Categories != null && request.Categories.Length > 0)
        {
            dynamicParams.Add("Categories", request.Categories.Select(c => c.Trim()).ToArray());
            conditions.Add("p.category = ANY(@Categories)");
        }

        if (request.Dietary != null && request.Dietary.Length > 0)
        {
            dynamicParams.Add("DietaryArray", request.Dietary.Select(d => d.Trim().ToLowerInvariant()).ToArray());
            conditions.Add("p.dietary_tags && @DietaryArray");
        }

        if (request.PriceMin.HasValue)
        {
            dynamicParams.Add("PriceMin", request.PriceMin.Value);
            conditions.Add("p.unit_price >= @PriceMin");
        }

        if (request.PriceMax.HasValue)
        {
            dynamicParams.Add("PriceMax", request.PriceMax.Value);
            conditions.Add("p.unit_price <= @PriceMax");
        }

        if (request.PromoOnly == true)
        {
            conditions.Add("(p.badge_text IS NOT NULL AND p.badge_text <> '')");
        }

        int tierLevel = GetTierLevel(request.LoyaltyTier);
        dynamicParams.Add("TierLevel", tierLevel);
        conditions.Add(@"
            (CASE p.min_loyalty_tier 
                WHEN 'platinum' THEN 4 
                WHEN 'gold' THEN 3 
                WHEN 'silver' THEN 2 
                WHEN 'bronze' THEN 1 
                ELSE 0 
             END) <= @TierLevel");

        var whereClause = string.Join(" AND ", conditions);

        var countSql = $"SELECT COUNT(*) FROM pos.products p WHERE {whereClause}";
        var totalCount = await conn.ExecuteScalarAsync<int>(countSql, dynamicParams);

        var page = Math.Max(1, request.Page ?? 1);
        var pageSize = Math.Clamp(request.PageSize ?? 20, 1, 50);
        var offset = (page - 1) * pageSize;

        dynamicParams.Add("Limit", pageSize);
        dynamicParams.Add("Offset", offset);

        var sql = $@"
            SELECT product_id as ProductId, sku as Sku, name as Name,
                   category as Category, unit_price as UnitPrice,
                   image_url as ImageUrl, badge_text as BadgeText, description as Description,
                   dietary_tags as DietaryTags, is_combo_only as IsComboOnly,
                   min_loyalty_tier as MinLoyaltyTier, stock_quantity as StockQuantity,
                   is_active as IsActive
            FROM pos.products p
            WHERE {whereClause}
            ORDER BY 
                CASE WHEN p.badge_text IS NOT NULL THEN 0 ELSE 1 END,
                p.category, p.name
            LIMIT @Limit OFFSET @Offset";

        var items = (await conn.QueryAsync<ProductDto>(sql, dynamicParams)).ToList();

        return new PagedResult<ProductDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<ProductDto>> GetUpsellProductsAsync(Guid? branchId, IEnumerable<Guid> cartProductIds)
    {
        using var conn = _dbFactory.CreateReadConnection();
        var excludeIds = cartProductIds.Distinct().ToArray();

        const string sql = @"
            SELECT product_id as ProductId, sku as Sku, name as Name,
                   category as Category, unit_price as UnitPrice,
                   image_url as ImageUrl, badge_text as BadgeText, description as Description,
                   dietary_tags as DietaryTags, is_combo_only as IsComboOnly,
                   min_loyalty_tier as MinLoyaltyTier, stock_quantity as StockQuantity,
                   is_active as IsActive
            FROM pos.products p
            WHERE (@BranchId IS NULL OR p.branch_id = @BranchId)
              AND p.is_active = true
              AND p.stock_quantity > 0
              AND (@ExcludeCount = 0 OR p.product_id <> ALL(@ExcludeIds))
            ORDER BY 
                CASE WHEN p.badge_text IS NOT NULL THEN 0 ELSE 1 END,
                p.unit_price DESC
            LIMIT 5";

        return await conn.QueryAsync<ProductDto>(sql, new 
        { 
            BranchId = branchId, 
            ExcludeIds = excludeIds, 
            ExcludeCount = excludeIds.Length 
        });
    }

    private static int GetTierLevel(string? tier)
    {
        return (tier?.ToLowerInvariant()) switch
        {
            "platinum" => 4,
            "gold" => 3,
            "silver" => 2,
            "bronze" => 1,
            _ => 0
        };
    }

    // =========================================================================
    // Milestone 5.3: Multi-Branch Stock Tracking & Suppliers
    // =========================================================================

    public async Task<IEnumerable<BranchInventoryDto>> GetBranchInventoryAsync(Guid branchId)
    {
        using var conn = _dbFactory.CreateReadConnection();
        const string sql = @"
            SELECT bi.inventory_id as InventoryId,
                   bi.branch_id as BranchId,
                   bi.product_id as ProductId,
                   p.name as ProductName,
                   p.sku as Sku,
                   p.category as Category,
                   p.unit_price as UnitPrice,
                   p.image_url as ImageUrl,
                   bi.stock_quantity as StockQuantity,
                   bi.reorder_threshold as ReorderThreshold,
                   bi.is_out_of_stock as IsOutOfStock,
                   bi.last_restocked_at as LastRestockedAt,
                   bi.updated_at as UpdatedAt
            FROM pos.branch_inventory bi
            JOIN pos.products p ON bi.product_id = p.product_id
            WHERE bi.branch_id = @BranchId
            ORDER BY p.category, p.name";
        return await conn.QueryAsync<BranchInventoryDto>(sql, new { BranchId = branchId });
    }

    public async Task<BranchInventoryDto?> GetProductBranchInventoryAsync(Guid branchId, Guid productId)
    {
        using var conn = _dbFactory.CreateReadConnection();
        const string sql = @"
            SELECT bi.inventory_id as InventoryId,
                   bi.branch_id as BranchId,
                   bi.product_id as ProductId,
                   p.name as ProductName,
                   p.sku as Sku,
                   p.category as Category,
                   p.unit_price as UnitPrice,
                   p.image_url as ImageUrl,
                   bi.stock_quantity as StockQuantity,
                   bi.reorder_threshold as ReorderThreshold,
                   bi.is_out_of_stock as IsOutOfStock,
                   bi.last_restocked_at as LastRestockedAt,
                   bi.updated_at as UpdatedAt
            FROM pos.branch_inventory bi
            JOIN pos.products p ON bi.product_id = p.product_id
            WHERE bi.branch_id = @BranchId AND bi.product_id = @ProductId;";
        return await conn.QuerySingleOrDefaultAsync<BranchInventoryDto>(sql, new { BranchId = branchId, ProductId = productId });
    }

    public async Task<bool> AdjustStockAsync(Guid branchId, Guid productId, int quantityDelta, string reason, Guid staffId)
    {
        using var conn = _dbFactory.CreateConnection();
        const string sql = @"
            INSERT INTO pos.branch_inventory (branch_id, product_id, stock_quantity, reorder_threshold, is_out_of_stock, last_restocked_at, updated_at)
            VALUES (@BranchId, @ProductId, GREATEST(0, @QuantityDelta), 20, (GREATEST(0, @QuantityDelta) = 0), CASE WHEN @QuantityDelta > 0 THEN now() ELSE NULL END, now())
            ON CONFLICT (branch_id, product_id) DO UPDATE
            SET stock_quantity = GREATEST(0, pos.branch_inventory.stock_quantity + @QuantityDelta),
                is_out_of_stock = (GREATEST(0, pos.branch_inventory.stock_quantity + @QuantityDelta) = 0),
                last_restocked_at = CASE WHEN @QuantityDelta > 0 THEN now() ELSE pos.branch_inventory.last_restocked_at END,
                updated_at = now();";
        var affected = await conn.ExecuteAsync(sql, new { BranchId = branchId, ProductId = productId, QuantityDelta = quantityDelta });
        return affected > 0;
    }

    public async Task<bool> ToggleProductAvailabilityAsync(Guid branchId, Guid productId, bool isOutOfStock)
    {
        using var conn = _dbFactory.CreateConnection();
        const string sql = @"
            UPDATE pos.branch_inventory
            SET is_out_of_stock = @IsOutOfStock,
                updated_at = now()
            WHERE branch_id = @BranchId AND product_id = @ProductId;";
        var affected = await conn.ExecuteAsync(sql, new { BranchId = branchId, ProductId = productId, IsOutOfStock = isOutOfStock });
        return affected > 0;
    }

    public async Task<Guid> LogWastageAsync(Guid branchId, LogWastageRequest req, Guid staffId)
    {
        using var conn = _dbFactory.CreateConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            if (conn is DbConnection dbConn) await dbConn.OpenAsync();
            else conn.Open();
        }
        using var tx = conn.BeginTransaction();
        try
        {
            // 1. Deduct stock from branch inventory
            const string deductSql = @"
                UPDATE pos.branch_inventory
                SET stock_quantity = GREATEST(0, stock_quantity - @Quantity),
                    is_out_of_stock = (GREATEST(0, stock_quantity - @Quantity) = 0),
                    updated_at = now()
                WHERE branch_id = @BranchId AND product_id = @ProductId;";
            await conn.ExecuteAsync(deductSql, new { BranchId = branchId, req.ProductId, req.Quantity }, tx);

            // 2. Insert into pos.inventory_wastage
            var totalLoss = req.Quantity * req.UnitCost;
            const string wastageSql = @"
                INSERT INTO pos.inventory_wastage (
                    branch_id, product_id, quantity, reason, cost_loss, logged_by, logged_at, notes
                ) VALUES (
                    @BranchId, @ProductId, @Quantity, @Reason, @CostLoss, @LoggedBy, now(), @Notes
                ) RETURNING wastage_id;";

            var wastageId = await conn.ExecuteScalarAsync<Guid>(wastageSql, new
            {
                BranchId = branchId,
                req.ProductId,
                req.Quantity,
                req.Reason,
                CostLoss = totalLoss,
                LoggedBy = staffId,
                req.Notes
            }, tx);

            tx.Commit();
            return wastageId;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<IEnumerable<InventoryWastageDto>> GetInventoryWastageAsync(Guid branchId, DateTimeOffset? fromDate, DateTimeOffset? toDate)
    {
        using var conn = _dbFactory.CreateReadConnection();
        const string sql = @"
            SELECT iw.wastage_id as WastageId,
                   iw.branch_id as BranchId,
                   iw.product_id as ProductId,
                   p.name as ProductName,
                   p.sku as Sku,
                   iw.quantity as Quantity,
                   iw.reason as Reason,
                   iw.cost_loss as CostLoss,
                   iw.logged_by as LoggedBy,
                   iw.logged_at as LoggedAt,
                   iw.notes as Notes
            FROM pos.inventory_wastage iw
            JOIN pos.products p ON iw.product_id = p.product_id
            WHERE iw.branch_id = @BranchId
              AND (@FromDate IS NULL OR iw.logged_at >= @FromDate)
              AND (@ToDate IS NULL OR iw.logged_at <= @ToDate)
            ORDER BY iw.logged_at DESC";
        return await conn.QueryAsync<InventoryWastageDto>(sql, new { BranchId = branchId, FromDate = fromDate, ToDate = toDate });
    }

    public async Task<IEnumerable<BranchInventoryDto>> GetReorderAlertsAsync(Guid? branchId)
    {
        using var conn = _dbFactory.CreateReadConnection();
        const string sql = @"
            SELECT bi.inventory_id as InventoryId,
                   bi.branch_id as BranchId,
                   bi.product_id as ProductId,
                   p.name as ProductName,
                   p.sku as Sku,
                   p.category as Category,
                   p.unit_price as UnitPrice,
                   p.image_url as ImageUrl,
                   bi.stock_quantity as StockQuantity,
                   bi.reorder_threshold as ReorderThreshold,
                   bi.is_out_of_stock as IsOutOfStock,
                   bi.last_restocked_at as LastRestockedAt,
                   bi.updated_at as UpdatedAt
            FROM pos.branch_inventory bi
            JOIN pos.products p ON bi.product_id = p.product_id
            WHERE (@BranchId IS NULL OR bi.branch_id = @BranchId)
              AND (bi.stock_quantity <= bi.reorder_threshold OR bi.is_out_of_stock = true)
            ORDER BY bi.stock_quantity ASC, p.name";
        return await conn.QueryAsync<BranchInventoryDto>(sql, new { BranchId = branchId });
    }

    public async Task<IEnumerable<SupplierDto>> GetSuppliersAsync()
    {
        using var conn = _dbFactory.CreateReadConnection();
        const string sql = @"
            SELECT supplier_id as SupplierId,
                   name as Name,
                   contact_name as ContactName,
                   email as Email,
                   phone as Phone,
                   address as Address,
                   is_active as IsActive,
                   created_at as CreatedAt
            FROM pos.suppliers
            ORDER BY name";
        return await conn.QueryAsync<SupplierDto>(sql);
    }

    public async Task<SupplierDto?> GetSupplierByIdAsync(Guid supplierId)
    {
        using var conn = _dbFactory.CreateReadConnection();
        const string sql = @"
            SELECT supplier_id as SupplierId,
                   name as Name,
                   contact_name as ContactName,
                   email as Email,
                   phone as Phone,
                   address as Address,
                   is_active as IsActive,
                   created_at as CreatedAt
            FROM pos.suppliers
            WHERE supplier_id = @SupplierId;";
        return await conn.QuerySingleOrDefaultAsync<SupplierDto>(sql, new { SupplierId = supplierId });
    }

    public async Task<Guid> CreateSupplierAsync(CreateSupplierRequest req)
    {
        using var conn = _dbFactory.CreateConnection();
        const string sql = @"
            INSERT INTO pos.suppliers (name, contact_name, email, phone, address, is_active)
            VALUES (@Name, @ContactName, @Email, @Phone, @Address, true)
            RETURNING supplier_id;";
        return await conn.ExecuteScalarAsync<Guid>(sql, new
        {
            req.Name,
            req.ContactName,
            req.Email,
            req.Phone,
            req.Address
        });
    }

    public async Task<IEnumerable<PurchaseOrderDto>> GetPurchaseOrdersAsync(Guid? branchId, string? status)
    {
        using var conn = _dbFactory.CreateReadConnection();
        const string sql = @"
            SELECT po.po_id as PoId,
                   po.po_number as PoNumber,
                   po.supplier_id as SupplierId,
                   s.name as SupplierName,
                   po.branch_id as BranchId,
                   po.status as Status,
                   po.total_cost as TotalCost,
                   po.ordered_at as OrderedAt,
                   po.received_at as ReceivedAt,
                   po.notes as Notes,
                   po.created_by as CreatedBy,
                   po.created_at as CreatedAt
            FROM pos.purchase_orders po
            LEFT JOIN pos.suppliers s ON po.supplier_id = s.supplier_id
            WHERE (@BranchId IS NULL OR po.branch_id = @BranchId)
              AND (@Status IS NULL OR po.status = @Status)
            ORDER BY po.created_at DESC";
        var pos = (await conn.QueryAsync<PurchaseOrderDto>(sql, new { BranchId = branchId, Status = status })).ToList();

        if (pos.Count > 0)
        {
            var poIds = pos.Select(p => p.PoId).ToArray();
            const string linesSql = @"
                SELECT pol.po_line_id as PoLineId,
                       pol.po_id as PoId,
                       pol.product_id as ProductId,
                       p.name as ProductName,
                       p.sku as Sku,
                       pol.quantity as Quantity,
                       pol.unit_cost as UnitCost,
                       pol.line_total as LineTotal
                FROM pos.purchase_order_lines pol
                JOIN pos.products p ON pol.product_id = p.product_id
                WHERE pol.po_id = ANY(@PoIds)
                ORDER BY pol.po_id, p.name";

            var lines = await conn.QueryAsync<dynamic>(linesSql, new { PoIds = poIds });
            var lineLookup = lines.GroupBy(l => (Guid)l.poid);
            foreach (var po in pos)
            {
                if (lineLookup.Any(g => g.Key == po.PoId))
                {
                    po.Lines = lineLookup.First(g => g.Key == po.PoId).Select(l => new PurchaseOrderLineDto
                    {
                        PoLineId = (Guid)l.polineid,
                        ProductId = (Guid)l.productid,
                        ProductName = (string)l.productname,
                        Sku = (string)l.sku,
                        Quantity = (int)l.quantity,
                        UnitCost = (decimal)l.unitcost,
                        LineTotal = (decimal)l.linetotal
                    }).ToList();
                }
            }
        }
        return pos;
    }

    public async Task<PurchaseOrderDto?> GetPurchaseOrderByIdAsync(Guid poId)
    {
        using var conn = _dbFactory.CreateReadConnection();
        const string sql = @"
            SELECT po.po_id as PoId,
                   po.po_number as PoNumber,
                   po.supplier_id as SupplierId,
                   s.name as SupplierName,
                   po.branch_id as BranchId,
                   po.status as Status,
                   po.total_cost as TotalCost,
                   po.ordered_at as OrderedAt,
                   po.received_at as ReceivedAt,
                   po.notes as Notes,
                   po.created_by as CreatedBy,
                   po.created_at as CreatedAt
            FROM pos.purchase_orders po
            LEFT JOIN pos.suppliers s ON po.supplier_id = s.supplier_id
            WHERE po.po_id = @PoId;";
        var po = await conn.QuerySingleOrDefaultAsync<PurchaseOrderDto>(sql, new { PoId = poId });
        if (po == null) return null;

        const string linesSql = @"
            SELECT pol.po_line_id as PoLineId,
                   pol.product_id as ProductId,
                   p.name as ProductName,
                   p.sku as Sku,
                   pol.quantity as Quantity,
                   pol.unit_cost as UnitCost,
                   pol.line_total as LineTotal
            FROM pos.purchase_order_lines pol
            JOIN pos.products p ON pol.product_id = p.product_id
            WHERE pol.po_id = @PoId
            ORDER BY p.name";
        var lines = await conn.QueryAsync<PurchaseOrderLineDto>(linesSql, new { PoId = poId });
        po.Lines = lines.ToList();
        return po;
    }

    public async Task<Guid> CreatePurchaseOrderAsync(CreatePurchaseOrderRequest req, Guid createdBy)
    {
        using var conn = _dbFactory.CreateConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            if (conn is DbConnection dbConn) await dbConn.OpenAsync();
            else conn.Open();
        }
        using var tx = conn.BeginTransaction();
        try
        {
            decimal totalCost = 0;
            if (req.Lines != null)
            {
                totalCost = req.Lines.Sum(l => l.Quantity * l.UnitCost);
            }

            const string poSql = @"
                INSERT INTO pos.purchase_orders (
                    po_number, supplier_id, branch_id, status, total_cost, notes, created_by, created_at
                ) VALUES (
                    @PoNumber, @SupplierId, @BranchId, 'draft', @TotalCost, @Notes, @CreatedBy, now()
                ) RETURNING po_id;";

            var poId = await conn.ExecuteScalarAsync<Guid>(poSql, new
            {
                req.PoNumber,
                req.SupplierId,
                req.BranchId,
                TotalCost = totalCost,
                req.Notes,
                CreatedBy = createdBy
            }, tx);

            if (req.Lines != null && req.Lines.Count > 0)
            {
                const string lineSql = @"
                    INSERT INTO pos.purchase_order_lines (
                        po_id, product_id, quantity, unit_cost, line_total
                    ) VALUES (
                        @PoId, @ProductId, @Quantity, @UnitCost, @LineTotal
                    );";

                foreach (var line in req.Lines)
                {
                    await conn.ExecuteAsync(lineSql, new
                    {
                        PoId = poId,
                        line.ProductId,
                        line.Quantity,
                        line.UnitCost,
                        LineTotal = line.Quantity * line.UnitCost
                    }, tx);
                }
            }

            tx.Commit();
            return poId;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<bool> UpdatePurchaseOrderStatusAsync(Guid poId, string status, Guid staffId)
    {
        using var conn = _dbFactory.CreateConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            if (conn is DbConnection dbConn) await dbConn.OpenAsync();
            else conn.Open();
        }
        using var tx = conn.BeginTransaction();
        try
        {
            const string getPoSql = "SELECT po_id, branch_id, status FROM pos.purchase_orders WHERE po_id = @PoId;";
            var po = await conn.QuerySingleOrDefaultAsync<dynamic>(getPoSql, new { PoId = poId }, tx);
            if (po == null)
            {
                tx.Rollback();
                return false;
            }

            string normalizedStatus = status.ToLowerInvariant();
            DateTimeOffset? orderedAt = normalizedStatus == "ordered" ? DateTimeOffset.UtcNow : null;
            DateTimeOffset? receivedAt = normalizedStatus == "received" ? DateTimeOffset.UtcNow : null;

            const string updateSql = @"
                UPDATE pos.purchase_orders
                SET status = @Status,
                    ordered_at = COALESCE(@OrderedAt, ordered_at),
                    received_at = COALESCE(@ReceivedAt, received_at)
                WHERE po_id = @PoId;";

            var affected = await conn.ExecuteAsync(updateSql, new
            {
                PoId = poId,
                Status = normalizedStatus,
                OrderedAt = orderedAt,
                ReceivedAt = receivedAt
            }, tx);

            // If status is "received", automatically add quantities to pos.branch_inventory!
            if (normalizedStatus == "received" && (string)po.status != "received")
            {
                Guid branchId = (Guid)po.branch_id;
                const string linesSql = "SELECT product_id, quantity FROM pos.purchase_order_lines WHERE po_id = @PoId;";
                var lines = await conn.QueryAsync<dynamic>(linesSql, new { PoId = poId }, tx);

                const string restockSql = @"
                    INSERT INTO pos.branch_inventory (branch_id, product_id, stock_quantity, reorder_threshold, is_out_of_stock, last_restocked_at, updated_at)
                    VALUES (@BranchId, @ProductId, @Quantity, 20, false, now(), now())
                    ON CONFLICT (branch_id, product_id) DO UPDATE
                    SET stock_quantity = pos.branch_inventory.stock_quantity + @Quantity,
                        is_out_of_stock = false,
                        last_restocked_at = now(),
                        updated_at = now();";

                foreach (var line in lines)
                {
                    await conn.ExecuteAsync(restockSql, new
                    {
                        BranchId = branchId,
                        ProductId = (Guid)line.product_id,
                        Quantity = (int)line.quantity
                    }, tx);
                }
            }

            tx.Commit();
            return affected > 0;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    // =========================================================================
    // Milestone 5.4: Refunds & Disputes
    // =========================================================================

    public async Task<Guid> RecordRefundAsync(Guid? orderId, Guid? reservationId, decimal refundAmount, string reasonCode, string? notes, Guid authorizedBy)
    {
        using var conn = _dbFactory.CreateConnection();
        var refundId = Guid.NewGuid();
        const string sql = @"
            INSERT INTO pos.refunds (refund_id, order_id, reservation_id, refund_amount, reason_code, notes, authorized_by, refunded_at)
            VALUES (@RefundId, @OrderId, @ReservationId, @RefundAmount, @ReasonCode, @Notes, @AuthorizedBy, now())";

        await conn.ExecuteAsync(sql, new 
        { 
            RefundId = refundId, 
            OrderId = orderId, 
            ReservationId = reservationId, 
            RefundAmount = refundAmount, 
            ReasonCode = reasonCode, 
            Notes = notes, 
            AuthorizedBy = authorizedBy 
        });

        return refundId;
    }

    public async Task<IEnumerable<dynamic>> GetRefundsByOrderAsync(Guid orderId)
    {
        using var conn = _dbFactory.CreateReadConnection();
        return await conn.QueryAsync(
            "SELECT * FROM pos.refunds WHERE order_id = @OrderId ORDER BY refunded_at DESC", 
            new { OrderId = orderId });
    }

    public async Task<Guid> RecordDisputeAsync(Guid? orderId, Guid? reservationId, string? providerDisputeId, string status, decimal amount, string? evidenceNotes)
    {
        using var conn = _dbFactory.CreateConnection();
        var disputeId = Guid.NewGuid();
        const string sql = @"
            INSERT INTO pos.disputes (dispute_id, order_id, reservation_id, provider_dispute_id, status, amount, evidence_notes, created_at)
            VALUES (@DisputeId, @OrderId, @ReservationId, @ProviderDisputeId, @Status, @Amount, @EvidenceNotes, now())";

        await conn.ExecuteAsync(sql, new 
        { 
            DisputeId = disputeId, 
            OrderId = orderId, 
            ReservationId = reservationId, 
            ProviderDisputeId = providerDisputeId, 
            Status = status, 
            Amount = amount, 
            EvidenceNotes = evidenceNotes 
        });

        return disputeId;
    }

    public async Task<IEnumerable<dynamic>> GetDisputesByOrderAsync(Guid orderId)
    {
        using var conn = _dbFactory.CreateReadConnection();
        return await conn.QueryAsync(
            "SELECT * FROM pos.disputes WHERE order_id = @OrderId ORDER BY created_at DESC", 
            new { OrderId = orderId });
    }
}




