using Cinema.Foundation.Data;
using Cinema.Foundation.Messaging;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Npgsql;
using Pos.Api.Models;
using Pos.Api.Repositories;
using Pos.Api.Telemetry;

namespace Pos.Api.Services;

public class WebhookIdempotencyService : IWebhookIdempotencyService
{
    private readonly IPosRepository _repository;
    private readonly IDbConnectionFactory _dbFactory;
    private readonly IDistributedCache _cache;
    private readonly IEventBus _eventBus;

    public WebhookIdempotencyService(
        IPosRepository repository, 
        IDbConnectionFactory dbFactory, 
        IDistributedCache cache, 
        IEventBus eventBus)
    {
        _repository = repository;
        _dbFactory = dbFactory;
        _cache = cache;
        _eventBus = eventBus;
    }

    public async Task<IResult> HandleWebhookAsync(PaymentWebhookPayload? payload)
    {
        if (payload == null || string.IsNullOrWhiteSpace(payload.IdempotencyKey))
        {
            return Results.BadRequest(new { error = "Request body with valid IdempotencyKey and OrderId is required." });
        }

        var swTotal = System.Diagnostics.Stopwatch.StartNew();
        var idempotencyKey = payload.IdempotencyKey;
        var redisKey = $"payment:webhook:{idempotencyKey}";

        var swRedis = System.Diagnostics.Stopwatch.StartNew();
        var existingStatus = await _cache.GetStringAsync(redisKey);
        swRedis.Stop();
        PosMetrics.RedisExecutionHistogram.Record(swRedis.Elapsed.TotalMilliseconds);

        if (!string.IsNullOrEmpty(existingStatus))
        {
            if (existingStatus == Constants.WebhookStatuses.Processing)
            {
                return Results.Conflict(new
                {
                    error = "Payment is currently processing. Concurrent retry rejected.",
                    status = Constants.WebhookStatuses.Processing,
                    idempotencyKey
                });
            }
            else if (existingStatus.StartsWith(Constants.WebhookStatuses.Succeeded))
            {
                return Results.Ok(new
                {
                    status = Constants.WebhookStatuses.Succeeded,
                    cached = true,
                    idempotencyKey,
                    message = "Payment already processed successfully."
                });
            }
        }

        await _cache.SetStringAsync(redisKey, Constants.WebhookStatuses.Processing, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60)
        });

        try
        {
            using var conn = (NpgsqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            var paymentId = Guid.NewGuid();
            var swSql = System.Diagnostics.Stopwatch.StartNew();
            using var tx = await conn.BeginTransactionAsync();

            await _repository.EnsureWalkInOrderExistsAsync(payload.OrderId, payload.Amount, conn, tx);

            string providerRef = payload.TransactionReference ?? $"WH-{idempotencyKey}";
            await _repository.InsertPaymentAsync(paymentId, payload.OrderId, Constants.PaymentMethods.Qr, payload.Amount, providerRef, conn, tx);
            await _repository.UpdateOrderStatusToPaidAsync(payload.OrderId, conn, tx);

            await tx.CommitAsync();
            swSql.Stop();
            PosMetrics.SqlCommitHistogram.Record(swSql.Elapsed.TotalMilliseconds);

            await _cache.SetStringAsync(redisKey, Constants.WebhookStatuses.Succeeded, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
            });

            await _eventBus.PublishAsync("payment.captured", new PaymentCapturedIntegrationEvent(
                paymentId,
                payload.OrderId,
                "webhook_qr",
                payload.Amount,
                DateTimeOffset.UtcNow));

            swTotal.Stop();
            PosMetrics.WebhookAckHistogram.Record(swTotal.Elapsed.TotalMilliseconds);

            return Results.Ok(new
            {
                status = Constants.WebhookStatuses.Succeeded,
                paymentId,
                orderId = payload.OrderId,
                amount = payload.Amount,
                idempotencyKey,
                p99Metrics = new
                {
                    webhookAckLatencyMs = swTotal.Elapsed.TotalMilliseconds,
                    redisLatencyMs = swRedis.Elapsed.TotalMilliseconds,
                    sqlCommitDurationMs = swSql.Elapsed.TotalMilliseconds
                }
            });
        }
        catch (Exception)
        {
            await _cache.RemoveAsync(redisKey);
            return Results.Problem("Payment webhook failed due to an internal error.");
        }
    }
}
