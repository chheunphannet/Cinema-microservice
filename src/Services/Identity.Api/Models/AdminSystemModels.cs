using System;
using System.Collections.Generic;

namespace Identity.Api.Models;

public record FeatureFlagDto(
    string Key,
    string Description,
    bool IsEnabled,
    string Environment,
    Guid? UpdatedBy,
    DateTimeOffset UpdatedAt
);

public record UpdateFeatureFlagRequest(
    bool IsEnabled,
    string? Description = null,
    string? Environment = null
);

public record AuditLogFilter(
    Guid? ActorId = null,
    string? ServiceName = null,
    string? Action = null,
    string? ResourceType = null,
    DateTimeOffset? FromDate = null,
    DateTimeOffset? ToDate = null,
    int Page = 1,
    int PageSize = 20
);

public record SystemAuditLogEntry(
    Guid LogId,
    Guid ActorId,
    string? ActorEmail,
    string? ActorRole,
    string ServiceName,
    string Action,
    string ResourceType,
    string? ResourceId,
    string DetailsJson,
    string? IpAddress,
    DateTimeOffset OccurredAt
);

public record SystemAuditLogResponse(
    IReadOnlyList<SystemAuditLogEntry> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);

public record IntegrationHealthItem(
    string Component,
    string Status,
    double LatencyMs,
    string? Details = null
);

public record SystemIntegrationsHealthResponse(
    string OverallStatus,
    IReadOnlyList<IntegrationHealthItem> Dependencies,
    DateTimeOffset CheckedAt
);
