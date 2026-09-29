using System.Text.Json;
using Catalog.Api.Models;
using Catalog.Api.Repositories;
using Cinema.Foundation.Redis;
using Microsoft.Extensions.Caching.Distributed;

namespace Catalog.Api.Services;

public class SeatMapService : ISeatMapService
{
    private readonly ICatalogRepository _repository;
    private readonly ISeatLockService _lockService;
    private readonly IDistributedCache _cache;
    private readonly ILogger<SeatMapService> _logger;
    private readonly IConfiguration _configuration;

    public SeatMapService(
        ICatalogRepository repository,
        ISeatLockService lockService,
        IDistributedCache cache,
        ILogger<SeatMapService> logger,
        IConfiguration configuration)
    {
        _repository = repository;
        _lockService = lockService;
        _cache = cache;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<SeatMapResponse?> GetSeatMapAsync(Guid showtimeId)
    {
        var showtime = await _repository.GetShowtimeInfoAsync(showtimeId);
        if (showtime == null)
            return null;

        var rawSeats = await _repository.GetSeatsByAuditoriumAsync(showtime.AuditoriumId);
        var bookedSeatIds = await _repository.GetBookedSeatIdsAsync(showtimeId);
        var blockedSeatIds = await _repository.GetBlockedSeatIdsAsync(showtimeId, showtime.AuditoriumId);
        
        var matrix = await _repository.GetPricingMatrixAsync(showtime.PriceCardId);
        var seatDtos = new List<SeatDto>();

        foreach (var s in rawSeats)
        {
            // We set default Price to 0 here because actual price depends on TicketType selected by user.
            // Client uses the PricingMatrix to calculate exact price based on TicketTypeId + SeatType.
            decimal defaultPrice = 0m; 

            string status = Constants.SeatStatuses.Available;
            if (bookedSeatIds.Contains(s.SeatId))
            {
                status = Constants.SeatStatuses.Booked;
            }
            else if (blockedSeatIds.Contains(s.SeatId))
            {
                status = Constants.SeatStatuses.Blocked;
            }
            else
            {
                var holdVal = await _lockService.GetSeatHoldAsync(showtimeId, s.SeatId);
                if (!string.IsNullOrEmpty(holdVal))
                {
                    status = Constants.SeatStatuses.Held;
                }
            }

            seatDtos.Add(new SeatDto(s.SeatId, s.RowLabel, s.SeatNumber, s.SeatType, defaultPrice, status));
        }

        return new SeatMapResponse(showtimeId, showtime.AuditoriumName, showtime.Capacity, seatDtos, matrix.ToList());
    }

    public async Task<object?> GetRedisSeatMatrixAsync(Guid showtimeId)
    {
        var cacheKey = $"catalog:seat-matrix:{showtimeId}";
        var cachedJson = await _cache.GetStringAsync(cacheKey);
        if (!string.IsNullOrEmpty(cachedJson))
        {
            return JsonSerializer.Deserialize<object>(cachedJson);
        }

        var showtime = await _repository.GetShowtimeInfoAsync(showtimeId);
        if (showtime == null)
            return null;

        var rawSeats = await _repository.GetSeatsByAuditoriumAsync(showtime.AuditoriumId);
        var bookedSeatIds = await _repository.GetBookedSeatIdsAsync(showtimeId);
        var blockedSeatIds = await _repository.GetBlockedSeatIdsAsync(showtimeId, showtime.AuditoriumId);

        var matrixSeats = new List<object>();

        // NOTE: Hardcoded test/debug leftover — version counter starting at 100
        int versionCounter = 100;

        foreach (var s in rawSeats)
        {
            versionCounter++;
            string seatLabel = $"{s.RowLabel}{s.SeatNumber}";
            if (bookedSeatIds.Contains(s.SeatId))
            {
                matrixSeats.Add(new { seatId = seatLabel, status = Constants.SeatStatuses.MatrixBooked, version = versionCounter });
            }
            else if (blockedSeatIds.Contains(s.SeatId))
            {
                matrixSeats.Add(new { seatId = seatLabel, status = Constants.SeatStatuses.MatrixLocked, owner = "blocked", version = versionCounter });
            }
            else
            {
                var holdVal = await _lockService.GetSeatHoldAsync(showtimeId, s.SeatId);
                if (!string.IsNullOrEmpty(holdVal))
                {
                    var parts = holdVal.Split(':');
                    var owner = parts.Length > 0 ? parts[0] : "held_user";
                    matrixSeats.Add(new { seatId = seatLabel, status = Constants.SeatStatuses.MatrixLocked, owner, version = versionCounter });
                }
                else
                {
                    matrixSeats.Add(new { seatId = seatLabel, status = Constants.SeatStatuses.MatrixAvailable, version = versionCounter });
                }
            }
        }

        // Phase 2 Module 1.3: Randomized Jittered TTL (540s - 660s) to prevent Cache Stampedes
        int jitterSeconds = 600 + Random.Shared.Next(-60, 61);
        var matrixPayload = new
        {
            showtimeId = $"SHW-{showtimeId.ToString("N")[..6].ToUpperInvariant()}",
            auditoriumId = $"AUD-{showtime.AuditoriumId.ToString("N")[..4].ToUpperInvariant()}",
            seats = matrixSeats,
            ttl = jitterSeconds
        };

        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(matrixPayload), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(jitterSeconds)
        });

        return matrixPayload;
    }
}
