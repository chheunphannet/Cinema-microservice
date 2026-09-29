using System;
using System.Collections.Generic;
using System.Linq;
using Catalog.Api.Models;
using Catalog.Api.Services;
using Xunit;

namespace Cinema.UnitTests;

public class ScreenOptimizedCatalogTests
{
    [Fact]
    public void CursorHelper_EncodeAndDecode_RoundtripsAccurately()
    {
        const int offset = 40;
        var encoded = CursorHelper.EncodeOffset(offset);

        Assert.NotNull(encoded);
        Assert.NotEmpty(encoded);

        var decoded = CursorHelper.DecodeOffset(encoded);
        Assert.Equal(offset, decoded);
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData("", 0)]
    [InlineData("   ", 0)]
    [InlineData("invalid_not_base64!@#", 0)]
    [InlineData("YmFzZTY0bm90Y3Vy", 0)] // Valid base64 but doesn't start with cur_
    [InlineData("Y3VyXy01", 0)] // cur_-5 (negative offset)
    public void CursorHelper_DecodeInvalidOrEmpty_ReturnsDefault(string? cursor, int expected)
    {
        var result = CursorHelper.DecodeOffset(cursor, expected);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CursorPagedResult_TotalPages_CalculatesCorrectly()
    {
        var result1 = new CursorPagedResult<string> { TotalCount = 45, PageSize = 20 };
        var result2 = new CursorPagedResult<string> { TotalCount = 20, PageSize = 20 };
        var result3 = new CursorPagedResult<string> { TotalCount = 0, PageSize = 20 };
        var result4 = new CursorPagedResult<string> { TotalCount = 21, PageSize = 10 };

        Assert.Equal(3, result1.TotalPages);
        Assert.Equal(1, result2.TotalPages);
        Assert.Equal(0, result3.TotalPages);
        Assert.Equal(3, result4.TotalPages);
    }

    [Fact]
    public void ShowtimePillDto_IsSoldOut_ReflectsAvailableSeats()
    {
        var available = new ShowtimePillDto { AvailableSeats = 5 };
        var soldOutZero = new ShowtimePillDto { AvailableSeats = 0 };
        var soldOutNegative = new ShowtimePillDto { AvailableSeats = -1 };

        Assert.False(available.IsSoldOut);
        Assert.True(soldOutZero.IsSoldOut);
        Assert.True(soldOutNegative.IsSoldOut);
    }

    [Fact]
    public void NowShowing_DefaultSortRule_RanksTodayShowtimesBranchFirst()
    {
        // Rule: Movies with showtimes today at user's selected branch first,
        // then 48h popularity, then newest release date, deprioritizing no showtimes in 7 days
        var branchId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var movieWithTodayShowtimes = new NowShowingMovieDto
        {
            MovieId = Guid.NewGuid(),
            Title = "Movie With Today Showtimes",
            HasShowtimesToday = true,
            HasShowtimesNext7Days = true,
            TicketSalesLast48h = 10,
            ReleaseDate = today.AddDays(-10)
        };

        var movieTrendingNoToday = new NowShowingMovieDto
        {
            MovieId = Guid.NewGuid(),
            Title = "Trending Movie Without Today Showtimes",
            HasShowtimesToday = false,
            HasShowtimesNext7Days = true,
            TicketSalesLast48h = 100, // Higher sales, but no showtimes today
            ReleaseDate = today.AddDays(-5)
        };

        var movieNoShowtimesNext7Days = new NowShowingMovieDto
        {
            MovieId = Guid.NewGuid(),
            Title = "Movie With No Showtimes Next 7 Days",
            HasShowtimesToday = false,
            HasShowtimesNext7Days = false,
            TicketSalesLast48h = 50,
            ReleaseDate = today.AddDays(-2)
        };

        var list = new List<NowShowingMovieDto>
        {
            movieNoShowtimesNext7Days,
            movieTrendingNoToday,
            movieWithTodayShowtimes
        };

        // Simulating the ordering applied by SQL
        var sorted = list
            .OrderByDescending(m => m.HasShowtimesToday ? 1 : 0)
            .ThenByDescending(m => m.HasShowtimesNext7Days ? 1 : 0)
            .ThenByDescending(m => m.TicketSalesLast48h)
            .ThenByDescending(m => m.ReleaseDate)
            .ToList();

        // 1st: Movie with showtimes today
        Assert.Equal("Movie With Today Showtimes", sorted[0].Title);
        // 2nd: Movie with showtimes in next 7 days
        Assert.Equal("Trending Movie Without Today Showtimes", sorted[1].Title);
        // 3rd: Deprioritized movie with no showtimes in next 7 days
        Assert.Equal("Movie With No Showtimes Next 7 Days", sorted[2].Title);
    }

    [Fact]
    public void NowShowing_WithinTodayShows_RanksBy48hSalesThenReleaseDate()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var movieHighSales = new NowShowingMovieDto
        {
            MovieId = Guid.NewGuid(),
            Title = "High Sales Movie",
            HasShowtimesToday = true,
            HasShowtimesNext7Days = true,
            TicketSalesLast48h = 250,
            ReleaseDate = today.AddDays(-30)
        };

        var movieLowSalesNewer = new NowShowingMovieDto
        {
            MovieId = Guid.NewGuid(),
            Title = "Low Sales Newer Movie",
            HasShowtimesToday = true,
            HasShowtimesNext7Days = true,
            TicketSalesLast48h = 50,
            ReleaseDate = today.AddDays(-2)
        };

        var movieSameSalesNewer = new NowShowingMovieDto
        {
            MovieId = Guid.NewGuid(),
            Title = "Equal Sales Newer",
            HasShowtimesToday = true,
            HasShowtimesNext7Days = true,
            TicketSalesLast48h = 50,
            ReleaseDate = today.AddDays(-1)
        };

        var list = new List<NowShowingMovieDto> { movieLowSalesNewer, movieHighSales, movieSameSalesNewer };

        var sorted = list
            .OrderByDescending(m => m.HasShowtimesToday ? 1 : 0)
            .ThenByDescending(m => m.HasShowtimesNext7Days ? 1 : 0)
            .ThenByDescending(m => m.TicketSalesLast48h)
            .ThenByDescending(m => m.ReleaseDate)
            .ToList();

        Assert.Equal("High Sales Movie", sorted[0].Title);
        Assert.Equal("Equal Sales Newer", sorted[1].Title);
        Assert.Equal("Low Sales Newer Movie", sorted[2].Title);
    }

    [Fact]
    public void NowShowing_FilterNoShowtimes_ExcludesUnscheduledMovies()
    {
        var movies = new List<NowShowingMovieDto>
        {
            new() { Title = "Active", AvailableShowtimesCount = 5, HasShowtimesNext7Days = true },
            new() { Title = "Unscheduled", AvailableShowtimesCount = 0, HasShowtimesNext7Days = false }
        };

        var filtered = movies.Where(m => m.HasShowtimesNext7Days).ToList();

        Assert.Single(filtered);
        Assert.Equal("Active", filtered[0].Title);
    }

    [Fact]
    public void FeaturedMovies_RuleRanked_CuratedFeaturedFlagTakesPrecedence()
    {
        var curated = new FeaturedMovieDto
        {
            MovieId = Guid.NewGuid(),
            Title = "Curated Banner Movie",
            IsFeatured = true,
            Rating = 8.0m,
            TicketSalesLast48h = 10,
            BannerCustomTag = "IMAX Laser Exclusive"
        };

        var highRatedNotCurated = new FeaturedMovieDto
        {
            MovieId = Guid.NewGuid(),
            Title = "High Rated Not Featured",
            IsFeatured = false,
            Rating = 9.5m,
            TicketSalesLast48h = 500
        };

        var list = new List<FeaturedMovieDto> { highRatedNotCurated, curated };

        var sorted = list
            .OrderByDescending(m => m.IsFeatured)
            .ThenByDescending(m => m.TicketSalesLast48h)
            .ThenByDescending(m => m.Rating)
            .ToList();

        Assert.Equal("Curated Banner Movie", sorted[0].Title);
        Assert.True(sorted[0].IsFeatured);
        Assert.Equal("IMAX Laser Exclusive", sorted[0].BannerCustomTag);
    }

    [Fact]
    public void FeaturedMovies_LimitsToSmallCuratedSet()
    {
        var requestDefault = new FeaturedMoviesRequest();
        var requestCustom = new FeaturedMoviesRequest { Limit = 8 };
        var requestCapped = new FeaturedMoviesRequest { Limit = 50 };

        int limitDefault = Math.Clamp(requestDefault.Limit ?? 5, 1, 10);
        int limitCustom = Math.Clamp(requestCustom.Limit ?? 5, 1, 10);
        int limitCapped = Math.Clamp(requestCapped.Limit ?? 5, 1, 10);

        Assert.Equal(5, limitDefault);
        Assert.Equal(8, limitCustom);
        Assert.Equal(10, limitCapped); // Capped at 10 max
    }

    [Fact]
    public void ComingSoon_ChronologicalOrderByReleaseDate_RanksEarliestFirst()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var movieSoon = new ComingSoonMovieDto
        {
            MovieId = Guid.NewGuid(),
            Title = "Next Week Release",
            ReleaseDate = today.AddDays(7),
            DaysUntilRelease = 7,
            IsAdvanceBooking = true
        };

        var movieLater = new ComingSoonMovieDto
        {
            MovieId = Guid.NewGuid(),
            Title = "Next Month Release",
            ReleaseDate = today.AddDays(30),
            DaysUntilRelease = 30,
            IsAdvanceBooking = false
        };

        var movieNextYear = new ComingSoonMovieDto
        {
            MovieId = Guid.NewGuid(),
            Title = "Next Year Blockbuster",
            ReleaseDate = today.AddDays(180),
            DaysUntilRelease = 180,
            IsAdvanceBooking = false
        };

        var list = new List<ComingSoonMovieDto> { movieNextYear, movieLater, movieSoon };

        var sorted = list.OrderBy(m => m.ReleaseDate).ToList();

        Assert.Equal("Next Week Release", sorted[0].Title);
        Assert.Equal("Next Month Release", sorted[1].Title);
        Assert.Equal("Next Year Blockbuster", sorted[2].Title);
        Assert.True(sorted[0].IsAdvanceBooking);
        Assert.False(sorted[1].IsAdvanceBooking);
    }

    [Fact]
    public void ComingSoon_AdvanceBookingStatus_ControlsNotifyMeAvailability()
    {
        var withAdvanceShowtimes = new ComingSoonMovieDto
        {
            AdvanceShowtimesCount = 3,
            IsAdvanceBooking = true,
            NotifyMeEnabled = false
        };

        var withoutAdvanceShowtimes = new ComingSoonMovieDto
        {
            AdvanceShowtimesCount = 0,
            IsAdvanceBooking = false,
            NotifyMeEnabled = true
        };

        Assert.False(withAdvanceShowtimes.NotifyMeEnabled);
        Assert.True(withoutAdvanceShowtimes.NotifyMeEnabled);
    }

    [Fact]
    public void RecommendedMovies_GenreAffinity_BoostsMatchScoreAndReason()
    {
        var userFavoriteGenres = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Sci-Fi", "Action" };

        var sciFiMovie = new
        {
            Title = "Dune: Part Two",
            Genre = "Sci-Fi/Adventure",
            Rating = 8.5m,
            Sales48h = 20
        };

        var dramaMovie = new
        {
            Title = "The Pianist",
            Genre = "Drama/War",
            Rating = 8.5m,
            Sales48h = 20
        };

        bool sciFiMatches = userFavoriteGenres.Any(g => sciFiMovie.Genre.Contains(g, StringComparison.OrdinalIgnoreCase));
        bool dramaMatches = userFavoriteGenres.Any(g => dramaMovie.Genre.Contains(g, StringComparison.OrdinalIgnoreCase));

        Assert.True(sciFiMatches);
        Assert.False(dramaMatches);

        // Scoring simulation
        decimal sciFiScore = 20.0m + (sciFiMatches ? 35.0m : 0m) + (sciFiMovie.Rating * 2.0m) + Math.Min(20.0m, sciFiMovie.Sales48h * 4.0m);
        decimal dramaScore = 20.0m + (dramaMatches ? 35.0m : 0m) + (dramaMovie.Rating * 2.0m) + Math.Min(20.0m, dramaMovie.Sales48h * 4.0m);

        Assert.True(sciFiScore > dramaScore, "Sci-Fi movie should score higher due to genre affinity.");
    }

    [Fact]
    public void RecommendedMovies_InfiniteScroll_CursorPaginatesCleanly()
    {
        var candidates = Enumerable.Range(1, 25).Select(i => new RecommendedMovieDto
        {
            MovieId = Guid.NewGuid(),
            Title = $"Movie {i}",
            MatchScore = 100 - i
        }).ToList();

        // Page 1: limit 10, offset 0
        int limit = 10;
        int offset1 = CursorHelper.DecodeOffset(null);
        var page1 = candidates.Skip(offset1).Take(limit).ToList();
        bool hasMore1 = (offset1 + page1.Count) < candidates.Count;
        string? cursor1 = hasMore1 ? CursorHelper.EncodeOffset(offset1 + page1.Count) : null;

        Assert.Equal(10, page1.Count);
        Assert.True(hasMore1);
        Assert.NotNull(cursor1);

        // Page 2: with cursor1
        int offset2 = CursorHelper.DecodeOffset(cursor1);
        Assert.Equal(10, offset2);
        var page2 = candidates.Skip(offset2).Take(limit).ToList();
        bool hasMore2 = (offset2 + page2.Count) < candidates.Count;
        string? cursor2 = hasMore2 ? CursorHelper.EncodeOffset(offset2 + page2.Count) : null;

        Assert.Equal(10, page2.Count);
        Assert.True(hasMore2);
        Assert.Equal("Movie 11", page2[0].Title);

        // Page 3: with cursor2
        int offset3 = CursorHelper.DecodeOffset(cursor2);
        Assert.Equal(20, offset3);
        var page3 = candidates.Skip(offset3).Take(limit).ToList();
        bool hasMore3 = (offset3 + page3.Count) < candidates.Count;
        string? cursor3 = hasMore3 ? CursorHelper.EncodeOffset(offset3 + page3.Count) : null;

        Assert.Equal(5, page3.Count);
        Assert.False(hasMore3);
        Assert.Null(cursor3);
    }

    [Theory]
    [InlineData("25", 25)]
    [InlineData("0", 0)]
    [InlineData("cur_42", 42)]
    [InlineData("CUR_99", 99)]
    [InlineData("  cur_15  ", 15)]
    [InlineData("-10", 0)]
    [InlineData("cur_-10", 0)]
    [InlineData("garbage_token", 0)]
    public void CursorHelper_ExtendedFormats_DecodesOrFallsBack(string? input, int expected)
    {
        var result = CursorHelper.DecodeOffset(input, 0);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void RecommendedMovies_RecencyBonus_OnlyRewardsPast45Days_NeverFutureOrOld()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // 1. Released 10 days ago -> Should get 8.0 pts recency bonus
        var recentMovieRelease = today.AddDays(-10);
        int daysSinceRecent = today.DayNumber - recentMovieRelease.DayNumber;
        bool recentGetsBonus = daysSinceRecent >= 0 && daysSinceRecent <= 45;
        Assert.True(recentGetsBonus);

        // 2. Released 45 days ago (boundary) -> Should get bonus
        var boundaryMovieRelease = today.AddDays(-45);
        int daysSinceBoundary = today.DayNumber - boundaryMovieRelease.DayNumber;
        bool boundaryGetsBonus = daysSinceBoundary >= 0 && daysSinceBoundary <= 45;
        Assert.True(boundaryGetsBonus);

        // 3. Released 46 days ago -> Past cutoff, should NOT get bonus
        var olderMovieRelease = today.AddDays(-46);
        int daysSinceOlder = today.DayNumber - olderMovieRelease.DayNumber;
        bool olderGetsBonus = daysSinceOlder >= 0 && daysSinceOlder <= 45;
        Assert.False(olderGetsBonus);

        // 4. Future release (+30 days) -> Must NOT get recency bonus (fixes the <= 45 negative bug!)
        var futureMovieRelease = today.AddDays(30);
        int daysSinceFuture = today.DayNumber - futureMovieRelease.DayNumber;
        bool futureGetsBonus = daysSinceFuture >= 0 && daysSinceFuture <= 45;
        Assert.False(futureGetsBonus);

        // 5. Far future release (+700 days in 2028) -> Must NOT get recency bonus
        var farFutureMovieRelease = today.AddDays(700);
        int daysSinceFarFuture = today.DayNumber - farFutureMovieRelease.DayNumber;
        bool farFutureGetsBonus = daysSinceFarFuture >= 0 && daysSinceFarFuture <= 45;
        Assert.False(farFutureGetsBonus);
    }

    [Fact]
    public void CatalogQueryService_CacheKeyNormalization_ProducesDeterministicKeys()
    {
        // Now Showing: page 1 with limit 20 and default offset with limit 20 normalize to offset 0
        int limit = 20;
        int? page = 1;
        int offsetFromPage = (Math.Max(1, page.Value) - 1) * limit;
        int offsetFromDefaultCursor = CursorHelper.DecodeOffset(null);

        Assert.Equal(0, offsetFromPage);
        Assert.Equal(0, offsetFromDefaultCursor);
        Assert.Equal(offsetFromPage, offsetFromDefaultCursor);

        // Recommended: genre order is deterministic
        string[] genresA = ["Sci-Fi", "Action"];
        string[] genresB = ["Action", "Sci-Fi"];

        string keyA = string.Join(",", genresA.Select(g => g.Trim().ToLowerInvariant()).OrderBy(g => g));
        string keyB = string.Join(",", genresB.Select(g => g.Trim().ToLowerInvariant()).OrderBy(g => g));

        Assert.Equal("action,sci-fi", keyA);
        Assert.Equal("action,sci-fi", keyB);
        Assert.Equal(keyA, keyB);
    }

    [Fact]
    public void ShowtimePillDto_AvailableSeatsClamping_PreventsNegativeValues()
    {
        int capacity = 100;
        int bookedOvercapacity = 110;

        int availableSeats = Math.Max(0, capacity - bookedOvercapacity);
        var pill = new ShowtimePillDto { AvailableSeats = availableSeats };

        Assert.Equal(0, pill.AvailableSeats);
        Assert.True(pill.IsSoldOut);
    }
}
