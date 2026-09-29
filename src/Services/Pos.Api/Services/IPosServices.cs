using Microsoft.AspNetCore.Http;
using Pos.Api.Models;

namespace Pos.Api.Services;

public interface IOrderService
{
    Task<IResult> CreateOrderAsync(CreateOrderRequest request, Guid? idempotencyKeyHeader);
    Task<IResult> CancelOrderAsync(Guid orderId);
}

public interface IPaymentService
{
    Task<IResult> ProcessPaymentAsync(Guid orderId, ProcessPaymentRequest request);
    Task<IResult> ProcessGuestCheckoutAsync(GuestCheckoutRequest request, Guid? idempotencyKeyHeader);
}

public interface IWebhookIdempotencyService
{
    Task<IResult> HandleWebhookAsync(PaymentWebhookPayload? payload);
}

public interface IShiftService
{
    Task<IResult> OpenShiftAsync(OpenShiftRequest request);
    Task<IResult> CloseShiftAsync(CloseShiftRequest request);
}

public interface IProductService
{
    Task<IResult> GetProductsAsync(Guid? branchId);
    Task<IResult> SearchProductsAsync(ProductSearchRequest request);
    Task<IResult> GetUpsellProductsAsync(Guid? branchId, IEnumerable<Guid> cartProductIds);
}

