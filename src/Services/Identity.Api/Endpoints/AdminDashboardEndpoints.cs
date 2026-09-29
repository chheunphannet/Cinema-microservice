using Cinema.Foundation.Security;
using Identity.Api.Services;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Identity.Api.Endpoints;

public static class AdminDashboardEndpoints
{
    public static void MapAdminDashboardEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/admin/dashboards").RequireAuthorization();

        group.MapGet("/executive", async (
            HttpContext context,
            [FromQuery] DateTimeOffset? fromDate,
            [FromQuery] DateTimeOffset? toDate,
            IAdminDashboardService service) =>
        {
            if (!IsTenantAuthorized(context, out var branchId)) return Results.Forbid();
            var result = await service.GetExecutiveDashboardAsync(branchId, fromDate, toDate);
            return Results.Ok(result);
        });

        group.MapGet("/sales", async (
            HttpContext context,
            [FromQuery] string? granularity,
            IAdminDashboardService service) =>
        {
            if (!IsTenantAuthorized(context, out var branchId)) return Results.Forbid();
            var result = await service.GetSalesRevenueDashboardAsync(branchId, granularity);
            return Results.Ok(result);
        });

        group.MapGet("/occupancy", async (
            HttpContext context,
            [FromQuery] Guid? movieId,
            IAdminDashboardService service) =>
        {
            if (!IsTenantAuthorized(context, out var branchId)) return Results.Forbid();
            var result = await service.GetOccupancyDashboardAsync(branchId, movieId);
            return Results.Ok(result);
        });

        group.MapGet("/inventory", async (
            HttpContext context,
            IAdminDashboardService service) =>
        {
            if (!IsTenantAuthorized(context, out var branchId)) return Results.Forbid();
            var result = await service.GetInventoryDashboardAsync(branchId);
            return Results.Ok(result);
        });

        group.MapGet("/finance-pnl", async (
            HttpContext context,
            [FromQuery] DateTimeOffset? fromDate,
            [FromQuery] DateTimeOffset? toDate,
            IAdminDashboardService service) =>
        {
            if (!IsTenantAuthorized(context, out _)) return Results.Forbid();
            var result = await service.GetProfitLossDashboardAsync(fromDate, toDate);
            return Results.Ok(result);
        });

        group.MapGet("/crm-loyalty", async (
            HttpContext context,
            IAdminDashboardService service) =>
        {
            if (!IsTenantAuthorized(context, out _)) return Results.Forbid();
            var result = await service.GetCustomerCRMDashboardAsync();
            return Results.Ok(result);
        });

        group.MapGet("/marketing", async (
            HttpContext context,
            [FromQuery] Guid? campaignId,
            IAdminDashboardService service) =>
        {
            if (!IsTenantAuthorized(context, out _)) return Results.Forbid();
            var result = await service.GetMarketingDashboardAsync(campaignId);
            return Results.Ok(result);
        });

        group.MapGet("/funnel", async (
            HttpContext context,
            [FromQuery] DateTimeOffset? fromDate,
            [FromQuery] DateTimeOffset? toDate,
            IAdminDashboardService service) =>
        {
            if (!IsTenantAuthorized(context, out _)) return Results.Forbid();
            var result = await service.GetFunnelDashboardAsync(fromDate, toDate);
            return Results.Ok(result);
        });

        group.MapGet("/operations", async (
            HttpContext context,
            IAdminDashboardService service) =>
        {
            if (!IsTenantAuthorized(context, out var branchId)) return Results.Forbid();
            var result = await service.GetOperationsDashboardAsync(branchId);
            return Results.Ok(result);
        });

        group.MapGet("/system-health", async (
            HttpContext context,
            IAdminDashboardService service) =>
        {
            if (!IsTenantAuthorized(context, out _)) return Results.Forbid();
            var result = await service.GetSystemHealthDashboardAsync();
            return Results.Ok(result);
        });

        group.MapGet("/fraud-risk", async (
            HttpContext context,
            [FromQuery] DateTimeOffset? fromDate,
            [FromQuery] DateTimeOffset? toDate,
            IAdminDashboardService service) =>
        {
            if (!IsTenantAuthorized(context, out _)) return Results.Forbid();
            var result = await service.GetFraudRiskDashboardAsync(fromDate, toDate);
            return Results.Ok(result);
        });
    }

    public static bool IsTenantAuthorized(HttpContext context, out Guid? requestedBranchId)
    {
        requestedBranchId = null;
        var user = context.User;

        if (context.Request.Query.TryGetValue("branch_id", out var qBranchId) && Guid.TryParse(qBranchId, out var parsedQ))
        {
            requestedBranchId = parsedQ;
        }
        else if (context.Request.Query.TryGetValue("branchId", out var qBranchIdCamel) && Guid.TryParse(qBranchIdCamel, out var parsedQCamel))
        {
            requestedBranchId = parsedQCamel;
        }
        else if (context.Request.RouteValues.TryGetValue("branch_id", out var rBranchId) && Guid.TryParse(rBranchId?.ToString(), out var parsedR))
        {
            requestedBranchId = parsedR;
        }
        else if (context.Request.RouteValues.TryGetValue("branchId", out var rBranchIdCamel) && Guid.TryParse(rBranchIdCamel?.ToString(), out var parsedRCamel))
        {
            requestedBranchId = parsedRCamel;
        }
        else if (context.Request.Headers.TryGetValue("X-Branch-Id", out var hBranchId) && Guid.TryParse(hBranchId, out var parsedH))
        {
            requestedBranchId = parsedH;
        }
        else if (context.Request.Headers.TryGetValue("X-BranchId", out var hBranchIdCamel) && Guid.TryParse(hBranchIdCamel, out var parsedHCamel))
        {
            requestedBranchId = parsedHCamel;
        }

        var role = user.FindFirstValue(ClaimTypes.Role) ?? "";
        if (role == "super_admin" || role == "system_admin" || role == "executive" || role == "content_manager" || role == "finance_manager")
        {
            return true;
        }

        if (role == "branch_manager" || role == "staff" || role == "supervisor" || role == "cashier")
        {
            var userBranchIdStr = user.FindFirstValue("branch_id") ?? user.FindFirstValue("branchId");
            if (string.IsNullOrEmpty(userBranchIdStr) || !Guid.TryParse(userBranchIdStr, out var userBranchId))
            {
                return false;
            }

            if (!requestedBranchId.HasValue)
            {
                return false;
            }

            if (requestedBranchId.Value != userBranchId)
            {
                return false;
            }
            
            return true;
        }
        
        return false;
    }
}


