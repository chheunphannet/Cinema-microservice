namespace Catalog.Api.Models;

public sealed class MovieSearchRequest
{
    public string? Q { get; set; }
    public string[]? Genres { get; set; }
    public string? AudioLanguage { get; set; }
    public string? SubtitleLanguage { get; set; }
    public string[]? Formats { get; set; } // "IMAX", "4DX", "SCREENX", "ATMOS", "VIP", "STANDARD"
    public string? ReleaseStatus { get; set; } // "now_showing", "coming_soon", "advance_booking", "ending_soon"
    public Guid? BranchId { get; set; }
    public string? City { get; set; }
    public string? Date { get; set; } // "today", "tomorrow", "this_weekend", or "yyyy-MM-dd"
    public string? TimeSlot { get; set; } // "morning", "afternoon", "evening", "late_night"
    public string? AgeRating { get; set; } // "G", "PG", "PG-13", "R", "NC-17"
    public string? DurationBucket { get; set; } // "under_90", "90_120", "over_120"
    public string? SortBy { get; set; } // "popularity", "release_date", "rating", "title_asc", "soonest_showtime"
    public int? Page { get; set; } = 1;
    public int? PageSize { get; set; } = 20;
}

public sealed class NowShowingMoviesRequest
{
    public Guid? BranchId { get; set; }
    public string? Cursor { get; set; }
    public int? Page { get; set; }
    public int? Limit { get; set; } = 20;
    public bool? FilterNoShowtimes { get; set; } = false;
}

public sealed class FeaturedMoviesRequest
{
    public Guid? BranchId { get; set; }
    public int? Limit { get; set; } = 5;
}

public sealed class ComingSoonMoviesRequest
{
    public Guid? BranchId { get; set; }
    public string? Cursor { get; set; }
    public int? Page { get; set; }
    public int? Limit { get; set; } = 20;
}

public sealed class RecommendedMoviesRequest
{
    public Guid? UserId { get; set; }
    public string[]? Genres { get; set; }
    public Guid? BranchId { get; set; }
    public string? Cursor { get; set; }
    public int? Limit { get; set; } = 10;
}

public sealed class CreateMovieRequest
{
    public string Title { get; set; } = "";
    public int DurationMinutes { get; set; }
    public string? Genre { get; set; }
    public string? Classification { get; set; }
    public string? CensorRating { get; set; }
    public string? CensorAdvisory { get; set; }
    public string? ReleaseStatus { get; set; }
    public string[]? SupportedFormats { get; set; }
    public decimal? Rating { get; set; }
    public DateOnly? ReleaseDate { get; set; }
    public string? TeaserText { get; set; }
    public string? Synopsis { get; set; }
    public string? TrailerUrl { get; set; }
    public string? PosterUrl { get; set; }
    public string? BackdropUrl { get; set; }
    public string? AudioLanguage { get; set; }
    public string? SubtitleLanguage { get; set; }
    public string? Director { get; set; }
    public string? CastMembers { get; set; }
    public bool? IsFeatured { get; set; }
    public string? BannerCustomTag { get; set; }
}

public sealed class UpdateMovieRequest
{
    public string? Title { get; set; }
    public int? DurationMinutes { get; set; }
    public string? Genre { get; set; }
    public string? Classification { get; set; }
    public string? CensorRating { get; set; }
    public string? CensorAdvisory { get; set; }
    public string? ReleaseStatus { get; set; }
    public string[]? SupportedFormats { get; set; }
    public decimal? Rating { get; set; }
    public DateOnly? ReleaseDate { get; set; }
    public string? TeaserText { get; set; }
    public string? Synopsis { get; set; }
    public string? TrailerUrl { get; set; }
    public string? PosterUrl { get; set; }
    public string? BackdropUrl { get; set; }
    public string? AudioLanguage { get; set; }
    public string? SubtitleLanguage { get; set; }
    public string? Director { get; set; }
    public string? CastMembers { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsFeatured { get; set; }
    public string? BannerCustomTag { get; set; }
}

public sealed class UpdateMovieStatusRequest
{
    public string ReleaseStatus { get; set; } = "";
    public bool? IsActive { get; set; }
}

public sealed class CreateShowtimeAdminRequest
{
    public Guid MovieId { get; set; }
    public Guid AuditoriumId { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
    public decimal BasePrice { get; set; }
    public Guid? PriceCardId { get; set; }
}

public sealed class UpdateShowtimeAdminRequest
{
    public DateTimeOffset? StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
    public decimal? BasePrice { get; set; }
    public Guid? PriceCardId { get; set; }
    public string? Status { get; set; }
}

public sealed class CreateSeatHoldAdminRequest
{
    public List<Guid> SeatIds { get; set; } = new();
    public string Reason { get; set; } = "maintenance";
}

public sealed class BulkImportShowtimesRequest
{
    public string? CsvContent { get; set; }
    public bool Execute { get; set; } = false;
}

public sealed class CreateTicketTypeRequest
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}

public sealed class UpdateTicketTypeRequest
{
    public string? Name { get; set; }
    public bool? IsActive { get; set; }
}

public sealed class PriceCardEntryInput
{
    public Guid TicketTypeId { get; set; }
    public string SeatType { get; set; } = "standard";
    public decimal Price { get; set; }
}

public sealed class CreatePriceCardRequest
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public List<PriceCardEntryInput> Entries { get; set; } = new();
}

public sealed class UpdatePriceCardRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool? IsActive { get; set; }
    public List<PriceCardEntryInput>? Entries { get; set; }
}

public sealed class CreatePricingRuleRequest
{
    public string Name { get; set; } = "";
    public string RuleType { get; set; } = "fixed_amount";
    public int? DayOfWeek { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public string? ScreenTypeCode { get; set; }
    public string AdjustmentType { get; set; } = "fixed_amount";
    public decimal AdjustmentValue { get; set; }
}

public sealed class UpdatePricingRuleRequest
{
    public string? Name { get; set; }
    public string? RuleType { get; set; }
    public int? DayOfWeek { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public string? ScreenTypeCode { get; set; }
    public string? AdjustmentType { get; set; }
    public decimal? AdjustmentValue { get; set; }
    public bool? IsActive { get; set; }
}

public sealed class CalculateTicketPriceRequest
{
    public Guid PriceCardId { get; set; }
    public string TicketTypeCode { get; set; } = "ADULT";
    public string SeatType { get; set; } = "standard";
    public DateTimeOffset ShowtimeStartsAt { get; set; }
    public string? ScreenTypeCode { get; set; }
}

