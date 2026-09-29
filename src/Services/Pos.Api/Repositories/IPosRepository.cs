using System.Data.Common;
using Pos.Api.Models;

namespace Pos.Api.Repositories;

public interface IPosRepository
{
    Task<IEnumerable<ProductDto>> GetActiveProductsAsync(Guid? branchId);
    Task<List<ComboDto>> GetActiveCombosAsync();
    Task<List<ProductDto>> GetProductsByIdsAsync(IEnumerable<Guid> productIds, DbConnection conn, DbTransaction? tx = null);
    
    Task<Guid> CreateProductAsync(CreateProductRequest req);
    Task<bool> UpdateProductAsync(Guid productId, UpdateProductRequest req);
    Task<bool> DeleteProductAsync(Guid productId);
    Task<Guid> CreateComboAsync(CreateComboRequest req);
    
    Task CreateTillShiftAsync(Guid shiftId, Guid branchId, Guid cashierId, string terminalCode, decimal openingFloat);
    Task<dynamic?> CloseTillShiftAsync(Guid shiftId, decimal closingCash);
    
    Task<dynamic?> GetOrderByIdempotencyKeyAsync(Guid idempotencyKey);
    Task InsertOrderAsync(Guid orderId, Guid branchId, Guid? cashierId, Guid? reservationId, decimal subtotal, decimal discount, decimal total, Guid idempotencyKey, string? customerEmail, string? voucherCode, DbConnection conn, DbTransaction tx);
    Task InsertOrderLineAsync(Guid orderId, OrderLineRequest line, DbConnection conn, DbTransaction tx);
    
    Task<dynamic?> GetOrderByIdAsync(Guid orderId, DbConnection conn, DbTransaction? tx = null);
    Task InsertPaymentAsync(Guid paymentId, Guid orderId, string method, decimal amount, string? providerRef, DbConnection conn, DbTransaction tx);
    Task InsertOutboxMessageAsync(string routingKey, object payload, DbConnection conn, DbTransaction tx);
    Task<decimal> GetTotalCapturedPaymentsAsync(Guid orderId, DbConnection conn, DbTransaction tx);
    Task UpdateOrderStatusToPaidAsync(Guid orderId, DbConnection conn, DbTransaction tx);
    Task UpdateOrderStatusToCancelledAsync(Guid orderId, DbConnection conn, DbTransaction tx);
    
    Task EnsureWalkInOrderExistsAsync(Guid orderId, decimal amount, DbConnection conn, DbTransaction tx);
    
    Task<int> InsertGuestOrderAsync(Guid orderId, Guid branchId, Guid reservationId, decimal totalAmount, Guid idempotencyKey, string customerEmail, string? customerPhone, string bookingReference, Guid? customerId, DbConnection conn, DbTransaction tx);
    Task<dynamic?> GetReservationForGuestCheckoutAsync(Guid reservationId, DbConnection conn, DbTransaction? tx = null);
    Task<List<dynamic>> GetReservationSeatsAsync(Guid reservationId, DbConnection conn, DbTransaction? tx = null);
    Task ConfirmReservationAndSeatsAsync(Guid reservationId, Guid showtimeId, IEnumerable<Guid> seatIds, string? guestEmail, string? guestPhone, string? guestName, DbConnection conn, DbTransaction tx);
    Task<List<dynamic>> CreateTicketsAndReturnDetailsAsync(Guid reservationId, Guid showtimeId, IEnumerable<Guid> seatIds, string guestEmail, DbConnection conn, DbTransaction tx);
    Task<List<dynamic>> GetOrderLinesByOrderIdAsync(Guid orderId);
    Task<List<dynamic>> FulfillOrderConcessionsAsync(Guid orderId, Guid? staffId = null);
    Task<Guid?> FindOrderIdByReferenceOrTokenAsync(string tokenOrRef);
    Task<PagedResult<ProductDto>> SearchProductsAsync(ProductSearchRequest request);
    Task<IEnumerable<ProductDto>> GetUpsellProductsAsync(Guid? branchId, IEnumerable<Guid> cartProductIds);

    // Milestone 5.3: Multi-Branch Stock Tracking & Suppliers
    Task<IEnumerable<BranchInventoryDto>> GetBranchInventoryAsync(Guid branchId);
    Task<BranchInventoryDto?> GetProductBranchInventoryAsync(Guid branchId, Guid productId);
    Task<bool> AdjustStockAsync(Guid branchId, Guid productId, int quantityDelta, string reason, Guid staffId);
    Task<bool> ToggleProductAvailabilityAsync(Guid branchId, Guid productId, bool isOutOfStock);
    Task<Guid> LogWastageAsync(Guid branchId, LogWastageRequest req, Guid staffId);
    Task<IEnumerable<InventoryWastageDto>> GetInventoryWastageAsync(Guid branchId, DateTimeOffset? fromDate, DateTimeOffset? toDate);
    Task<IEnumerable<BranchInventoryDto>> GetReorderAlertsAsync(Guid? branchId);
    Task<IEnumerable<SupplierDto>> GetSuppliersAsync();
    Task<SupplierDto?> GetSupplierByIdAsync(Guid supplierId);
    Task<Guid> CreateSupplierAsync(CreateSupplierRequest req);
    Task<IEnumerable<PurchaseOrderDto>> GetPurchaseOrdersAsync(Guid? branchId, string? status);
    Task<PurchaseOrderDto?> GetPurchaseOrderByIdAsync(Guid poId);
    Task<Guid> CreatePurchaseOrderAsync(CreatePurchaseOrderRequest req, Guid createdBy);
    Task<bool> UpdatePurchaseOrderStatusAsync(Guid poId, string status, Guid staffId);

    // Milestone 5.4: Refunds & Disputes
    Task<Guid> RecordRefundAsync(Guid? orderId, Guid? reservationId, decimal refundAmount, string reasonCode, string? notes, Guid authorizedBy);
    Task<IEnumerable<dynamic>> GetRefundsByOrderAsync(Guid orderId);
    Task<Guid> RecordDisputeAsync(Guid? orderId, Guid? reservationId, string? providerDisputeId, string status, decimal amount, string? evidenceNotes);
    Task<IEnumerable<dynamic>> GetDisputesByOrderAsync(Guid orderId);
}





