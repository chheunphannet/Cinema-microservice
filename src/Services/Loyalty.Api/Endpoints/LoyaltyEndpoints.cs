using Loyalty.Api.Models;
using Loyalty.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Loyalty.Api.Endpoints;

public static class LoyaltyEndpoints
{
    public static void MapLoyaltyEndpoints(this IEndpointRouteBuilder routes)
    {
        var loyalty = routes.MapGroup("/api/v1/loyalty")
            .WithTags("Loyalty, CRM & Vouchers")
            .RequireAuthorization();

        loyalty.MapGet("/members/{email}", async (
            string email,
            ILoyaltyService loyaltyService) =>
        {
            return await loyaltyService.GetMemberProfileAsync(email);
        })
        .WithSummary("Get Member Profile")
        .WithDescription("Retrieves a CRM member profile and current point balance.");

        loyalty.MapPost("/vouchers/validate", async (
            [FromBody] ValidateVoucherRequest request,
            ILoyaltyService loyaltyService) =>
        {
            return await loyaltyService.ValidateVoucherAsync(request);
        })
        .AllowAnonymous()
        .WithSummary("Validate Loyalty Voucher")
        .WithDescription("Validates a voucher code and returns discount values if valid.");
    }
}
