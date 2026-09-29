namespace Catalog.Api.Services;

public interface IBlockbusterCacheService
{
    Task<object?> GetSeatMatrixAsync(Guid showtimeId);
}
