using Catalog.Api.Models;

namespace Catalog.Api.Services;

public interface ICatalogQueryService
{
    Task<IEnumerable<BranchDto>> GetBranchesAsync();
    Task<IEnumerable<BranchImageDto>> GetBranchGalleryAsync(Guid branchId);
    Task<IEnumerable<MovieDto>> GetMoviesAsync();
    Task<MovieDetailsDto?> GetMovieByIdAsync(Guid movieId);
    Task<IEnumerable<ScreenTypeDto>> GetScreenTypesAsync();
    Task<IEnumerable<PromotionDto>> GetPromotionsAsync();
    Task<IEnumerable<GlobalNotificationDto>> GetNotificationsAsync();
    Task<IEnumerable<ShowtimeDto>> GetShowtimesAsync(Guid? branchId, Guid? movieId);
    Task<object?> GetAuditoriumLayoutAsync(Guid auditoriumId);
    Task<IEnumerable<TicketTypeDto>> GetTicketTypesAsync();
    Task<PagedResult<MovieSearchResultDto>> SearchMoviesAsync(MovieSearchRequest request);
    Task<IEnumerable<MovieAutocompleteDto>> AutocompleteMoviesAsync(string query, int limit = 10);
    Task<CursorPagedResult<NowShowingMovieDto>> GetNowShowingMoviesAsync(NowShowingMoviesRequest request);
    Task<IReadOnlyList<FeaturedMovieDto>> GetFeaturedMoviesAsync(FeaturedMoviesRequest request);
    Task<CursorPagedResult<ComingSoonMovieDto>> GetComingSoonMoviesAsync(ComingSoonMoviesRequest request);
    Task<CursorPagedResult<RecommendedMovieDto>> GetRecommendedMoviesAsync(RecommendedMoviesRequest request);
}

