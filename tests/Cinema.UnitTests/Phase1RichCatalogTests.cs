using System;
using System.Collections.Generic;
using Catalog.Api.Models;
using Pos.Api.Models;
using Xunit;

namespace Cinema.UnitTests;

public class Phase1RichCatalogTests
{
    [Fact]
    public void MovieDetailsDto_ShouldDefaultAudioToKhmerAndSubtitleToEnglish()
    {
        var movie = new MovieDetailsDto
        {
            MovieId = Guid.NewGuid(),
            Title = "Avatar: The Way of Water",
            DurationMinutes = 192,
            Genre = "Sci-Fi/Action",
            Classification = "PG-13",
            Rating = 8.5m,
            TrailerUrl = "https://youtube.com/watch?v=123",
            PosterUrl = "https://assets.cinema.local/avatar2.jpg"
        };

        Assert.Equal("Khmer", movie.AudioLanguage);
        Assert.Equal("English", movie.SubtitleLanguage);
        Assert.True(movie.DurationMinutes > 0);
        Assert.NotNull(movie.TrailerUrl);
    }

    [Theory]
    [InlineData("Khmer", "English")]
    [InlineData("English", "Khmer & English")]
    [InlineData("Thai", "Khmer")]
    public void MovieDetailsDto_ShouldSupportCustomLanguagePairings(string audio, string subtitle)
    {
        var movie = new MovieDetailsDto
        {
            MovieId = Guid.NewGuid(),
            Title = "Localized Movie",
            AudioLanguage = audio,
            SubtitleLanguage = subtitle
        };

        Assert.Equal(audio, movie.AudioLanguage);
        Assert.Equal(subtitle, movie.SubtitleLanguage);
    }

    [Theory]
    [InlineData("IMAX", "IMAX with Laser", "https://assets.cinema.local/imax.svg")]
    [InlineData("4DX", "4DX Experience", "https://assets.cinema.local/4dx.svg")]
    [InlineData("SCREENX", "ScreenX 270°", "https://assets.cinema.local/screenx.svg")]
    [InlineData("ATMOS", "Dolby Atmos Sound", "https://assets.cinema.local/atmos.svg")]
    public void ScreenTypeDto_ShouldContainCodeNameAndLogoUrl(string code, string name, string logoUrl)
    {
        var screen = new ScreenTypeDto
        {
            ScreenTypeId = Guid.NewGuid(),
            Code = code,
            Name = name,
            LogoUrl = logoUrl,
            IsActive = true
        };

        Assert.Equal(code, screen.Code);
        Assert.Equal(name, screen.Name);
        Assert.Equal(logoUrl, screen.LogoUrl);
        Assert.True(screen.IsActive);
    }

    [Fact]
    public void PromotionDto_ShouldValidateActiveTimeWindow()
    {
        var now = DateTime.UtcNow;
        var activePromo = new PromotionDto
        {
            PromotionId = Guid.NewGuid(),
            Title = "Wednesday Popcorn Deal",
            PosterUrl = "https://assets.cinema.local/promo.jpg",
            ContentText = "50% off caramel popcorn",
            DiscountType = "percentage",
            DiscountValue = 50.0m,
            PromoCode = "POPWED50",
            StartsAt = now.AddDays(-1),
            EndsAt = now.AddDays(7),
            IsActive = true
        };

        bool isCurrentlyActive = activePromo.IsActive && now >= activePromo.StartsAt && now <= activePromo.EndsAt;
        Assert.True(isCurrentlyActive);
    }

    [Fact]
    public void GlobalNotificationDto_ShouldSupportVariousAlertTypes()
    {
        var notification = new GlobalNotificationDto
        {
            NotificationId = Guid.NewGuid(),
            Title = "Scheduled Maintenance",
            Message = "Online ticketing will be offline from 2AM to 4AM UTC.",
            Type = "warning",
            ActionUrl = "/maintenance-info",
            StartsAt = DateTime.UtcNow.AddHours(-1),
            ExpiresAt = DateTime.UtcNow.AddHours(2),
            IsActive = true
        };

        Assert.Equal("warning", notification.Type);
        Assert.NotNull(notification.ActionUrl);
        Assert.True(notification.ExpiresAt > notification.StartsAt);
    }

    [Fact]
    public void ProductDto_WithBadgeAndImageUrl_ShouldRetainProperties()
    {
        var product = new ProductDto
        {
            ProductId = Guid.NewGuid(),
            Sku = "SKU-POPCORN-XL",
            Name = "Jumbo Butter Popcorn",
            Category = "Concessions",
            UnitPrice = 6.50m,
            ImageUrl = "https://assets.cinema.local/popcorn-xl.png",
            BadgeText = "Best Seller",
            Description = "Giant tub of freshly popped salted butter corn.",
            IsActive = true
        };

        Assert.Equal("Best Seller", product.BadgeText);
        Assert.Equal("https://assets.cinema.local/popcorn-xl.png", product.ImageUrl);
        Assert.False(string.IsNullOrEmpty(product.Description));
    }
}
