using System.Text.Json;
using Catalog.Api.Models;
using Catalog.Api.Repositories;
using Microsoft.Extensions.Caching.Distributed;

namespace Catalog.Api.Services;

public class BlockbusterCacheService : IBlockbusterCacheService
{
    private readonly IDistributedCache _cache;
    private readonly ICatalogRepository _repository;
    private readonly ILogger<BlockbusterCacheService> _logger;

    public BlockbusterCacheService(IDistributedCache cache, ICatalogRepository repository, ILogger<BlockbusterCacheService> logger)
    {
        _cache = cache;
        _repository = repository;
        _logger = logger;
    }

    public async Task<object?> GetSeatMatrixAsync(Guid showtimeId)
    {
        var blockbusterKey = $"blockbuster:high-traffic:seat-matrix:{showtimeId}";
        var cached = await _cache.GetStringAsync(blockbusterKey);
        if (!string.IsNullOrEmpty(cached))
        {
            return new
            {
                source = "High-Traffic Cache (Bypassing PostgreSQL to prevent thundering herd)",
                hybridModel = "Twitter celebrity tweet architecture pattern",
                matrix = JsonSerializer.Deserialize<object>(cached)
            };
        }

        var showtime = await _repository.GetShowtimeInfoAsync(showtimeId);
        if (showtime == null)
            return null;

        var precomputed = new
        {
            showtimeId,
            tier = "Blockbuster High-Fan-Out Premiere",
            precomputedAt = DateTimeOffset.UtcNow,
            status = "Ready"
        };

        await _cache.SetStringAsync(blockbusterKey, JsonSerializer.Serialize(precomputed), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(Constants.CacheConstants.BlockbusterExpirationHours)
        });

        return new
        {
            source = "High-Traffic Cache Initialized",
            hybridModel = "Twitter celebrity tweet architecture pattern",
            matrix = precomputed
        };
    }
}
