using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Cinema.Foundation.Data;
using Dapper;
using Identity.Api.Models;
using StackExchange.Redis;

namespace Identity.Api.Repositories;

public class AdminSystemRepository : IAdminSystemRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly IConnectionMultiplexer? _redis;

    public AdminSystemRepository(IDbConnectionFactory db, IConnectionMultiplexer? redis = null)
    {
        _db = db;
        _redis = redis;
    }

    public async Task<IReadOnlyList<FeatureFlagDto>> GetFeatureFlagsAsync()
    {
        using var conn = _db.CreateReadConnection();
        const string sql = @"
            SELECT key, description, is_enabled, environment, updated_by, updated_at
            FROM public.system_feature_flags
            ORDER BY key ASC";

        var rows = await conn.QueryAsync<dynamic>(sql);
        return rows.Select(r => new FeatureFlagDto(
            Key: (string)r.key,
            Description: (string)r.description,
            IsEnabled: (bool)r.is_enabled,
            Environment: (string)r.environment,
            UpdatedBy: (Guid?)r.updated_by,
            UpdatedAt: (DateTimeOffset)r.updated_at
        )).ToList();
    }

    public async Task<FeatureFlagDto?> GetFeatureFlagByKeyAsync(string key)
    {
        using var conn = _db.CreateReadConnection();
        const string sql = @"
            SELECT key, description, is_enabled, environment, updated_by, updated_at
            FROM public.system_feature_flags
            WHERE key = @Key";

        var r = await conn.QuerySingleOrDefaultAsync<dynamic>(sql, new { Key = key.Trim().ToLowerInvariant() });
        if (r == null) return null;

        return new FeatureFlagDto(
            Key: (string)r.key,
            Description: (string)r.description,
            IsEnabled: (bool)r.is_enabled,
            Environment: (string)r.environment,
            UpdatedBy: (Guid?)r.updated_by,
            UpdatedAt: (DateTimeOffset)r.updated_at
        );
    }

    public async Task<FeatureFlagDto?> UpdateFeatureFlagAsync(
        string key, bool isEnabled, string? description, string? environment, Guid actorId)
    {
        using var conn = _db.CreateConnection();
        using var tx = conn.BeginTransaction();

        string normalizedKey = key.Trim().ToLowerInvariant();

        const string existingSql = @"
            SELECT key, description, is_enabled, environment, updated_by, updated_at
            FROM public.system_feature_flags
            WHERE key = @Key
            FOR UPDATE";

        var existing = await conn.QuerySingleOrDefaultAsync<dynamic>(existingSql, new { Key = normalizedKey }, tx);

        string descToUse = description ?? (existing != null ? (string)existing.description : $"Feature flag {normalizedKey}");
        string envToUse = environment ?? (existing != null ? (string)existing.environment : "all");

        const string upsertSql = @"
            INSERT INTO public.system_feature_flags (
                key, description, is_enabled, environment, updated_by, updated_at
            ) VALUES (
                @Key, @Description, @IsEnabled, @Environment, @UpdatedBy, now()
            )
            ON CONFLICT (key) DO UPDATE SET
                is_enabled = EXCLUDED.is_enabled,
                description = EXCLUDED.description,
                environment = EXCLUDED.environment,
                updated_by = EXCLUDED.updated_by,
                updated_at = now()
            RETURNING key, description, is_enabled, environment, updated_by, updated_at";

        var updated = await conn.QuerySingleAsync<dynamic>(upsertSql, new
        {
            Key = normalizedKey,
            Description = descToUse,
            IsEnabled = isEnabled,
            Environment = envToUse,
            UpdatedBy = actorId
        }, tx);

        // Record tamper-evident audit trail
        var details = JsonSerializer.Serialize(new
        {
            key = normalizedKey,
            previousEnabled = existing != null ? (bool)existing.is_enabled : (bool?)null,
            newEnabled = isEnabled,
            environment = envToUse
        });

        const string auditSql = @"
            INSERT INTO public.system_audit_log (
                actor_id, service_name, action, resource_type, resource_id, details, occurred_at
            ) VALUES (
                @ActorId, 'Identity.Api', 'update_feature_flag', 'feature_flag', @ResourceId, @Details::jsonb, now()
            )";

        await conn.ExecuteAsync(auditSql, new
        {
            ActorId = actorId,
            ResourceId = normalizedKey,
            Details = details
        }, tx);

        tx.Commit();

        return new FeatureFlagDto(
            Key: (string)updated.key,
            Description: (string)updated.description,
            IsEnabled: (bool)updated.is_enabled,
            Environment: (string)updated.environment,
            UpdatedBy: (Guid?)updated.updated_by,
            UpdatedAt: (DateTimeOffset)updated.updated_at
        );
    }

    public async Task<SystemAuditLogResponse> GetAuditLogsAsync(AuditLogFilter filter)
    {
        using var conn = _db.CreateReadConnection();

        var sqlBuilder = new StringBuilder(" FROM public.system_audit_log WHERE 1=1 ");
        var parameters = new DynamicParameters();

        if (filter.ActorId.HasValue && filter.ActorId.Value != Guid.Empty)
        {
            sqlBuilder.Append(" AND actor_id = @ActorId");
            parameters.Add("ActorId", filter.ActorId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.ServiceName))
        {
            sqlBuilder.Append(" AND LOWER(service_name) = LOWER(@ServiceName)");
            parameters.Add("ServiceName", filter.ServiceName.Trim());
        }

        if (!string.IsNullOrWhiteSpace(filter.Action))
        {
            sqlBuilder.Append(" AND LOWER(action) = LOWER(@Action)");
            parameters.Add("Action", filter.Action.Trim());
        }

        if (!string.IsNullOrWhiteSpace(filter.ResourceType))
        {
            sqlBuilder.Append(" AND LOWER(resource_type) = LOWER(@ResourceType)");
            parameters.Add("ResourceType", filter.ResourceType.Trim());
        }

        if (filter.FromDate.HasValue)
        {
            sqlBuilder.Append(" AND occurred_at >= @FromDate");
            parameters.Add("FromDate", filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            sqlBuilder.Append(" AND occurred_at <= @ToDate");
            parameters.Add("ToDate", filter.ToDate.Value);
        }

        var countSql = "SELECT COUNT(*) " + sqlBuilder;
        int totalCount = await conn.ExecuteScalarAsync<int>(countSql, parameters);

        int page = filter.Page < 1 ? 1 : filter.Page;
        int pageSize = filter.PageSize is < 1 or > 100 ? 20 : filter.PageSize;
        int offset = (page - 1) * pageSize;

        var querySql = $@"
            SELECT log_id, actor_id, actor_email, actor_role, service_name, action, 
                   resource_type, resource_id, details::text as details_json, ip_address, occurred_at
            {sqlBuilder}
            ORDER BY occurred_at DESC
            LIMIT @Limit OFFSET @Offset";

        parameters.Add("Limit", pageSize);
        parameters.Add("Offset", offset);

        var rows = await conn.QueryAsync<dynamic>(querySql, parameters);
        var items = rows.Select(r => new SystemAuditLogEntry(
            LogId: (Guid)r.log_id,
            ActorId: (Guid)r.actor_id,
            ActorEmail: (string?)r.actor_email,
            ActorRole: (string?)r.actor_role,
            ServiceName: (string)r.service_name,
            Action: (string)r.action,
            ResourceType: (string)r.resource_type,
            ResourceId: (string?)r.resource_id,
            DetailsJson: (string)r.details_json,
            IpAddress: (string?)r.ip_address,
            OccurredAt: (DateTimeOffset)r.occurred_at
        )).ToList();

        int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return new SystemAuditLogResponse(
            Items: items,
            TotalCount: totalCount,
            Page: page,
            PageSize: pageSize,
            TotalPages: totalPages
        );
    }

    public async Task<IntegrationHealthItem> CheckPostgresHealthAsync()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var conn = _db.CreateReadConnection();
            var ping = await conn.ExecuteScalarAsync<int>("SELECT 1");
            sw.Stop();

            return new IntegrationHealthItem(
                Component: "PostgreSQL Database",
                Status: ping == 1 ? "Healthy" : "Degraded",
                LatencyMs: Math.Round(sw.Elapsed.TotalMilliseconds, 2),
                Details: "Active connection pool responding"
            );
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new IntegrationHealthItem(
                Component: "PostgreSQL Database",
                Status: "Unhealthy",
                LatencyMs: Math.Round(sw.Elapsed.TotalMilliseconds, 2),
                Details: ex.Message
            );
        }
    }

    public async Task<IntegrationHealthItem> CheckRedisHealthAsync()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (_redis == null || !_redis.IsConnected)
            {
                return new IntegrationHealthItem(
                    Component: "Redis Distributed Cache",
                    Status: "Degraded",
                    LatencyMs: 0,
                    Details: "Redis multiplexer disconnected or unconfigured"
                );
            }

            var db = _redis.GetDatabase();
            var pingResult = await db.PingAsync();
            sw.Stop();

            return new IntegrationHealthItem(
                Component: "Redis Distributed Cache",
                Status: "Healthy",
                LatencyMs: Math.Round(pingResult.TotalMilliseconds, 2),
                Details: "Redis cache responsive and connected"
            );
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new IntegrationHealthItem(
                Component: "Redis Distributed Cache",
                Status: "Unhealthy",
                LatencyMs: Math.Round(sw.Elapsed.TotalMilliseconds, 2),
                Details: ex.Message
            );
        }
    }
}
