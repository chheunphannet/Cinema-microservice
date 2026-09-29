using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Pos.Api.Models;
using Pos.Api.Repositories;

namespace Pos.Api.Endpoints;

public static class AdminInventoryEndpoints
{
    public static void MapAdminInventoryEndpoints(this IEndpointRouteBuilder routes)
    {
        var admin = routes.MapGroup("/api/v1/admin/inventory")
            .WithTags("Back-Office Admin — Multi-Branch Inventory & Suppliers")
            .RequireAuthorization();

        // =========================================================================
        // 1. Multi-Branch Stock Tracking & Product Creation
        // =========================================================================

        admin.MapPost("/products", async (
            [FromBody] CreateProductRequest req,
            IPosRepository repository,
            HttpContext context) =>
        {
            var (callerBranchId, isSuperAdmin, isInventoryManager) = GetCallerContext(context);
            if (!isSuperAdmin && !isInventoryManager && callerBranchId.HasValue && req.BranchId.HasValue && req.BranchId.Value != Guid.Empty && req.BranchId.Value != callerBranchId.Value)
            {
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Cannot create product for a different branch.");
            }

            if (string.IsNullOrWhiteSpace(req.Name))
            {
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Product name is required.");
            }
            if (req.UnitPrice < 0)
            {
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Unit price cannot be negative.");
            }

            var id = await repository.CreateProductAsync(req);
            return Results.Created($"/api/v1/admin/inventory/products/{id}", new { id, status = "created" });
        })
        .RequireAuthorization(policy => policy.RequireRole("inventory_manager", "branch_manager", "super_admin", "system_admin"))
        .WithSummary("Create F&B Product")
        .WithDescription("Creates a new concession product and initializes stock in branch inventory.");

        admin.MapPut("/products/{productId:guid}", async (
            Guid productId,
            [FromBody] UpdateProductRequest req,
            IPosRepository repository) =>
        {
            var success = await repository.UpdateProductAsync(productId, req);
            return success ? Results.Ok(new { productId, status = "updated" }) : Results.NotFound(new { error = $"Product {productId} not found." });
        })
        .RequireAuthorization(policy => policy.RequireRole("inventory_manager", "branch_manager", "super_admin", "system_admin"))
        .WithSummary("Update F&B Product");

        admin.MapDelete("/products/{productId:guid}", async (
            Guid productId,
            IPosRepository repository) =>
        {
            var success = await repository.DeleteProductAsync(productId);
            return success ? Results.Ok(new { productId, status = "deleted" }) : Results.NotFound(new { error = $"Product {productId} not found." });
        })
        .RequireAuthorization(policy => policy.RequireRole("inventory_manager", "branch_manager", "super_admin", "system_admin"))
        .WithSummary("Delete F&B Product");

        admin.MapGet("/branches/{branchId:guid}/stock", async (
            Guid branchId,
            IPosRepository repository,
            HttpContext context) =>
        {
            var (callerBranchId, isSuperAdmin, isInventoryManager) = GetCallerContext(context);
            if (!isSuperAdmin && !isInventoryManager && callerBranchId.HasValue && branchId != callerBranchId.Value)
            {
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Cannot access inventory of a different branch.");
            }

            var stock = await repository.GetBranchInventoryAsync(branchId);
            return Results.Ok(stock);
        })
        .RequireAuthorization(policy => policy.RequireRole("inventory_manager", "branch_manager", "super_admin", "system_admin"))
        .WithSummary("Get Branch Inventory Stock")
        .WithDescription("Retrieves stock levels, thresholds, and out-of-stock statuses for all products at a specific branch.");

        admin.MapGet("/branches/{branchId:guid}/products/{productId:guid}", async (
            Guid branchId,
            Guid productId,
            IPosRepository repository,
            HttpContext context) =>
        {
            var (callerBranchId, isSuperAdmin, isInventoryManager) = GetCallerContext(context);
            if (!isSuperAdmin && !isInventoryManager && callerBranchId.HasValue && branchId != callerBranchId.Value)
            {
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Cannot access inventory of a different branch.");
            }

            var item = await repository.GetProductBranchInventoryAsync(branchId, productId);
            return item != null ? Results.Ok(item) : Results.NotFound(new { error = $"Inventory item for branch {branchId} and product {productId} not found." });
        })
        .RequireAuthorization(policy => policy.RequireRole("inventory_manager", "branch_manager", "super_admin", "system_admin"))
        .WithSummary("Get Product Stock at Branch");

        admin.MapPost("/branches/{branchId:guid}/products/{productId:guid}/adjust", async (
            Guid branchId,
            Guid productId,
            [FromBody] StockAdjustRequest req,
            IPosRepository repository,
            HttpContext context) =>
        {
            var (callerBranchId, isSuperAdmin, isInventoryManager) = GetCallerContext(context);
            if (!isSuperAdmin && !isInventoryManager && callerBranchId.HasValue && branchId != callerBranchId.Value)
            {
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Cannot adjust inventory of a different branch.");
            }

            var staffId = GetCallerUserId(context);
            var success = await repository.AdjustStockAsync(branchId, productId, req.QuantityDelta, req.Reason, staffId);
            if (!success)
            {
                return Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Failed to adjust stock.");
            }

            var updated = await repository.GetProductBranchInventoryAsync(branchId, productId);
            return Results.Ok(updated);
        })
        .RequireAuthorization(policy => policy.RequireRole("inventory_manager", "branch_manager", "super_admin", "system_admin"))
        .WithSummary("Adjust Stock Level")
        .WithDescription("Adjusts stock quantity (+/-) for restocking or physical count reconciliation.");

        admin.MapPatch("/branches/{branchId:guid}/products/{productId:guid}/availability", async (
            Guid branchId,
            Guid productId,
            [FromBody] ToggleAvailabilityRequest req,
            IPosRepository repository,
            HttpContext context) =>
        {
            var (callerBranchId, isSuperAdmin, isInventoryManager) = GetCallerContext(context);
            if (!isSuperAdmin && !isInventoryManager && callerBranchId.HasValue && branchId != callerBranchId.Value)
            {
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Cannot toggle availability for a different branch.");
            }

            var success = await repository.ToggleProductAvailabilityAsync(branchId, productId, req.IsOutOfStock);
            if (!success)
            {
                return Results.NotFound(new { error = $"Product {productId} inventory at branch {branchId} not found." });
            }

            return Results.Ok(new { branchId, productId, isOutOfStock = req.IsOutOfStock });
        })
        .RequireAuthorization(policy => policy.RequireRole("inventory_manager", "branch_manager", "super_admin", "system_admin"))
        .WithSummary("Toggle Instant Product Out-of-Stock")
        .WithDescription("Immediately toggles out-of-stock flag for a product at a branch, updating POS tills and customer apps.");

        // =========================================================================
        // 2. Wastage & Spoilage Logging
        // =========================================================================

        admin.MapPost("/branches/{branchId:guid}/wastage", async (
            Guid branchId,
            [FromBody] LogWastageRequest req,
            IPosRepository repository,
            HttpContext context) =>
        {
            var (callerBranchId, isSuperAdmin, isInventoryManager) = GetCallerContext(context);
            if (!isSuperAdmin && !isInventoryManager && callerBranchId.HasValue && branchId != callerBranchId.Value)
            {
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Cannot log wastage for a different branch.");
            }

            if (req.Quantity <= 0)
            {
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Quantity must be greater than zero.");
            }

            var staffId = GetCallerUserId(context);
            var wastageId = await repository.LogWastageAsync(branchId, req, staffId);

            return Results.Created($"/api/v1/admin/inventory/branches/{branchId}/wastage", new
            {
                wastageId,
                branchId,
                req.ProductId,
                req.Quantity,
                req.Reason,
                costLoss = req.Quantity * req.UnitCost,
                loggedBy = staffId
            });
        })
        .RequireAuthorization(policy => policy.RequireRole("inventory_manager", "branch_manager", "super_admin", "system_admin"))
        .WithSummary("Log Spoilage / Wastage")
        .WithDescription("Records damaged, expired, or dropped items, writing off stock and calculating cost loss.");

        admin.MapGet("/branches/{branchId:guid}/wastage", async (
            Guid branchId,
            [FromQuery] DateTimeOffset? fromDate,
            [FromQuery] DateTimeOffset? toDate,
            IPosRepository repository,
            HttpContext context) =>
        {
            var (callerBranchId, isSuperAdmin, isInventoryManager) = GetCallerContext(context);
            if (!isSuperAdmin && !isInventoryManager && callerBranchId.HasValue && branchId != callerBranchId.Value)
            {
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Cannot view wastage of a different branch.");
            }

            var wastage = await repository.GetInventoryWastageAsync(branchId, fromDate, toDate);
            return Results.Ok(wastage);
        })
        .RequireAuthorization(policy => policy.RequireRole("inventory_manager", "branch_manager", "super_admin", "system_admin"))
        .WithSummary("List Branch Wastage Log");

        // =========================================================================
        // 3. Reorder Alerts
        // =========================================================================

        admin.MapGet("/reorder-alerts", async (
            [FromQuery] Guid? branchId,
            IPosRepository repository,
            HttpContext context) =>
        {
            var (callerBranchId, isSuperAdmin, isInventoryManager) = GetCallerContext(context);
            Guid? effectiveBranchId = branchId;
            if (!isSuperAdmin && !isInventoryManager && callerBranchId.HasValue)
            {
                effectiveBranchId = callerBranchId.Value;
            }

            var alerts = await repository.GetReorderAlertsAsync(effectiveBranchId);
            return Results.Ok(alerts);
        })
        .RequireAuthorization(policy => policy.RequireRole("inventory_manager", "branch_manager", "super_admin", "system_admin"))
        .WithSummary("Get Low Stock & Reorder Alerts")
        .WithDescription("Returns inventory items whose stock has fallen below the reorder threshold or are marked out of stock.");

        // =========================================================================
        // 4. Suppliers & Purchase Orders
        // =========================================================================

        admin.MapGet("/suppliers", async (IPosRepository repository) =>
        {
            var suppliers = await repository.GetSuppliersAsync();
            return Results.Ok(suppliers);
        })
        .RequireAuthorization(policy => policy.RequireRole("inventory_manager", "super_admin", "system_admin"))
        .WithSummary("List Suppliers");

        admin.MapPost("/suppliers", async (
            [FromBody] CreateSupplierRequest req,
            IPosRepository repository) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name))
            {
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Supplier name is required.");
            }

            var id = await repository.CreateSupplierAsync(req);
            var supplier = await repository.GetSupplierByIdAsync(id);
            return Results.Created($"/api/v1/admin/inventory/suppliers/{id}", supplier);
        })
        .RequireAuthorization(policy => policy.RequireRole("inventory_manager", "super_admin", "system_admin"))
        .WithSummary("Create Supplier");

        admin.MapGet("/purchase-orders", async (
            [FromQuery] Guid? branchId,
            [FromQuery] string? status,
            IPosRepository repository,
            HttpContext context) =>
        {
            var (callerBranchId, isSuperAdmin, isInventoryManager) = GetCallerContext(context);
            Guid? effectiveBranchId = branchId;
            if (!isSuperAdmin && !isInventoryManager && callerBranchId.HasValue)
            {
                effectiveBranchId = callerBranchId.Value;
            }

            var orders = await repository.GetPurchaseOrdersAsync(effectiveBranchId, status);
            return Results.Ok(orders);
        })
        .RequireAuthorization(policy => policy.RequireRole("inventory_manager", "branch_manager", "super_admin", "system_admin"))
        .WithSummary("List Purchase Orders");

        admin.MapGet("/purchase-orders/{id:guid}", async (
            Guid id,
            IPosRepository repository) =>
        {
            var po = await repository.GetPurchaseOrderByIdAsync(id);
            return po != null ? Results.Ok(po) : Results.NotFound(new { error = $"Purchase order {id} not found." });
        })
        .RequireAuthorization(policy => policy.RequireRole("inventory_manager", "branch_manager", "super_admin", "system_admin"))
        .WithSummary("Get Purchase Order Details");

        admin.MapPost("/purchase-orders", async (
            [FromBody] CreatePurchaseOrderRequest req,
            IPosRepository repository,
            HttpContext context) =>
        {
            if (string.IsNullOrWhiteSpace(req.PoNumber))
            {
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "PO Number is required.");
            }

            var staffId = GetCallerUserId(context);
            var poId = await repository.CreatePurchaseOrderAsync(req, staffId);
            var po = await repository.GetPurchaseOrderByIdAsync(poId);
            return Results.Created($"/api/v1/admin/inventory/purchase-orders/{poId}", po);
        })
        .RequireAuthorization(policy => policy.RequireRole("inventory_manager", "super_admin", "system_admin"))
        .WithSummary("Create Purchase Order");

        admin.MapPatch("/purchase-orders/{id:guid}/status", async (
            Guid id,
            [FromBody] UpdatePurchaseOrderStatusRequest req,
            IPosRepository repository,
            HttpContext context) =>
        {
            if (string.IsNullOrWhiteSpace(req.Status))
            {
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Status is required.");
            }

            var staffId = GetCallerUserId(context);
            var success = await repository.UpdatePurchaseOrderStatusAsync(id, req.Status, staffId);
            if (!success)
            {
                return Results.NotFound(new { error = $"Purchase order {id} not found." });
            }

            var updated = await repository.GetPurchaseOrderByIdAsync(id);
            return Results.Ok(updated);
        })
        .RequireAuthorization(policy => policy.RequireRole("inventory_manager", "branch_manager", "super_admin", "system_admin"))
        .WithSummary("Update Purchase Order Status")
        .WithDescription("Updates PO status. Setting to 'received' automatically updates branch inventory stock.");
    }

    private static (Guid? BranchId, bool IsSuperAdmin, bool IsInventoryManager) GetCallerContext(HttpContext context)
    {
        var isSuperAdmin = context.User.IsInRole("super_admin") || context.User.IsInRole("system_admin");
        var isInventoryManager = context.User.IsInRole("inventory_manager");
        var branchClaim = context.User.FindFirst("branchId")?.Value 
            ?? context.User.FindFirst("branch_id")?.Value;
        Guid? branchId = Guid.TryParse(branchClaim, out var b) ? b : null;
        return (branchId, isSuperAdmin, isInventoryManager);
    }

    private static Guid GetCallerUserId(HttpContext context)
    {
        var idClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? context.User.FindFirst("sub")?.Value;
        return Guid.TryParse(idClaim, out var uid) ? uid : Guid.Empty;
    }
}
