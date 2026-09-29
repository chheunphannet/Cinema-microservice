using Catalog.Api.Models;

namespace Catalog.Api.Repositories;

public interface ICatalogRepository
{
    Task<IEnumerable<BranchDto>> GetActiveBranchesAsync();
    Task<IEnumerable<BranchImageDto>> GetBranchGalleryAsync(Guid branchId);
    Task<IEnumerable<MovieDto>> GetActiveMoviesAsync();
    Task<MovieDetailsDto?> GetMovieByIdAsync(Guid movieId);
    Task<IEnumerable<ScreenTypeDto>> GetScreenTypesAsync();
    Task<IEnumerable<PromotionDto>> GetActivePromotionsAsync();
    Task<IEnumerable<GlobalNotificationDto>> GetActiveNotificationsAsync();
    Task<IEnumerable<ShowtimeDto>> GetShowtimesAsync(Guid? branchId, Guid? movieId);
    Task<ShowtimeInfo?> GetShowtimeInfoAsync(Guid showtimeId);
    Task<IEnumerable<SeatInfo>> GetSeatsByAuditoriumAsync(Guid auditoriumId);
    Task<HashSet<Guid>> GetBookedSeatIdsAsync(Guid showtimeId);
    Task<dynamic?> GetAuditoriumLayoutAsync(Guid auditoriumId);
    Task<IEnumerable<TicketTypeDto>> GetTicketTypesAsync();
    Task<IEnumerable<PriceCardEntryDto>> GetPricingMatrixAsync(Guid priceCardId);
    Task<PagedResult<MovieSearchResultDto>> SearchMoviesAsync(MovieSearchRequest request);
    Task<IEnumerable<MovieAutocompleteDto>> AutocompleteMoviesAsync(string query, int limit = 10);
    Task<CursorPagedResult<NowShowingMovieDto>> GetNowShowingMoviesAsync(NowShowingMoviesRequest request);
    Task<IReadOnlyList<FeaturedMovieDto>> GetFeaturedMoviesAsync(FeaturedMoviesRequest request);
    Task<CursorPagedResult<ComingSoonMovieDto>> GetComingSoonMoviesAsync(ComingSoonMoviesRequest request);
    Task<CursorPagedResult<RecommendedMovieDto>> GetRecommendedMoviesAsync(RecommendedMoviesRequest request);

    // Milestone 5.2: Admin Movie Lifecycle
    Task<IEnumerable<AdminMovieDetailsDto>> GetAdminMoviesAsync(string? search, string? releaseStatus, string? format, string? censorRating, bool? isActive, int page, int pageSize);
    Task<int> GetAdminMoviesCountAsync(string? search, string? releaseStatus, string? format, string? censorRating, bool? isActive);
    Task<AdminMovieDetailsDto?> GetAdminMovieByIdAsync(Guid movieId);
    Task<Guid> CreateMovieAsync(CreateMovieRequest request);
    Task<bool> UpdateMovieAsync(Guid movieId, UpdateMovieRequest request);
    Task<bool> UpdateMovieStatusAsync(Guid movieId, string releaseStatus, bool? isActive);
    Task<bool> SoftDeleteMovieAsync(Guid movieId);
    Task<bool> HasActiveShowtimesAsync(Guid movieId);

    // Milestone 5.2: Collision-Safe Showtime Engine
    Task<AuditoriumDetailsDto?> GetAuditoriumDetailsAsync(Guid auditoriumId);
    Task<IEnumerable<AuditoriumDetailsDto>> GetAuditoriumsAsync(Guid? branchId);
    Task<AdminShowtimeDto?> GetAdminShowtimeByIdAsync(Guid showtimeId);
    Task<IEnumerable<AdminShowtimeDto>> GetAdminShowtimesAsync(Guid? branchId, Guid? auditoriumId, Guid? movieId, DateTimeOffset? fromDate, DateTimeOffset? toDate);
    Task<ShowtimeCollisionDto?> CheckShowtimeCollisionAsync(Guid auditoriumId, DateTimeOffset startsAt, DateTimeOffset endsAt, Guid? excludeShowtimeId);
    Task<Guid> CreateShowtimeAsync(Guid movieId, Guid auditoriumId, DateTimeOffset startsAt, DateTimeOffset endsAt, decimal basePrice, Guid? priceCardId);
    Task<bool> UpdateShowtimeAsync(Guid showtimeId, DateTimeOffset startsAt, DateTimeOffset endsAt, decimal basePrice, Guid? priceCardId, string? status);
    Task<bool> CancelShowtimeAsync(Guid showtimeId, string? reason);
    Task<int> GetConfirmedBookingsCountForShowtimeAsync(Guid showtimeId);

    // Milestone 5.2: Seat Blocks & Maintenance Holds
    Task<HashSet<Guid>> GetBlockedSeatIdsAsync(Guid showtimeId, Guid auditoriumId);
    Task<IEnumerable<SeatBlockDto>> GetSeatBlocksAsync(Guid showtimeId, Guid auditoriumId);
    Task<List<Guid>> CreateSeatBlocksAsync(Guid auditoriumId, Guid? showtimeId, IEnumerable<Guid> seatIds, string reason, Guid blockedBy);
    Task<bool> DeleteSeatBlockAsync(Guid? showtimeId, Guid seatId);

    // Milestone 5.2: Bulk Showtime Importer
    Task<List<BulkImportRowResult>> ExecuteBulkShowtimeImportAsync(List<BulkImportRowResult> validRows);

    // Milestone 5.3: Ticket Pricing Cards, Surcharges & Matrix
    Task<IEnumerable<TicketTypeDto>> GetAllTicketTypesAsync();
    Task<Guid> CreateTicketTypeAsync(string code, string name);
    Task<bool> UpdateTicketTypeStatusAsync(Guid ticketTypeId, bool isActive);
    Task<IEnumerable<PriceCardSummaryDto>> GetPriceCardsAsync();
    Task<PriceCardDetailDto?> GetPriceCardDetailByIdAsync(Guid priceCardId);
    Task<Guid> CreatePriceCardAsync(string name, string? description, List<PriceCardEntryInput> entries);
    Task<bool> UpdatePriceCardAsync(Guid priceCardId, string? name, string? description, bool? isActive, List<PriceCardEntryInput>? entries);
    Task<bool> DeletePriceCardAsync(Guid priceCardId);
    Task<IEnumerable<PricingRuleDto>> GetPricingRulesAsync(bool? activeOnly);
    Task<PricingRuleDto?> GetPricingRuleByIdAsync(Guid ruleId);
    Task<Guid> CreatePricingRuleAsync(CreatePricingRuleRequest req);
    Task<bool> UpdatePricingRuleAsync(Guid ruleId, UpdatePricingRuleRequest req);
    Task<bool> UpdatePricingRuleStatusAsync(Guid ruleId, bool isActive);
    Task<CalculatedTicketPriceResponse?> CalculateTicketPriceAsync(Guid priceCardId, string ticketTypeCode, string seatType, DateTimeOffset showtimeStartsAt, string? screenTypeCode);
}


