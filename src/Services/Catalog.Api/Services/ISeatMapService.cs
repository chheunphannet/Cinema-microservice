using Catalog.Api.Models;

namespace Catalog.Api.Services;

public interface ISeatMapService
{
    Task<SeatMapResponse?> GetSeatMapAsync(Guid showtimeId);
    Task<object?> GetRedisSeatMatrixAsync(Guid showtimeId);
}
