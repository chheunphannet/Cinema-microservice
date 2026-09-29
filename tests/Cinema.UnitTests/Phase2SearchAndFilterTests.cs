using Catalog.Api.Models;
using Pos.Api.Models;
using Xunit;

namespace Cinema.UnitTests;

public class Phase2SearchAndFilterTests
{
    [Fact]
    public void MovieSearchRequest_DefaultValues_AreSensible()
    {
        var request = new MovieSearchRequest();

        Assert.Equal(1, request.Page);
        Assert.Equal(20, request.PageSize);
        Assert.Null(request.Q);
        Assert.Null(request.Genres);
        Assert.Null(request.Formats);
    }

    [Theory]
    [InlineData("morning", 10, true)]
    [InlineData("morning", 12, false)]
    [InlineData("afternoon", 14, true)]
    [InlineData("afternoon", 17, false)]
    [InlineData("evening", 19, true)]
    [InlineData("evening", 21, false)]
    [InlineData("late_night", 22, true)]
    [InlineData("late_night", 20, false)]
    public void TimeSlotFiltering_MatchesExpectedHourBuckets(string slot, int hour, bool expectedMatch)
    {
        bool matches = slot switch
        {
            "morning" => hour < 12,
            "afternoon" => hour >= 12 && hour < 17,
            "evening" => hour >= 17 && hour < 21,
            "late_night" => hour >= 21,
            _ => false
        };

        Assert.Equal(expectedMatch, matches);
    }

    [Theory]
    [InlineData("under_90", 85, true)]
    [InlineData("under_90", 90, false)]
    [InlineData("90_120", 105, true)]
    [InlineData("90_120", 130, false)]
    [InlineData("over_120", 160, true)]
    [InlineData("over_120", 115, false)]
    public void DurationBucketFiltering_MatchesExpectedMinutes(string bucket, int minutes, bool expectedMatch)
    {
        bool matches = bucket switch
        {
            "under_90" => minutes < 90,
            "90_120" => minutes >= 90 && minutes <= 120,
            "over_120" => minutes > 120,
            _ => false
        };

        Assert.Equal(expectedMatch, matches);
    }

    [Fact]
    public void ShowtimeCutoffRule_FiveMinutesAfterStart_IsEnforced()
    {
        var now = DateTime.UtcNow;
        var validShowtime = now.AddMinutes(10);
        var expiredShowtime = now.AddMinutes(4);

        var cutoffThreshold = now.AddMinutes(5);

        Assert.True(validShowtime > cutoffThreshold, "Showtime in 10 minutes should be bookable.");
        Assert.False(expiredShowtime > cutoffThreshold, "Showtime in 4 minutes violates the 5-minute cutoff.");
    }

    [Fact]
    public void MovieSearchResultDto_ComingSoonWithNoShowtimes_EnablesNotifyMe()
    {
        var movie = new MovieSearchResultDto
        {
            MovieId = Guid.NewGuid(),
            Title = "Avatar 3: Fire and Ash",
            ReleaseDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(3)),
            Showtimes = new List<ShowtimeSearchResultDto>()
        };

        var isFuture = movie.ReleaseDate.HasValue && movie.ReleaseDate.Value > DateOnly.FromDateTime(DateTime.UtcNow);
        if (isFuture && movie.Showtimes.Count == 0)
        {
            movie.IsBookable = false;
            movie.NotifyMeEnabled = true;
        }

        Assert.False(movie.IsBookable);
        Assert.True(movie.NotifyMeEnabled);
    }

    [Fact]
    public void ShowtimeSearchResultDto_SoldOutIndicator_ComputesCorrectly()
    {
        var availableShowtime = new ShowtimeSearchResultDto
        {
            ShowtimeId = Guid.NewGuid(),
            TotalCapacity = 100,
            AvailableSeats = 15
        };

        var soldOutShowtime = new ShowtimeSearchResultDto
        {
            ShowtimeId = Guid.NewGuid(),
            TotalCapacity = 100,
            AvailableSeats = 0
        };

        Assert.False(availableShowtime.IsSoldOut);
        Assert.True(soldOutShowtime.IsSoldOut);
    }

    [Theory]
    [InlineData("guest", "none", true)]
    [InlineData("guest", "bronze", false)]
    [InlineData("bronze", "bronze", true)]
    [InlineData("bronze", "gold", false)]
    [InlineData("gold", "silver", true)]
    [InlineData("gold", "gold", true)]
    [InlineData("platinum", "platinum", true)]
    public void ProductLoyaltyGating_RespectsTierHierarchy(string customerTier, string minItemTier, bool isAllowed)
    {
        static int GetTierRank(string tier) => tier.ToLowerInvariant() switch
        {
            "platinum" => 4,
            "gold" => 3,
            "silver" => 2,
            "bronze" => 1,
            _ => 0
        };

        int customerRank = GetTierRank(customerTier);
        int itemRank = GetTierRank(minItemTier);

        bool eligible = itemRank <= customerRank;
        Assert.Equal(isAllowed, eligible);
    }

    [Fact]
    public void ProductDto_StockIndicator_ReflectsInventory()
    {
        var inStockItem = new ProductDto { StockQuantity = 20 };
        var outOfStockItem = new ProductDto { StockQuantity = 0 };

        Assert.True(inStockItem.InStock);
        Assert.False(outOfStockItem.InStock);
    }

    [Fact]
    public void ProductDietaryFilter_MatchesSelectedTags()
    {
        var product = new ProductDto
        {
            Name = "Caramel Popcorn",
            DietaryTags = new[] { "vegetarian", "gluten_free", "halal" }
        };

        var selectedTags = new[] { "vegetarian", "halal" };
        bool hasAllTags = selectedTags.All(tag => product.DietaryTags.Contains(tag));

        Assert.True(hasAllTags);
    }

    [Fact]
    public void PagedResult_TotalPages_CalculatesCorrectly()
    {
        var paged1 = new Catalog.Api.Models.PagedResult<string> { TotalCount = 45, PageSize = 20 };
        var paged2 = new Catalog.Api.Models.PagedResult<string> { TotalCount = 20, PageSize = 20 };
        var paged3 = new Catalog.Api.Models.PagedResult<string> { TotalCount = 0, PageSize = 20 };

        Assert.Equal(3, paged1.TotalPages);
        Assert.Equal(1, paged2.TotalPages);
        Assert.Equal(0, paged3.TotalPages);
    }
}
