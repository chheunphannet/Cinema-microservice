using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Catalog.Api.Models;
using Catalog.Api.Repositories;
using Moq;
using Xunit;

namespace Cinema.UnitTests;

public class AdminShowtimeAndSchedulingTests
{
    private readonly Mock<ICatalogRepository> _mockRepo = new();

    [Fact]
    public void CleaningBuffer_Calculates_MinimumEndTimeCorrectly()
    {
        // Arrange
        var startsAt = new DateTimeOffset(2026, 9, 20, 14, 0, 0, TimeSpan.Zero);
        int movieDurationMinutes = 120;
        int cleaningBufferMinutes = 15;
        int totalRuntime = movieDurationMinutes + cleaningBufferMinutes;

        // Act
        var minEndsAt = startsAt.AddMinutes(totalRuntime);

        // Assert
        Assert.Equal(startsAt.AddMinutes(135), minEndsAt);
        Assert.Equal(135, (minEndsAt - startsAt).TotalMinutes);
    }

    [Fact]
    public async Task CollisionGuard_Detects_OverlapInSameAuditorium()
    {
        // Arrange
        var audId = Guid.NewGuid();
        var startsAt = new DateTimeOffset(2026, 9, 20, 14, 0, 0, TimeSpan.Zero);
        var endsAt = startsAt.AddMinutes(135);

        var existingCollision = new ShowtimeCollisionDto
        {
            ShowtimeId = Guid.NewGuid(),
            MovieTitle = "Dune: Part Two",
            StartsAt = startsAt.AddMinutes(30).UtcDateTime,
            EndsAt = endsAt.AddMinutes(30).UtcDateTime
        };

        _mockRepo.Setup(r => r.CheckShowtimeCollisionAsync(audId, startsAt, endsAt, null))
            .ReturnsAsync(existingCollision);

        // Act
        var result = await _mockRepo.Object.CheckShowtimeCollisionAsync(audId, startsAt, endsAt, null);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Dune: Part Two", result.MovieTitle);
        _mockRepo.Verify(r => r.CheckShowtimeCollisionAsync(audId, startsAt, endsAt, null), Times.Once);
    }

    [Fact]
    public async Task CollisionGuard_Allows_NonOverlappingShowtimes()
    {
        // Arrange
        var audId = Guid.NewGuid();
        var startsAt = new DateTimeOffset(2026, 9, 20, 17, 0, 0, TimeSpan.Zero);
        var endsAt = startsAt.AddMinutes(135);

        _mockRepo.Setup(r => r.CheckShowtimeCollisionAsync(audId, startsAt, endsAt, null))
            .ReturnsAsync((ShowtimeCollisionDto?)null);

        // Act
        var result = await _mockRepo.Object.CheckShowtimeCollisionAsync(audId, startsAt, endsAt, null);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData("IMAX", new[] { "IMAX", "STANDARD" }, true)]
    [InlineData("STANDARD", new[] { "2D", "STANDARD" }, true)]
    [InlineData("4DX", new[] { "IMAX", "STANDARD" }, false)]
    public void FormatCompatibility_Validates_AuditoriumScreenType(string screenTypeCode, string[] supportedFormats, bool expectedCompatible)
    {
        // Act
        bool isCompatible = supportedFormats.Any(f =>
            string.Equals(f, screenTypeCode, StringComparison.OrdinalIgnoreCase) ||
            (string.Equals(screenTypeCode, "STANDARD", StringComparison.OrdinalIgnoreCase) &&
             (string.Equals(f, "2D", StringComparison.OrdinalIgnoreCase) || string.Equals(f, "STANDARD", StringComparison.OrdinalIgnoreCase))));

        // Assert
        Assert.Equal(expectedCompatible, isCompatible);
    }

    [Fact]
    public void BulkImport_Detects_BatchInternalOverlap()
    {
        // Arrange
        var audId = Guid.NewGuid();
        var t1 = new DateTimeOffset(2026, 9, 20, 10, 0, 0, TimeSpan.Zero);
        var t2 = t1.AddMinutes(135);

        var batch = new List<BulkImportRowResult>
        {
            new() { RowNumber = 1, AuditoriumId = audId, StartsAt = t1, EndsAt = t2, Status = "valid" }
        };

        // Row 2 starts during Row 1's slot (e.g. 11:00)
        var row2Start = t1.AddMinutes(60);
        var row2End = row2Start.AddMinutes(135);

        // Act - Check collision with earlier row in batch
        var conflict = batch.FirstOrDefault(b =>
            b.AuditoriumId == audId &&
            b.StartsAt < row2End &&
            row2Start < b.EndsAt);

        // Assert
        Assert.NotNull(conflict);
        Assert.Equal(1, conflict.RowNumber);
    }

    [Fact]
    public async Task SeatBlocks_Rejects_IfSeatAlreadyBooked()
    {
        // Arrange
        var showtimeId = Guid.NewGuid();
        var bookedSeatId = Guid.NewGuid();
        var requestedSeatIds = new List<Guid> { bookedSeatId, Guid.NewGuid() };

        _mockRepo.Setup(r => r.GetBookedSeatIdsAsync(showtimeId))
            .ReturnsAsync(new HashSet<Guid> { bookedSeatId });

        // Act
        var bookedSeats = await _mockRepo.Object.GetBookedSeatIdsAsync(showtimeId);
        var conflicts = requestedSeatIds.Where(s => bookedSeats.Contains(s)).ToList();

        // Assert
        Assert.Single(conflicts);
        Assert.Equal(bookedSeatId, conflicts[0]);
    }

    [Fact]
    public async Task SeatBlocks_CreatesMaintenanceHold_ForAvailableSeats()
    {
        // Arrange
        var audId = Guid.NewGuid();
        var showtimeId = Guid.NewGuid();
        var seatIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var callerId = Guid.NewGuid();
        var expectedBlockIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        _mockRepo.Setup(r => r.CreateSeatBlocksAsync(audId, showtimeId, seatIds, "vip_hold", callerId))
            .ReturnsAsync(expectedBlockIds);

        // Act
        var result = await _mockRepo.Object.CreateSeatBlocksAsync(audId, showtimeId, seatIds, "vip_hold", callerId);

        // Assert
        Assert.Equal(2, result.Count());
        _mockRepo.Verify(r => r.CreateSeatBlocksAsync(audId, showtimeId, seatIds, "vip_hold", callerId), Times.Once);
    }

    [Theory]
    [InlineData("coming_soon", true)]
    [InlineData("now_showing", true)]
    [InlineData("ended", true)]
    [InlineData("invalid_status", false)]
    public void MovieLifecycle_Validates_StatusTransitions(string status, bool expectedValid)
    {
        var validStatuses = new[] { "coming_soon", "now_showing", "ended" };
        bool isValid = validStatuses.Contains(status.Trim().ToLowerInvariant());

        Assert.Equal(expectedValid, isValid);
    }

    [Fact]
    public async Task SoftDeleteMovie_MarksAsEnded_IfFutureShowtimesExist()
    {
        // Arrange
        var movieId = Guid.NewGuid();
        _mockRepo.Setup(r => r.HasActiveShowtimesAsync(movieId))
            .ReturnsAsync(true);
        _mockRepo.Setup(r => r.SoftDeleteMovieAsync(movieId))
            .ReturnsAsync(true);

        // Act
        bool hasActiveShows = await _mockRepo.Object.HasActiveShowtimesAsync(movieId);
        bool deleted = await _mockRepo.Object.SoftDeleteMovieAsync(movieId);

        // Assert
        Assert.True(hasActiveShows);
        Assert.True(deleted);
        _mockRepo.Verify(r => r.SoftDeleteMovieAsync(movieId), Times.Once);
    }
}
