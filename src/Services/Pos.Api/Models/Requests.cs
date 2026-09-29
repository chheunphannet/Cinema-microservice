namespace Pos.Api.Models;

public sealed record PaymentWebhookPayload(string IdempotencyKey, Guid OrderId, decimal Amount, string? TransactionReference, string? Provider);
public sealed record OpenShiftRequest(Guid BranchId, Guid CashierId, string TerminalCode, decimal OpeningFloat);
public sealed record CloseShiftRequest(Guid ShiftId, decimal ClosingCash);
public sealed record OrderLineRequest(Guid? ProductId = null, string? Description = null, int Quantity = 1, decimal UnitPrice = 0, decimal DiscountAmount = 0);
public sealed record CreateOrderRequest(Guid BranchId, Guid CashierId, Guid? ReservationId, decimal DiscountAmount, List<OrderLineRequest> Lines, string? CustomerEmail = null, string? VoucherCode = null);
public sealed record ProcessPaymentRequest(string Method, decimal Amount, decimal? TenderedAmount, string? ProviderReference);
public sealed record GuestCheckoutRequest(
    Guid ReservationId,
    Guid? BranchId = null,
    string GuestEmail = "",
    string? GuestPhone = null,
    string? GuestName = null,
    string PaymentMethod = "card",
    decimal Amount = 0,
    string? ProviderReference = null,
    Guid? IdempotencyKey = null,
    List<Guid>? SeatIds = null,
    string? FoodAndBeverage = null,
    List<OrderLineRequest>? Concessions = null,
    Guid? CustomerId = null
);

public sealed record ScanConcessionRequest(string BarcodeOrToken, bool? AutoFulfill = false, Guid? StaffId = null);

public sealed class ProductSearchRequest
{
    public string? Q { get; set; }
    public string[]? Categories { get; set; }
    public string[]? Dietary { get; set; }
    public Guid? BranchId { get; set; }
    public decimal? PriceMin { get; set; }
    public decimal? PriceMax { get; set; }
    public bool? PromoOnly { get; set; }
    public string? LoyaltyTier { get; set; } // "guest", "bronze", "silver", "gold", "platinum"
    public int? Page { get; set; } = 1;
    public int? PageSize { get; set; } = 20;
}

public sealed class StockAdjustRequest
{
    public int QuantityDelta { get; set; }
    public string Reason { get; set; } = "restock"; // "restock", "count_adjustment", "return", "transfer"
}

public sealed class LogWastageRequest
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public string Reason { get; set; } = "damaged"; // "damaged", "expired", "dropped", "spoilage", "theft", "other"
    public decimal UnitCost { get; set; } = 0;
    public string? Notes { get; set; }
}

public sealed class ToggleAvailabilityRequest
{
    public bool IsOutOfStock { get; set; }
}

public sealed class CreateSupplierRequest
{
    public string Name { get; set; } = "";
    public string? ContactName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
}

public sealed class PurchaseOrderLineInput
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
}

public sealed class CreatePurchaseOrderRequest
{
    public string PoNumber { get; set; } = "";
    public Guid? SupplierId { get; set; }
    public Guid BranchId { get; set; }
    public string? Notes { get; set; }
    public List<PurchaseOrderLineInput> Lines { get; set; } = new();
}

public sealed class UpdatePurchaseOrderStatusRequest
{
    public string Status { get; set; } = ""; // "draft", "ordered", "received", "cancelled"
}

public sealed class CreateProductRequest
{
    public Guid? BranchId { get; set; }
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public string? ImageUrl { get; set; }
    public int? InitialStock { get; set; }
    public int? ReorderThreshold { get; set; }
}

public sealed class UpdateProductRequest
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; }
}

public sealed class CreateComboRequest
{
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
}
