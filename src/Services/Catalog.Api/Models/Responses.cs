namespace Catalog.Api.Models;

public sealed class BranchDto
{
    public Guid BranchId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string Timezone { get; set; } = "";
    public string? HeroImageUrl { get; set; }
    public bool IsActive { get; set; }
}

public sealed class BranchImageDto
{
    public Guid ImageId { get; set; }
    public Guid BranchId { get; set; }
    public string ImageUrl { get; set; } = "";
    public string? Caption { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsPrimary { get; set; }
}

public sealed class MovieDto
{
    public Guid MovieId { get; set; }
    public string Title { get; set; } = "";
    public int DurationMinutes { get; set; }
    public string Genre { get; set; } = "";
    public string Classification { get; set; } = "";
    public string CensorRating { get; set; } = "G";
    public string ReleaseStatus { get; set; } = "now_showing";
    public string[] SupportedFormats { get; set; } = Array.Empty<string>();
    public decimal Rating { get; set; }
    public DateOnly? ReleaseDate { get; set; }
    public string? TeaserText { get; set; }
    public string? TrailerUrl { get; set; }
    public string? PosterUrl { get; set; }
    public string? BackdropUrl { get; set; }
    public string AudioLanguage { get; set; } = "Khmer";
    public string SubtitleLanguage { get; set; } = "English";
    public bool IsActive { get; set; }
}

public sealed class MovieDetailsDto
{
    public Guid MovieId { get; set; }
    public string Title { get; set; } = "";
    public int DurationMinutes { get; set; }
    public string Genre { get; set; } = "";
    public string Classification { get; set; } = "";
    public string CensorRating { get; set; } = "G";
    public string? CensorAdvisory { get; set; }
    public string ReleaseStatus { get; set; } = "now_showing";
    public string[] SupportedFormats { get; set; } = Array.Empty<string>();
    public decimal Rating { get; set; }
    public DateOnly? ReleaseDate { get; set; }
    public string? TeaserText { get; set; }
    public string? Synopsis { get; set; }
    public string? TrailerUrl { get; set; }
    public string? PosterUrl { get; set; }
    public string? BackdropUrl { get; set; }
    public string AudioLanguage { get; set; } = "Khmer";
    public string SubtitleLanguage { get; set; } = "English";
    public string? Director { get; set; }
    public string? CastMembers { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class ScreenTypeDto
{
    public Guid ScreenTypeId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? LogoUrl { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public sealed class PromotionDto
{
    public Guid PromotionId { get; set; }
    public string Title { get; set; } = "";
    public string? Subtitle { get; set; }
    public string PosterUrl { get; set; } = "";
    public string? BannerUrl { get; set; }
    public string ContentText { get; set; } = "";
    public string DiscountType { get; set; } = "none";
    public decimal? DiscountValue { get; set; }
    public string? PromoCode { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public bool IsActive { get; set; }
}

public sealed class GlobalNotificationDto
{
    public Guid NotificationId { get; set; }
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string Type { get; set; } = "info";
    public string? ActionUrl { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsActive { get; set; }
}

public sealed class ShowtimeDto
{
    public Guid ShowtimeId { get; set; }
    public Guid MovieId { get; set; }
    public Guid BranchId { get; set; }
    public string AuditoriumName { get; set; } = "";
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public Guid PriceCardId { get; set; }
    public decimal BasePrice { get; set; } // Kept for backward compat
    public string Status { get; set; } = "";
}

public sealed record SeatDto(Guid SeatId, string Row, int SeatNumber, string SeatType, decimal Price, string Status);
public sealed record SeatMapResponse(Guid ShowtimeId, string AuditoriumName, int Capacity, IReadOnlyList<SeatDto> Seats, IReadOnlyList<PriceCardEntryDto> PricingMatrix);

public sealed class ShowtimeInfo
{
    public Guid ShowtimeId { get; set; }
    public Guid AuditoriumId { get; set; }
    public string AuditoriumName { get; set; } = "";
    public int Capacity { get; set; }
    public Guid PriceCardId { get; set; }
    public decimal BasePrice { get; set; } // Kept for backward compat
}

public sealed class TicketTypeDto
{
    public Guid TicketTypeId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public sealed class PriceCardEntryDto
{
    public Guid TicketTypeId { get; set; }
    public string SeatType { get; set; } = "";
    public decimal Price { get; set; }
}

public sealed class SeatInfo
{
    public Guid SeatId { get; set; }
    public string RowLabel { get; set; } = "";
    public int SeatNumber { get; set; }
    public string SeatType { get; set; } = "standard";
    public bool IsAccessible { get; set; }
}

public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}

public sealed class ShowtimeSearchResultDto
{
    public Guid ShowtimeId { get; set; }
    public Guid AuditoriumId { get; set; }
    public string AuditoriumName { get; set; } = "";
    public string HallType { get; set; } = "Standard";
    public string? HallLogoUrl { get; set; }
    public string ScreenTypeCode { get; set; } = "STANDARD";
    public string? ScreenTypeLogoUrl { get; set; }
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = "";
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public decimal BasePrice { get; set; }
    public int TotalCapacity { get; set; }
    public int AvailableSeats { get; set; }
    public bool IsSoldOut => AvailableSeats <= 0;
}

public sealed class MovieSearchResultDto
{
    public Guid MovieId { get; set; }
    public string Title { get; set; } = "";
    public int DurationMinutes { get; set; }
    public string Genre { get; set; } = "";
    public string Classification { get; set; } = "";
    public decimal Rating { get; set; }
    public DateOnly? ReleaseDate { get; set; }
    public string? TeaserText { get; set; }
    public string? PosterUrl { get; set; }
    public string? BackdropUrl { get; set; }
    public string? TrailerUrl { get; set; }
    public string AudioLanguage { get; set; } = "Khmer";
    public string SubtitleLanguage { get; set; } = "English";
    public string? Director { get; set; }
    public string? CastMembers { get; set; }
    public bool IsBookable { get; set; }
    public bool NotifyMeEnabled { get; set; }
    public List<ShowtimeSearchResultDto> Showtimes { get; set; } = new();
}

public sealed class MovieAutocompleteDto
{
    public Guid MovieId { get; set; }
    public string Title { get; set; } = "";
    public string? PosterUrl { get; set; }
    public string Genre { get; set; } = "";
    public string Classification { get; set; } = "";
    public string? MatchType { get; set; } // "title", "director", "actor"
    public string? MatchedText { get; set; }
}

public sealed class CursorPagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public string? NextCursor { get; set; }
    public bool HasMore { get; set; }
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}

public sealed class ShowtimePillDto
{
    public Guid ShowtimeId { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public string AuditoriumName { get; set; } = "";
    public string ScreenTypeCode { get; set; } = "STANDARD";
    public string? ScreenTypeLogoUrl { get; set; }
    public decimal BasePrice { get; set; }
    public int AvailableSeats { get; set; }
    public bool IsSoldOut => AvailableSeats <= 0;
}

public sealed class NowShowingMovieDto
{
    public Guid MovieId { get; set; }
    public string Title { get; set; } = "";
    public int DurationMinutes { get; set; }
    public string Genre { get; set; } = "";
    public string Classification { get; set; } = "";
    public decimal Rating { get; set; }
    public DateOnly? ReleaseDate { get; set; }
    public string? TeaserText { get; set; }
    public string? PosterUrl { get; set; }
    public string? BackdropUrl { get; set; }
    public string? TrailerUrl { get; set; }
    public string AudioLanguage { get; set; } = "Khmer";
    public string SubtitleLanguage { get; set; } = "English";
    public string? Director { get; set; }
    public string? CastMembers { get; set; }
    public bool HasShowtimesToday { get; set; }
    public bool HasShowtimesNext7Days { get; set; }
    public int TicketSalesLast48h { get; set; }
    public int AvailableShowtimesCount { get; set; }
    public DateTime? SoonestShowtime { get; set; }
    public bool IsBookable { get; set; }
    public List<ShowtimePillDto> TodayShowtimes { get; set; } = new();
}

public sealed class FeaturedMovieDto
{
    public Guid MovieId { get; set; }
    public string Title { get; set; } = "";
    public int DurationMinutes { get; set; }
    public string Genre { get; set; } = "";
    public string Classification { get; set; } = "";
    public decimal Rating { get; set; }
    public DateOnly? ReleaseDate { get; set; }
    public string? TeaserText { get; set; }
    public string? Synopsis { get; set; }
    public string? PosterUrl { get; set; }
    public string? BackdropUrl { get; set; }
    public string? TrailerUrl { get; set; }
    public string AudioLanguage { get; set; } = "Khmer";
    public string SubtitleLanguage { get; set; } = "English";
    public string? BannerCustomTag { get; set; }
    public bool IsFeatured { get; set; }
    public int TrendingRank { get; set; }
    public int TicketSalesLast48h { get; set; }
    public bool IsBookable { get; set; }
    public DateTime? NextShowtime { get; set; }
    public List<string> Formats { get; set; } = new();
}

public sealed class ComingSoonMovieDto
{
    public Guid MovieId { get; set; }
    public string Title { get; set; } = "";
    public int DurationMinutes { get; set; }
    public string Genre { get; set; } = "";
    public string Classification { get; set; } = "";
    public decimal Rating { get; set; }
    public DateOnly? ReleaseDate { get; set; }
    public string? TeaserText { get; set; }
    public string? Synopsis { get; set; }
    public string? PosterUrl { get; set; }
    public string? BackdropUrl { get; set; }
    public string? TrailerUrl { get; set; }
    public string? Director { get; set; }
    public string? CastMembers { get; set; }
    public int? DaysUntilRelease { get; set; }
    public bool IsAdvanceBooking { get; set; }
    public int AdvanceShowtimesCount { get; set; }
    public DateTime? SoonestAdvanceShowtime { get; set; }
    public bool NotifyMeEnabled { get; set; }
}

public sealed class RecommendedMovieDto
{
    public Guid MovieId { get; set; }
    public string Title { get; set; } = "";
    public int DurationMinutes { get; set; }
    public string Genre { get; set; } = "";
    public string Classification { get; set; } = "";
    public decimal Rating { get; set; }
    public DateOnly? ReleaseDate { get; set; }
    public string? TeaserText { get; set; }
    public string? PosterUrl { get; set; }
    public string? BackdropUrl { get; set; }
    public string? TrailerUrl { get; set; }
    public string RecommendationReason { get; set; } = "";
    public decimal MatchScore { get; set; }
    public List<string> MatchedGenres { get; set; } = new();
    public bool IsBookable { get; set; }
    public bool HasShowtimesToday { get; set; }
}

public sealed class AdminMovieDetailsDto
{
    public Guid MovieId { get; set; }
    public string Title { get; set; } = "";
    public int DurationMinutes { get; set; }
    public string Genre { get; set; } = "";
    public string Classification { get; set; } = "";
    public string CensorRating { get; set; } = "G";
    public string? CensorAdvisory { get; set; }
    public string ReleaseStatus { get; set; } = "now_showing";
    public string[] SupportedFormats { get; set; } = Array.Empty<string>();
    public decimal Rating { get; set; } = 8.0m;
    public DateOnly? ReleaseDate { get; set; }
    public string? TeaserText { get; set; }
    public string? Synopsis { get; set; }
    public string? TrailerUrl { get; set; }
    public string? PosterUrl { get; set; }
    public string? BackdropUrl { get; set; }
    public string AudioLanguage { get; set; } = "Khmer";
    public string SubtitleLanguage { get; set; } = "English";
    public string? Director { get; set; }
    public string? CastMembers { get; set; }
    public bool IsActive { get; set; }
    public bool IsFeatured { get; set; }
    public string? BannerCustomTag { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class AdminShowtimeDto
{
    public Guid ShowtimeId { get; set; }
    public Guid MovieId { get; set; }
    public string MovieTitle { get; set; } = "";
    public int MovieDurationMinutes { get; set; }
    public Guid AuditoriumId { get; set; }
    public string AuditoriumName { get; set; } = "";
    public int CleaningBufferMinutes { get; set; } = 15;
    public string ScreenTypeCode { get; set; } = "STANDARD";
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = "";
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public decimal BasePrice { get; set; }
    public Guid? PriceCardId { get; set; }
    public string Status { get; set; } = "scheduled";
    public int Capacity { get; set; }
    public int BookedSeatsCount { get; set; }
    public int BlockedSeatsCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class AuditoriumDetailsDto
{
    public Guid AuditoriumId { get; set; }
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = "";
    public string Name { get; set; } = "";
    public int Capacity { get; set; }
    public int CleaningBufferMinutes { get; set; } = 15;
    public Guid? ScreenTypeId { get; set; }
    public string? ScreenTypeCode { get; set; }
}

public sealed class ShowtimeCollisionDto
{
    public Guid ShowtimeId { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public string MovieTitle { get; set; } = "";
}

public sealed class SeatBlockDto
{
    public Guid BlockId { get; set; }
    public Guid AuditoriumId { get; set; }
    public Guid? ShowtimeId { get; set; }
    public Guid SeatId { get; set; }
    public string RowLabel { get; set; } = "";
    public int SeatNumber { get; set; }
    public string SeatType { get; set; } = "standard";
    public string Reason { get; set; } = "";
    public Guid BlockedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class BulkImportResult
{
    public bool Executed { get; set; }
    public int TotalRows { get; set; }
    public int ValidCount { get; set; }
    public int ConflictCount { get; set; }
    public int ErrorCount { get; set; }
    public List<BulkImportRowResult> Results { get; set; } = new();
}

public sealed class BulkImportRowResult
{
    public int RowNumber { get; set; }
    public string Status { get; set; } = "valid"; // "valid", "conflict", "invalid_movie", "invalid_auditorium", "invalid_format"
    public string? Error { get; set; }
    public Guid? ShowtimeId { get; set; }
    public Guid? MovieId { get; set; }
    public Guid? AuditoriumId { get; set; }
    public DateTimeOffset? StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
    public decimal? BasePrice { get; set; }
}

public sealed class PriceCardSummaryDto
{
    public Guid PriceCardId { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public int EntryCount { get; set; }
}

public sealed class PriceCardEntryDetailDto
{
    public Guid EntryId { get; set; }
    public Guid PriceCardId { get; set; }
    public Guid TicketTypeId { get; set; }
    public string TicketTypeCode { get; set; } = "";
    public string TicketTypeName { get; set; } = "";
    public string SeatType { get; set; } = "standard";
    public decimal Price { get; set; }
}

public sealed class PriceCardDetailDto
{
    public Guid PriceCardId { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public List<PriceCardEntryDetailDto> Entries { get; set; } = new();
}

public sealed class PricingRuleDto
{
    public Guid RuleId { get; set; }
    public string Name { get; set; } = "";
    public string RuleType { get; set; } = "";
    public int? DayOfWeek { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public string? ScreenTypeCode { get; set; }
    public string AdjustmentType { get; set; } = "fixed_amount";
    public decimal AdjustmentValue { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class PriceAdjustmentDto
{
    public string RuleName { get; set; } = "";
    public string RuleType { get; set; } = "";
    public decimal Amount { get; set; }
}

public sealed class CalculatedTicketPriceResponse
{
    public Guid PriceCardId { get; set; }
    public string TicketTypeCode { get; set; } = "";
    public string SeatType { get; set; } = "";
    public decimal BasePrice { get; set; }
    public List<PriceAdjustmentDto> Adjustments { get; set; } = new();
    public decimal FinalPrice { get; set; }
}


