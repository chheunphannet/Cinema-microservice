using System.Diagnostics.Metrics;

namespace Pos.Api.Telemetry;

public static class PosMetrics
{
    public static readonly Meter Meter = new("Cinema.Pos.Telemetry");
    
    public static readonly Histogram<double> WebhookAckHistogram = 
        Meter.CreateHistogram<double>("pos.webhook.ack.latency", "ms", "p99 Webhook ACK Latency");
        
    public static readonly Histogram<double> RedisExecutionHistogram = 
        Meter.CreateHistogram<double>("pos.redis.execution.latency", "ms", "p99 Redis Lua Execution");
        
    public static readonly Histogram<double> SqlCommitHistogram = 
        Meter.CreateHistogram<double>("pos.sql.commit.duration", "ms", "p99 SQL Commit Duration");
}
