using Microsoft.AspNetCore.Mvc;

namespace Pos.Api.Endpoints;

public static class AdminFinanceEndpoints
{
    public static void MapAdminFinanceEndpoints(this IEndpointRouteBuilder routes)
    {
        var admin = routes.MapGroup("/api/v1/admin/finance")
            .WithTags("Back-Office Admin — Finance & Payments")
            .RequireAuthorization("SuperAdmin");

        admin.MapGet("/settlements", () => Results.Ok(new object[] { }))
            .WithSummary("Payment gateway settlement view");
            
        admin.MapGet("/refunds", () => Results.Ok(new object[] { }))
            .WithSummary("Refund and chargeback tracking");
            
        admin.MapGet("/taxes", () => Results.Ok(new object[] { }))
            .WithSummary("List tax/fee configurations");

        admin.MapPost("/taxes", ([FromBody] object request) => Results.Created("/api/v1/admin/finance/taxes/1", new { id = Guid.NewGuid() }))
            .WithSummary("Create tax/fee configuration");
    }
}
