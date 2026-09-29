using System;
using System.Security.Claims;
using Loyalty.Api.Models;
using Loyalty.Api.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Loyalty.Api.Endpoints;

public static class AdminLoyaltyEndpoints
{
    public static void MapAdminLoyaltyEndpoints(this IEndpointRouteBuilder routes)
    {
        // =====================================================================
        // Module 7: Loyalty CRM Operations
        // =====================================================================
        var loyaltyAdmin = routes.MapGroup("/api/v1/admin/loyalty")
            .WithTags("Back-Office Loyalty CRM")
            .RequireAuthorization();

        loyaltyAdmin.MapGet("/members", async (
            [AsParameters] AdminMemberFilter filter,
            IAdminLoyaltyService service) =>
        {
            return await service.GetMembersAsync(filter);
        })
        .RequireAuthorization("CustomerSupport")
        .WithSummary("List Loyalty Members")
        .WithDescription("Searches loyalty members filtered by name, email, tier, or active status.");

        loyaltyAdmin.MapGet("/members/{customerId:guid}/history", async (
            Guid customerId,
            [FromQuery] int limit,
            IAdminLoyaltyService service) =>
        {
            return await service.GetMemberLedgerAsync(customerId, limit <= 0 ? 50 : limit);
        })
        .RequireAuthorization("CustomerSupport")
        .WithSummary("Get Member Points Ledger")
        .WithDescription("Retrieves append-only points audit ledger for a loyalty customer.");

        loyaltyAdmin.MapPost("/customers/{customerId:guid}/adjust-points", async (
            Guid customerId,
            [FromBody] AdjustPointsRequest request,
            ClaimsPrincipal user,
            IAdminLoyaltyService service) =>
        {
            return await service.AdjustMemberPointsAsync(customerId, request, user);
        })
        .RequireAuthorization("CustomerSupport")
        .WithSummary("Manual Points Adjustment")
        .WithDescription("Credits or debits loyalty points with mandatory justification note and audit record.");

        loyaltyAdmin.MapPut("/customers/{customerId:guid}/override-tier", async (
            Guid customerId,
            [FromBody] OverrideTierRequest request,
            ClaimsPrincipal user,
            IAdminLoyaltyService service) =>
        {
            return await service.OverrideMemberTierAsync(customerId, request, user);
        })
        .RequireAuthorization("MarketingManager")
        .WithSummary("Override Member Tier")
        .WithDescription("Forces a customer into a specific loyalty tier (e.g. Bronze, Silver, Gold, Platinum).");

        // =====================================================================
        // Module 8: Marketing & CRM Campaigns
        // =====================================================================
        var marketingAdmin = routes.MapGroup("/api/v1/admin/marketing")
            .WithTags("Back-Office Marketing & CRM")
            .RequireAuthorization("MarketingManager");

        marketingAdmin.MapGet("/segments", async (IAdminLoyaltyService service) =>
        {
            return await service.GetCustomerSegmentsAsync();
        })
        .WithSummary("Get Customer Segments")
        .WithDescription("Aggregates loyalty member counts and percentages across VIP, active, lapsed, and new customer segments.");

        marketingAdmin.MapGet("/promotions", async (
            [FromQuery] string? status,
            [FromQuery] string? search,
            IAdminLoyaltyService service) =>
        {
            return await service.GetCampaignsAsync(status, search);
        })
        .WithSummary("List Marketing Campaigns")
        .WithDescription("Lists promotional campaigns and linked discount codes.");

        marketingAdmin.MapPost("/promotions", async (
            [FromBody] CreateCampaignRequest request,
            ClaimsPrincipal user,
            IAdminLoyaltyService service) =>
        {
            return await service.CreateCampaignAsync(request, user);
        })
        .WithSummary("Create Marketing Campaign")
        .WithDescription("Creates a new marketing campaign targeting a customer demographic.");

        marketingAdmin.MapPost("/broadcasts", async (
            [FromBody] BroadcastCampaignRequest request,
            ClaimsPrincipal user,
            IAdminLoyaltyService service) =>
        {
            return await service.BroadcastCampaignAsync(request, user);
        })
        .WithSummary("Broadcast Campaign Email")
        .WithDescription("Sends promotional announcements via email to a customer segment or preview test recipient.");

        marketingAdmin.MapPost("/vouchers/generate-batch", async (
            [FromBody] GenerateVouchersBatchRequest request,
            ClaimsPrincipal user,
            IAdminLoyaltyService service) =>
        {
            return await service.GenerateVouchersBatchAsync(request, user);
        })
        .WithSummary("Batch Generate Voucher Codes")
        .WithDescription("Generates unique single-use or multi-use discount vouchers for corporate promotions or campaigns.");
    }
}
