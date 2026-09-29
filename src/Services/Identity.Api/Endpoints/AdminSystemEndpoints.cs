using System;
using System.Security.Claims;
using Identity.Api.Models;
using Identity.Api.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Identity.Api.Endpoints;

public static class AdminSystemEndpoints
{
    public static void MapAdminSystemEndpoints(this IEndpointRouteBuilder routes)
    {
        var system = routes.MapGroup("/api/v1/admin/system")
            .WithTags("System & Technical Admin")
            .RequireAuthorization("SuperAdmin");

        // -------------------------------------------------------------
        // Feature Flags
        // -------------------------------------------------------------
        system.MapGet("/feature-flags", async (IAdminSystemRepository repo) =>
        {
            var flags = await repo.GetFeatureFlagsAsync();
            return Results.Ok(flags);
        })
        .WithSummary("List Feature Flags")
        .WithDescription("Retrieves runtime system feature flags and their current toggle states.");

        system.MapGet("/feature-flags/{key}", async (string key, IAdminSystemRepository repo) =>
        {
            var flag = await repo.GetFeatureFlagByKeyAsync(key);
            if (flag == null)
            {
                return Results.NotFound(new { message = $"Feature flag '{key}' not found." });
            }
            return Results.Ok(flag);
        })
        .WithSummary("Get Feature Flag")
        .WithDescription("Retrieves state and metadata for a specific feature flag.");

        system.MapPut("/feature-flags/{key}", async (
            string key,
            [FromBody] UpdateFeatureFlagRequest request,
            HttpContext context,
            IAdminSystemRepository repo) =>
        {
            var actorId = GetCallerUserId(context);
            var updated = await repo.UpdateFeatureFlagAsync(
                key, request.IsEnabled, request.Description, request.Environment, actorId);

            return Results.Ok(updated);
        })
        .WithSummary("Update Feature Flag")
        .WithDescription("Toggles or updates runtime behavior of a feature flag with tamper-evident audit logging.");

        // -------------------------------------------------------------
        // Tamper-Proof Audit Trail
        // -------------------------------------------------------------
        system.MapGet("/audit-logs", async (
            [AsParameters] AuditLogFilter filter,
            IAdminSystemRepository repo) =>
        {
            var logs = await repo.GetAuditLogsAsync(filter);
            return Results.Ok(logs);
        })
        .WithSummary("Query System Audit Trail")
        .WithDescription("Queries system-wide chronological administrative actions and change history.");

        // -------------------------------------------------------------
        // Integrations & Infrastructure Health Telemetry
        // -------------------------------------------------------------
        system.MapGet("/integrations/health", async (IAdminSystemRepository repo) =>
        {
            var pgHealth = await repo.CheckPostgresHealthAsync();
            var redisHealth = await repo.CheckRedisHealthAsync();

            var components = new[] { pgHealth, redisHealth };
            string overall = components.Any(c => c.Status == "Unhealthy")
                ? "Unhealthy"
                : components.Any(c => c.Status == "Degraded")
                    ? "Degraded"
                    : "Healthy";

            return Results.Ok(new SystemIntegrationsHealthResponse(
                OverallStatus: overall,
                Dependencies: components,
                CheckedAt: DateTimeOffset.UtcNow
            ));
        })
        .WithSummary("Integrations Health Status")
        .WithDescription("Performs real-time latency and connectivity ping checks for core infrastructure dependencies.");
    }

    private static Guid GetCallerUserId(HttpContext context)
    {
        var idClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? context.User.FindFirst("sub")?.Value;
        return Guid.TryParse(idClaim, out var uid) ? uid : Guid.Empty;
    }
}
