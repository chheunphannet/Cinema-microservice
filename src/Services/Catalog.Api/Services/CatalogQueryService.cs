using System.Text.Json;
using Catalog.Api.Models;
using Catalog.Api.Repositories;
using Microsoft.Extensions.Caching.Distributed;

namespace Catalog.Api.Services;

public class CatalogQueryService : ICatalogQueryService
{
    private readonly ICatalogRepository _repository;
    private readonly IDistributedCache _cache;
    private readonly ILogger<CatalogQueryService> _logger;

    public CatalogQueryService(ICatalogRepository repository, IDistributedCache cache, ILogger<CatalogQueryService> logger)
    {
        _repository = repository;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IEnumerable<BranchDto>> GetBranchesAsync()
    {
        const string cacheKey = "catalog:branches";
        var cached = await _cache.GetStringAsync(cacheKey);
        if (!string.IsNullOrEmpty(cached))
        {
            return JsonSerializer.Deserialize<List<BranchDto>>(cached)!;
        }

        var branches = await _repository.GetActiveBranchesAsync();

        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(branches), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(Constants.CacheConstants.BranchesExpirationMinutes)
        });

        return branches;
    }

    public async Task<IEnumerable<MovieDto>> GetMoviesAsync()
    {
        const string cacheKey = "catalog:movies";
        var cached = await _cache.GetStringAsync(cacheKey);
        if (!string.IsNullOrEmpty(cached))
        {
            return JsonSerializer.Deserialize<List<MovieDto>>(cached)!;
        }

        var movies = await _repository.GetActiveMoviesAsync();

        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(movies), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(Constants.CacheConstants.MoviesExpirationMinutes)
        });

        return movies;
    }

    public async Task<IEnumerable<ShowtimeDto>> GetShowtimesAsync(Guid? branchId, Guid? movieId)
    {
        string cacheKey = $"catalog:showtimes:{branchId}:{movieId}";
        var cached = await _cache.GetStringAsync(cacheKey);
        if (!string.IsNullOrEmpty(cached))
        {
            return JsonSerializer.Deserialize<List<ShowtimeDto>>(cached)!;
        }

        var showtimes = await _repository.GetShowtimesAsync(branchId, movieId);

        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(showtimes), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(Constants.CacheConstants.ShowtimesExpirationMinutes)
        });

        return showtimes;
    }

    public async Task<object?> GetAuditoriumLayoutAsync(Guid auditoriumId)
    {
        var layout = await _repository.GetAuditoriumLayoutAsync(auditoriumId);
        if (layout == null)
            return null;

        var seatMapObj = JsonSerializer.Deserialize<object>((string)layout.seatmapjson);
        return new
        {
            auditoriumId = (Guid)layout.auditoriumid,
            branchId = (Guid)layout.branchid,
            seatMap = seatMapObj
        };
    }

    public async Task<IEnumerable<TicketTypeDto>> GetTicketTypesAsync()
    {
        const string cacheKey = "catalog:ticket_types";
        var cached = await _cache.GetStringAsync(cacheKey);
        if (!string.IsNullOrEmpty(cached))
        {
            return JsonSerializer.Deserialize<List<TicketTypeDto>>(cached)!;
        }

        var types = await _repository.GetTicketTypesAsync();

        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(types), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
        });

        return types;
    }

    public async Task<MovieDetailsDto?> GetMovieByIdAsync(Guid movieId)
    {
        string cacheKey = $"catalog:movie:{movieId}";
        var cached = await _cache.GetStringAsync(cacheKey);
        if (!string.IsNullOrEmpty(cached))
        {
            return JsonSerializer.Deserialize<MovieDetailsDto>(cached);
        }

        var movie = await _repository.GetMovieByIdAsync(movieId);
        if (movie != null)
        {
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(movie), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(Constants.CacheConstants.MoviesExpirationMinutes)
            });
        }

        return movie;
    }

    public async Task<IEnumerable<ScreenTypeDto>> GetScreenTypesAsync()
    {
        const string cacheKey = "catalog:screen_types";
        var cached = await _cache.GetStringAsync(cacheKey);
        if (!string.IsNullOrEmpty(cached))
        {
            return JsonSerializer.Deserialize<List<ScreenTypeDto>>(cached)!;
        }

        var screens = await _repository.GetScreenTypesAsync();

        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(screens), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
        });

        return screens;
    }

    public async Task<IEnumerable<PromotionDto>> GetPromotionsAsync()
    {
        const string cacheKey = "catalog:promotions:active";
        var cached = await _cache.GetStringAsync(cacheKey);
        if (!string.IsNullOrEmpty(cached))
        {
            return JsonSerializer.Deserialize<List<PromotionDto>>(cached)!;
        }

        var promos = await _repository.GetActivePromotionsAsync();

        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(promos), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
        });

        return promos;
    }

    public async Task<IEnumerable<GlobalNotificationDto>> GetNotificationsAsync()
    {
        const string cacheKey = "catalog:notifications:active";
        var cached = await _cache.GetStringAsync(cacheKey);
        if (!string.IsNullOrEmpty(cached))
        {
            return JsonSerializer.Deserialize<List<GlobalNotificationDto>>(cached)!;
        }

        var notifications = await _repository.GetActiveNotificationsAsync();

        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(notifications), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
        });

        return notifications;
    }

    public async Task<IEnumerable<BranchImageDto>> GetBranchGalleryAsync(Guid branchId)
    {
        string cacheKey = $"catalog:branch_gallery:{branchId}";
        var cached = await _cache.GetStringAsync(cacheKey);
        if (!string.IsNullOrEmpty(cached))
        {
            return JsonSerializer.Deserialize<List<BranchImageDto>>(cached)!;
        }

        var images = await _repository.GetBranchGalleryAsync(branchId);

        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(images), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(Constants.CacheConstants.BranchesExpirationMinutes)
        });

        return images;
    }

    public async Task<IEnumerable<MovieAutocompleteDto>> AutocompleteMoviesAsync(string query, int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Enumerable.Empty<MovieAutocompleteDto>();

        string normalized = query.Trim().ToLowerInvariant();
        string cacheKey = $"catalog:autocomplete:{normalized}:{limit}";
        var cached = await _cache.GetStringAsync(cacheKey);
        if (!string.IsNullOrEmpty(cached))
        {
            return JsonSerializer.Deserialize<List<MovieAutocompleteDto>>(cached)!;
        }

        var results = (await _repository.AutocompleteMoviesAsync(query, limit)).ToList();

        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(results), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
        });

        return results;
    }

    public async Task<PagedResult<MovieSearchResultDto>> SearchMoviesAsync(MovieSearchRequest request)
    {
        var rawKey = $"{request.Q}|{string.Join(",", request.Genres ?? Array.Empty<string>())}|{request.AudioLanguage}|{request.SubtitleLanguage}|{string.Join(",", request.Formats ?? Array.Empty<string>())}|{request.ReleaseStatus}|{request.BranchId}|{request.City}|{request.Date}|{request.TimeSlot}|{request.AgeRating}|{request.DurationBucket}|{request.SortBy}|{request.Page}|{request.PageSize}";
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawKey)));
        string cacheKey = $"catalog:search:{hash}";

        var cached = await _cache.GetStringAsync(cacheKey);
        if (!string.IsNullOrEmpty(cached))
        {
            return JsonSerializer.Deserialize<PagedResult<MovieSearchResultDto>>(cached)!;
        }

        var result = await _repository.SearchMoviesAsync(request);

        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(result), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60)
        });

        return result;
    }

    public async Task<CursorPagedResult<NowShowingMovieDto>> GetNowShowingMoviesAsync(NowShowingMoviesRequest request)
    {
        int limit = Math.Clamp(request.Limit ?? 20, 1, 50);
        int offset = request.Page.HasValue
            ? (Math.Max(1, request.Page.Value) - 1) * limit
            : CursorHelper.DecodeOffset(request.Cursor);

        string branchKey = request.BranchId?.ToString("N") ?? "all";
        string cacheKey = $"catalog:movies:now-showing:{branchKey}:{request.FilterNoShowtimes ?? false}:{offset}:{limit}";
        var cached = await _cache.GetStringAsync(cacheKey);
        if (!string.IsNullOrEmpty(cached))
        {
            return JsonSerializer.Deserialize<CursorPagedResult<NowShowingMovieDto>>(cached)!;
        }

        var result = await _repository.GetNowShowingMoviesAsync(request);

        // Jittered TTL (110 - 130 seconds ~ 2 minutes) to prevent cache stampedes
        int jitterSeconds = Random.Shared.Next(110, 131);
        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(result), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(jitterSeconds)
        });

        return result;
    }

    public async Task<IReadOnlyList<FeaturedMovieDto>> GetFeaturedMoviesAsync(FeaturedMoviesRequest request)
    {
        int limit = Math.Clamp(request.Limit ?? 5, 1, 10);
        string branchKey = request.BranchId?.ToString("N") ?? "all";
        string cacheKey = $"catalog:movies:featured:{branchKey}:{limit}";
        var cached = await _cache.GetStringAsync(cacheKey);
        if (!string.IsNullOrEmpty(cached))
        {
            return JsonSerializer.Deserialize<List<FeaturedMovieDto>>(cached)!;
        }

        var result = await _repository.GetFeaturedMoviesAsync(request);

        // Jittered TTL (280 - 320 seconds ~ 5 minutes)
        int jitterSeconds = Random.Shared.Next(280, 321);
        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(result), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(jitterSeconds)
        });

        return result;
    }

    public async Task<CursorPagedResult<ComingSoonMovieDto>> GetComingSoonMoviesAsync(ComingSoonMoviesRequest request)
    {
        int limit = Math.Clamp(request.Limit ?? 20, 1, 50);
        int offset = request.Page.HasValue
            ? (Math.Max(1, request.Page.Value) - 1) * limit
            : CursorHelper.DecodeOffset(request.Cursor);

        string branchKey = request.BranchId?.ToString("N") ?? "all";
        string cacheKey = $"catalog:movies:coming-soon:{branchKey}:{offset}:{limit}";
        var cached = await _cache.GetStringAsync(cacheKey);
        if (!string.IsNullOrEmpty(cached))
        {
            return JsonSerializer.Deserialize<CursorPagedResult<ComingSoonMovieDto>>(cached)!;
        }

        var result = await _repository.GetComingSoonMoviesAsync(request);

        // Jittered TTL (570 - 630 seconds ~ 10 minutes)
        int jitterSeconds = Random.Shared.Next(570, 631);
        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(result), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(jitterSeconds)
        });

        return result;
    }

    public async Task<CursorPagedResult<RecommendedMovieDto>> GetRecommendedMoviesAsync(RecommendedMoviesRequest request)
    {
        int limit = Math.Clamp(request.Limit ?? 10, 1, 50);
        int offset = CursorHelper.DecodeOffset(request.Cursor);

        string genresKey = request.Genres != null && request.Genres.Length > 0
            ? string.Join(",", request.Genres.Where(g => !string.IsNullOrWhiteSpace(g)).Select(g => g.Trim().ToLowerInvariant()).OrderBy(g => g))
            : "none";

        string userKey = request.UserId?.ToString("N") ?? "anon";
        string branchKey = request.BranchId?.ToString("N") ?? "all";
        string cacheKey = $"catalog:movies:recommended:{userKey}:{genresKey}:{branchKey}:{offset}:{limit}";

        var cached = await _cache.GetStringAsync(cacheKey);
        if (!string.IsNullOrEmpty(cached))
        {
            return JsonSerializer.Deserialize<CursorPagedResult<RecommendedMovieDto>>(cached)!;
        }

        var result = await _repository.GetRecommendedMoviesAsync(request);

        // Jittered TTL (110 - 130 seconds ~ 2 minutes)
        int jitterSeconds = Random.Shared.Next(110, 131);
        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(result), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(jitterSeconds)
        });

        return result;
    }
}

