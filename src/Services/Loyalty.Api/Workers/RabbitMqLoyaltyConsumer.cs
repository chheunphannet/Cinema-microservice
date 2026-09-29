using System.Text;
using System.Text.Json;
using Cinema.Foundation.Data;
using Dapper;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Loyalty.Api.Workers;
using Loyalty.Api.Repositories;

public class RabbitMqLoyaltyConsumer : BackgroundService
{
    private readonly IConnectionFactory _connectionFactory;
    private readonly ILogger<RabbitMqLoyaltyConsumer> _logger;
    private readonly IDbConnectionFactory _dbFactory;
    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitMqLoyaltyConsumer(ILogger<RabbitMqLoyaltyConsumer> logger, IDbConnectionFactory dbFactory)
    {
        _logger = logger;
        _dbFactory = dbFactory;
        
        var hostName = Environment.GetEnvironmentVariable("RabbitMQ__Host") ?? "rabbitmq";
        var userName = Environment.GetEnvironmentVariable("RabbitMQ__Username") ?? "guest";
        var password = Environment.GetEnvironmentVariable("RabbitMQ__Password") ?? "guest";

        _connectionFactory = new ConnectionFactory
        {
            HostName = hostName,
            UserName = userName,
            Password = password
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _connection = await _connectionFactory.CreateConnectionAsync(stoppingToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

            await _channel.ExchangeDeclareAsync(exchange: "cinema.events", type: ExchangeType.Topic, durable: true, cancellationToken: stoppingToken);
            await _channel.QueueDeclareAsync(queue: "identity.loyalty.queue", durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
            await _channel.QueueBindAsync(queue: "identity.loyalty.queue", exchange: "cinema.events", routingKey: "order.completed", cancellationToken: stoppingToken);
            await _channel.QueueBindAsync(queue: "identity.loyalty.queue", exchange: "cinema.events", routingKey: "order.cancelled", cancellationToken: stoppingToken);
            await _channel.QueueBindAsync(queue: "identity.loyalty.queue", exchange: "cinema.events", routingKey: "customer.registered", cancellationToken: stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                var routingKey = ea.RoutingKey;

                try
                {
                    if (routingKey == "order.completed")
                    {
                        await ProcessOrderCompletedEventAsync(message);
                    }
                    else if (routingKey == "order.cancelled")
                    {
                        await ProcessOrderCancelledEventAsync(message);
                    }
                    else if (routingKey == "customer.registered")
                    {
                        await ProcessCustomerRegisteredEventAsync(message);
                    }
                    
                    await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing loyalty accrual message.");
                    await _channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
                }
            };

            await _channel.BasicConsumeAsync(queue: "identity.loyalty.queue", autoAck: false, consumer: consumer, cancellationToken: stoppingToken);
            _logger.LogInformation("Loyalty RabbitMQ Consumer started successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to RabbitMQ for Loyalty Consumer.");
        }
    }

    private async Task ProcessCustomerRegisteredEventAsync(string messageJson)
    {
        var jsonDoc = JsonDocument.Parse(messageJson);
        var root = jsonDoc.RootElement;

        bool hasEmail = root.TryGetProperty("Email", out var emailElement) || root.TryGetProperty("email", out emailElement);
        bool hasId = root.TryGetProperty("CustomerId", out var idElement) || root.TryGetProperty("customerId", out idElement);
        if (!hasEmail || !hasId) return;

        string? email = emailElement.GetString();
        if (string.IsNullOrWhiteSpace(email)) return;
        Guid customerId = idElement.GetGuid();

        string firstName = (root.TryGetProperty("FirstName", out var fn) || root.TryGetProperty("firstName", out fn)) ? (fn.GetString() ?? "Valued") : "Valued";
        string lastName = (root.TryGetProperty("LastName", out var ln) || root.TryGetProperty("lastName", out ln)) ? (ln.GetString() ?? "Member") : "Member";
        string? phone = (root.TryGetProperty("Phone", out var ph) || root.TryGetProperty("phone", out ph)) ? ph.GetString() : null;

        // Truncate fields according to database schema constraints
        if (firstName.Length > 100) firstName = firstName[..100];
        if (lastName.Length > 100) lastName = lastName[..100];
        if (phone != null && phone.Length > 20) phone = phone[..20];

        using var conn = (System.Data.Common.DbConnection)_dbFactory.CreateConnection();
        await conn.OpenAsync();
        using var tx = await conn.BeginTransactionAsync();
        try
        {
            var tierId = await Dapper.SqlMapper.ExecuteScalarAsync<Guid?>(conn, "SELECT tier_id FROM loyalty.tiers WHERE name = 'Bronze' LIMIT 1", tx);
            if (tierId == null) return;

            const string insertSql = @"
                INSERT INTO loyalty.members (member_id, first_name, last_name, email, phone, tier_id, is_active) 
                VALUES (@MemberId, @FirstName, @LastName, @Email, @Phone, @TierId, true) 
                ON CONFLICT (email) DO NOTHING;";
            await Dapper.SqlMapper.ExecuteAsync(conn, insertSql, new { 
                MemberId = customerId, 
                FirstName = firstName, 
                LastName = lastName, 
                Email = email.ToLowerInvariant(), 
                Phone = phone, 
                TierId = tierId.Value 
            }, tx);

            // Provision a welcome voucher for the new member
            string welcomeCode = $"WELCOME-{customerId.ToString("N")[..8].ToUpperInvariant()}";
            const string insertVoucherSql = @"
                INSERT INTO loyalty.vouchers (code, voucher_type, target_item_type, discount_value, is_redeemed, expires_at) 
                VALUES (@Code, 'fixed_discount'::loyalty.voucher_type, 'order_total', 5.00, false, now() + interval '90 days') 
                ON CONFLICT (code) DO NOTHING;";
            await Dapper.SqlMapper.ExecuteAsync(conn, insertVoucherSql, new { Code = welcomeCode }, tx);

            await tx.CommitAsync();
            _logger.LogInformation("Auto-provisioned Bronze Loyalty account and welcome voucher {VoucherCode} for {Email}", welcomeCode, email);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    private async Task ProcessOrderCompletedEventAsync(string messageJson)
    {
        var jsonDoc = JsonDocument.Parse(messageJson);
        var root = jsonDoc.RootElement;
        
        if (!root.TryGetProperty("CustomerEmail", out var emailElement) || 
            !root.TryGetProperty("TotalAmount", out var totalAmountElement) ||
            !root.TryGetProperty("OrderId", out var orderIdElement))
        {
            return;
        }

        string? email = emailElement.ValueKind == JsonValueKind.Null ? null : emailElement.GetString();
        decimal totalAmount = totalAmountElement.GetDecimal();
        Guid orderId = orderIdElement.GetGuid();

        if (string.IsNullOrEmpty(email) || totalAmount <= 0) return;

        using var conn = (System.Data.Common.DbConnection)_dbFactory.CreateConnection();
        await conn.OpenAsync();

        const string getMemberSql = @"
            SELECT m.member_id, t.points_multiplier, t.points_per_dollar
            FROM loyalty.members m
            JOIN loyalty.tiers t ON m.tier_id = t.tier_id
            WHERE m.email = @Email AND m.is_active = true";

        var memberInfo = await conn.QuerySingleOrDefaultAsync<dynamic>(getMemberSql, new { Email = email });
        if (memberInfo == null) return;

        decimal multiplier = memberInfo.points_multiplier;
        decimal pointsPerDollar = memberInfo.points_per_dollar;
        int pointsEarned = (int)Math.Floor(totalAmount * pointsPerDollar * multiplier);

        if (pointsEarned > 0)
        {
            using var tx = await conn.BeginTransactionAsync();
            try
            {
                const string insertLedgerSql = @"
                    INSERT INTO loyalty.points_ledger 
                    (member_id, transaction_type, points_delta, reference_order_id, description)
                    VALUES 
                    (@MemberId, 'earn_purchase', @PointsDelta, @OrderId, @Description)";

                await conn.ExecuteAsync(insertLedgerSql, new
                {
                    MemberId = (Guid)memberInfo.member_id,
                    PointsDelta = pointsEarned,
                    OrderId = orderId,
                    Description = "Points earned from Order " + orderId.ToString().Substring(0, 8)
                }, tx);

                await tx.CommitAsync();
                _logger.LogInformation("Awarded {Points} points to {Email} for Order {OrderId}", pointsEarned, email, orderId);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }
    }

    private async Task ProcessOrderCancelledEventAsync(string messageJson)
    {
        var jsonDoc = JsonDocument.Parse(messageJson);
        var root = jsonDoc.RootElement;
        
        if (!root.TryGetProperty("CustomerEmail", out var emailElement) || 
            !root.TryGetProperty("TotalAmount", out var totalAmountElement) ||
            !root.TryGetProperty("OrderId", out var orderIdElement))
        {
            return;
        }

        string? email = emailElement.ValueKind == JsonValueKind.Null ? null : emailElement.GetString();
        Guid orderId = orderIdElement.GetGuid();

        string? voucherCode = null;
        if (root.TryGetProperty("VoucherCode", out var voucherElement) && voucherElement.ValueKind != JsonValueKind.Null)
        {
            voucherCode = voucherElement.GetString();
        }

        using var conn = (System.Data.Common.DbConnection)_dbFactory.CreateConnection();
        await conn.OpenAsync();
        using var tx = await conn.BeginTransactionAsync();
        
        try
        {
            // 1. Reactivate Voucher if used
            if (!string.IsNullOrEmpty(voucherCode))
            {
                const string updateVoucherSql = @"UPDATE loyalty.vouchers SET is_redeemed = false, redeemed_at = null, redeemed_order_id = null WHERE code = @Code";
                await conn.ExecuteAsync(updateVoucherSql, new { Code = voucherCode }, tx);
                _logger.LogInformation("Voucher {VoucherCode} reactivated due to order cancellation.", voucherCode);
            }

            // 2. Clawback Points if earned
            if (!string.IsNullOrEmpty(email))
            {
                // Find if they earned points for this order
                const string findEarnedSql = @"SELECT points_delta, member_id FROM loyalty.points_ledger WHERE reference_order_id = @OrderId AND transaction_type = 'earn_purchase'";
                var earned = await conn.QuerySingleOrDefaultAsync<dynamic>(findEarnedSql, new { OrderId = orderId }, tx);
                
                if (earned != null)
                {
                    const string insertLedgerSql = @"
                        INSERT INTO loyalty.points_ledger 
                        (member_id, transaction_type, points_delta, reference_order_id, description)
                        VALUES 
                        (@MemberId, 'clawback_refund', @PointsDelta, @OrderId, @Description)";

                    await conn.ExecuteAsync(insertLedgerSql, new
                    {
                        MemberId = (Guid)earned.member_id,
                        PointsDelta = -((int)earned.points_delta),
                        OrderId = orderId,
                        Description = "Points clawback for refunded Order " + orderId.ToString().Substring(0, 8)
                    }, tx);
                    _logger.LogInformation("Clawed back {Points} points from Member {MemberId} for refunded Order {OrderId}", (int)earned.points_delta, (Guid)earned.member_id, orderId);
                }
            }

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }
}

