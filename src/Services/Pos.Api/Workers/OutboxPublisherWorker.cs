using System;
using System.Threading;
using System.Threading.Tasks;
using Cinema.Foundation.Data;
using Cinema.Foundation.Messaging;
using Dapper;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Pos.Api.Workers;

public class OutboxPublisherWorker : BackgroundService
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly IEventBus _eventBus;
    private readonly ILogger<OutboxPublisherWorker> _logger;

    public OutboxPublisherWorker(IDbConnectionFactory dbFactory, IEventBus eventBus, ILogger<OutboxPublisherWorker> logger)
    {
        _dbFactory = dbFactory;
        _eventBus = eventBus;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox Publisher Worker starting...");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox messages");
            }

            await Task.Delay(2000, stoppingToken);
        }
    }

    private async Task ProcessOutboxMessagesAsync(CancellationToken stoppingToken)
    {
        using var conn = (NpgsqlConnection)_dbFactory.CreateConnection();
        await conn.OpenAsync(stoppingToken);

        const string fetchSql = @"
            WITH cte AS (
                SELECT id 
                FROM messaging.outbox_messages
                WHERE status = 'pending' AND service_name = 'pos-api'
                ORDER BY created_at ASC
                LIMIT 50
                FOR UPDATE SKIP LOCKED
            )
            UPDATE messaging.outbox_messages m
            SET status = 'processing'
            FROM cte
            WHERE m.id = cte.id
            RETURNING m.id, m.routing_key, m.payload;
        ";

        var messages = (await conn.QueryAsync<dynamic>(fetchSql)).AsList();

        if (messages.Count == 0) return;

        foreach (var msg in messages)
        {
            Guid id = (Guid)msg.id;
            string key = (string)msg.routing_key;
            string payloadJson = (string)msg.payload;

            try
            {
                var payload = System.Text.Json.JsonSerializer.Deserialize<dynamic>(payloadJson);
                
                await _eventBus.PublishAsync(key, payload);

                await conn.ExecuteAsync("UPDATE messaging.outbox_messages SET status = 'published', published_at = NOW() WHERE id = @Id", new { Id = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish outbox message {Id}", id);
                await conn.ExecuteAsync("UPDATE messaging.outbox_messages SET status = 'pending', error = @Error WHERE id = @Id", new { Id = id, Error = ex.Message });
            }
        }
    }
}
