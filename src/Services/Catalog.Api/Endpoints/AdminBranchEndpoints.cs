using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Endpoints;

public static class AdminBranchEndpoints
{
    public static void MapAdminBranchEndpoints(this IEndpointRouteBuilder routes)
    {
        var admin = routes.MapGroup("/api/v1/admin/branches")
            .WithTags("Back-Office Admin — Branches & Screens")
            .RequireAuthorization("SuperAdmin");

        admin.MapGet("/", () => Results.Ok(new object[] { }))
            .WithSummary("List all branches");

        admin.MapPost("/", ([FromBody] object request) => Results.Created("/api/v1/admin/branches/1", new { id = Guid.NewGuid(), status = "created" }))
            .WithSummary("Create branch");

        admin.MapPut("/{branchId:guid}", (Guid branchId, [FromBody] object request) => Results.Ok(new { branchId, status = "updated" }))
            .WithSummary("Update branch");

        admin.MapGet("/{branchId:guid}/screens", (Guid branchId) => Results.Ok(new object[] { }))
            .WithSummary("List screens in branch");

        admin.MapPost("/{branchId:guid}/screens", (Guid branchId, [FromBody] object request) => Results.Created($"/api/v1/admin/branches/{branchId}/screens/1", new { id = Guid.NewGuid() }))
            .WithSummary("Create screen in branch");

        admin.MapPatch("/{branchId:guid}/screens/{screenId:guid}/status", (Guid branchId, Guid screenId, [FromBody] object request) => Results.Ok(new { screenId, status = "toggled" }))
            .WithSummary("Toggle screen status (e.g. maintenance)");
            
        admin.MapPut("/{branchId:guid}/screens/{screenId:guid}/seat-map", (Guid branchId, Guid screenId, [FromBody] object request) => Results.Ok(new { screenId, message = "Seat map updated" }))
            .WithSummary("Update seat map for screen");
    }
}
