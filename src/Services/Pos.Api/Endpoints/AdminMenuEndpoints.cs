using Microsoft.AspNetCore.Mvc;
using Pos.Api.Models;
using Pos.Api.Repositories;

namespace Pos.Api.Endpoints;

public static class AdminMenuEndpoints
{
    public static void MapAdminMenuEndpoints(this IEndpointRouteBuilder routes)
    {
        var admin = routes.MapGroup("/api/v1/admin/menu")
            .WithTags("Back-Office Admin — F&B Menu")
            .RequireAuthorization("InventoryManager");

        admin.MapGet("/", async (IPosRepository repo, [FromQuery] Guid? branchId) => 
            Results.Ok(await repo.GetActiveProductsAsync(branchId)))
            .WithSummary("List F&B menu items");

        admin.MapPost("/", async (IPosRepository repo, [FromBody] CreateProductRequest request) => 
        {
            var id = await repo.CreateProductAsync(request);
            return Results.Created($"/api/v1/admin/menu/{id}", new { id, status = "created" });
        })
        .WithSummary("Create F&B menu item");

        admin.MapPut("/{itemId:guid}", async (Guid itemId, IPosRepository repo, [FromBody] UpdateProductRequest request) => 
        {
            var success = await repo.UpdateProductAsync(itemId, request);
            return success ? Results.Ok(new { itemId, status = "updated" }) : Results.NotFound();
        })
        .WithSummary("Update F&B menu item");

        admin.MapDelete("/{itemId:guid}", async (Guid itemId, IPosRepository repo) => 
        {
            var success = await repo.DeleteProductAsync(itemId);
            return success ? Results.Ok(new { itemId, status = "deleted" }) : Results.NotFound();
        })
        .WithSummary("Delete F&B menu item");
            
        admin.MapPost("/combos", async (IPosRepository repo, [FromBody] CreateComboRequest request) => 
        {
            var id = await repo.CreateComboAsync(request);
            return Results.Created($"/api/v1/admin/menu/combos/{id}", new { id, status = "created" });
        })
        .WithSummary("Create Combo/Bundle");
    }
}
