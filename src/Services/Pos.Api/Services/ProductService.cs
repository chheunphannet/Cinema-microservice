using Microsoft.AspNetCore.Http;
using Pos.Api.Repositories;

namespace Pos.Api.Services;

public class ProductService : IProductService
{
    private readonly IPosRepository _repository;

    public ProductService(IPosRepository repository)
    {
        _repository = repository;
    }

    public async Task<IResult> GetProductsAsync(Guid? branchId)
    {
        var products = await _repository.GetActiveProductsAsync(branchId);
        return Results.Ok(products);
    }

    public async Task<IResult> SearchProductsAsync(Models.ProductSearchRequest request)
    {
        var results = await _repository.SearchProductsAsync(request);
        return Results.Ok(results);
    }

    public async Task<IResult> GetUpsellProductsAsync(Guid? branchId, IEnumerable<Guid> cartProductIds)
    {
        var upsells = await _repository.GetUpsellProductsAsync(branchId, cartProductIds);
        return Results.Ok(upsells);
    }
}

