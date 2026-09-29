using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Identity.Api.Models;

namespace Identity.Api.Repositories;

public interface IAdminSystemRepository
{
    Task<IReadOnlyList<FeatureFlagDto>> GetFeatureFlagsAsync();
    Task<FeatureFlagDto?> GetFeatureFlagByKeyAsync(string key);
    Task<FeatureFlagDto?> UpdateFeatureFlagAsync(string key, bool isEnabled, string? description, string? environment, Guid actorId);
    Task<SystemAuditLogResponse> GetAuditLogsAsync(AuditLogFilter filter);
    Task<IntegrationHealthItem> CheckPostgresHealthAsync();
    Task<IntegrationHealthItem> CheckRedisHealthAsync();
}
