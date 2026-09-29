namespace Pos.Api.Models;

public sealed class ProductDto
{
    public Guid ProductId { get; set; }
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public string? ImageUrl { get; set; }
    public string? BadgeText { get; set; }
    public string? Description { get; set; }
    public string[] DietaryTags { get; set; } = Array.Empty<string>();
    public bool IsComboOnly { get; set; }
    public string MinLoyaltyTier { get; set; } = "none";
    public int StockQuantity { get; set; }
    public bool InStock => StockQuantity > 0;
    public bool IsActive { get; set; }
}

public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}


public sealed class ComboItemDto
{
    public Guid TargetId { get; set; }
    public string ItemType { get; set; } = "";
    public int Quantity { get; set; }
}

public sealed class ComboDto
{
    public Guid ComboId { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public List<ComboItemDto> Items { get; set; } = new();
}

public sealed record OrderResponse(
    Guid OrderId, 
    Guid BranchId, 
    Guid CashierId, 
    Guid? ReservationId, 
    string Status, 
    decimal Subtotal, 
    decimal DiscountAmount, 
    decimal TotalAmount, 
    Guid IdempotencyKey, 
    DateTimeOffset CreatedAt);

public sealed record VoucherDto(Guid VoucherId, string Code, string VoucherType, string TargetItemType, decimal DiscountValue, bool IsRedeemed, DateTimeOffset? ExpiresAt);

public sealed class BranchInventoryDto
{
    public Guid InventoryId { get; set; }
    public Guid BranchId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string Sku { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public string? ImageUrl { get; set; }
    public int StockQuantity { get; set; }
    public int ReorderThreshold { get; set; }
    public bool IsOutOfStock { get; set; }
    public bool NeedsReorder => StockQuantity <= ReorderThreshold;
    public DateTimeOffset? LastRestockedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class InventoryWastageDto
{
    public Guid WastageId { get; set; }
    public Guid BranchId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string Sku { get; set; } = "";
    public int Quantity { get; set; }
    public string Reason { get; set; } = "";
    public decimal CostLoss { get; set; }
    public Guid LoggedBy { get; set; }
    public DateTimeOffset LoggedAt { get; set; }
    public string? Notes { get; set; }
}

public sealed class SupplierDto
{
    public Guid SupplierId { get; set; }
    public string Name { get; set; } = "";
    public string? ContactName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class PurchaseOrderLineDto
{
    public Guid PoLineId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string Sku { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineTotal { get; set; }
}

public sealed class PurchaseOrderDto
{
    public Guid PoId { get; set; }
    public string PoNumber { get; set; } = "";
    public Guid? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public Guid BranchId { get; set; }
    public string Status { get; set; } = "draft";
    public decimal TotalCost { get; set; }
    public DateTimeOffset? OrderedAt { get; set; }
    public DateTimeOffset? ReceivedAt { get; set; }
    public string? Notes { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<PurchaseOrderLineDto> Lines { get; set; } = new();
}