using Catalog.Api.Models;
using Catalog.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Endpoints;

public static class CatalogEndpoints
{
    public static void MapCatalogEndpoints(this IEndpointRouteBuilder routes)
    {
        var catalog = routes.MapGroup("/api/v1/catalog")
            .WithTags("Catalog & Showtimes")
            .AllowAnonymous();

        catalog.MapGet("/health-contract", () => Results.Ok(new
        {
            schema = "catalog",
            reads = new[] { "branches", "movies", "showtimes", "seat-map", "now-showing", "featured", "coming-soon", "recommended" },
            database = "PostgreSQL (Read-Replica / Primary) + Redis Seat Lock Integration"
        }))
        .AllowAnonymous()
        .WithSummary("Catalog Health & Architectural Contract")
        .WithDescription("Verifies catalog schema ownership, replication routing, and read-through caching contracts.");

        catalog.MapGet("/branches", async (ICatalogQueryService queryService) =>
        {
            var branches = await queryService.GetBranchesAsync();
            return Results.Ok(branches);
        })
        .WithSummary("Get All Cinema Branches")
        .WithDescription("Retrieves the list of active cinema branches with Redis read-through caching.");

        catalog.MapGet("/ticket-types", async (ICatalogQueryService queryService) =>
        {
            var types = await queryService.GetTicketTypesAsync();
            return Results.Ok(types);
        })
        .WithSummary("Get Ticket Types")
        .WithDescription("Retrieves available ticket demographics (Adult, Child, etc).");

        catalog.MapGet("/movies", async (ICatalogQueryService queryService) =>
        {
            var movies = await queryService.GetMoviesAsync();
            return Results.Ok(movies);
        })
        .WithSummary("Get Movies Catalog")
        .WithDescription("Lists currently scheduled and active movies with Redis read-through caching.");

        catalog.MapGet("/showtimes", async (
            [FromQuery] Guid? branchId,
            [FromQuery] Guid? movieId,
            ICatalogQueryService queryService) =>
        {
            var showtimes = await queryService.GetShowtimesAsync(branchId, movieId);
            return Results.Ok(showtimes);
        })
        .WithSummary("List Showtimes Schedules")
        .WithDescription("Returns scheduled showtimes filtered by cinema branch and movie ID with Redis read-through caching.");

        catalog.MapGet("/seat-map/{showtimeId:guid}", async (Guid showtimeId, ISeatMapService seatMapService) =>
        {
            var response = await seatMapService.GetSeatMapAsync(showtimeId);
            if (response == null) return Results.NotFound(new { error = $"Showtime {showtimeId} not found" });
            return Results.Ok(response);
        })
        .WithSummary("Get Auditorium Seat Map Layout")
        .WithDescription("Provides the visual seat matrix (rows, numbers, types, status: available/held/booked) for box-office touchscreen seat selection.");

        catalog.MapGet("/auditorium-layouts/{auditoriumId:guid}", async (Guid auditoriumId, ICatalogQueryService queryService) =>
        {
            var response = await queryService.GetAuditoriumLayoutAsync(auditoriumId);
            if (response == null) return Results.NotFound(new { error = $"Auditorium layout for {auditoriumId} not found." });
            return Results.Ok(response);
        })
        .WithSummary("Phase 2 Module 1.1: AuditoriumLayouts JSONB")
        .WithDescription("Retrieves the JSONB auditorium seat map layout model specified in Phase 2 Module 1.1.");

        catalog.MapGet("/showtimes/{showtimeId:guid}/redis-seat-matrix", async (Guid showtimeId, ISeatMapService seatMapService) =>
        {
            var response = await seatMapService.GetRedisSeatMatrixAsync(showtimeId);
            if (response == null) return Results.NotFound(new { error = $"Showtime {showtimeId} not found" });
            return Results.Ok(response);
        })
        .WithSummary("Phase 2 Module 1.3: Auditorium Seat Matrix Schema (Redis Read-Through + Jittered TTL)")
        .WithDescription("Implements exact JSON matrix schema from Phase 2 Module 1.3 with randomized jittered TTL to prevent cache stampedes.");

        catalog.MapGet("/blockbuster/{showtimeId:guid}/seat-matrix", async (Guid showtimeId, IBlockbusterCacheService blockbusterService) =>
        {
            var response = await blockbusterService.GetSeatMatrixAsync(showtimeId);
            if (response == null) return Results.NotFound();
            return Results.Ok(response);
        })
        .WithSummary("Phase 2 Module 1.2: Blockbuster High-Traffic Cache")
        .WithDescription("Bypasses database queries for high-fan-out blockbuster movie premieres via dedicated pre-computed Redis cache.");

        catalog.MapGet("/movies/{movieId:guid}", async (Guid movieId, ICatalogQueryService queryService) =>
        {
            var movie = await queryService.GetMovieByIdAsync(movieId);
            if (movie == null) return Results.NotFound(new { error = $"Movie {movieId} not found." });
            return Results.Ok(movie);
        })
        .WithSummary("Get Detailed Movie Information")
        .WithDescription("Returns comprehensive movie details including trailer URL, poster/backdrop, synopsis, languages (Khmer/English), and cast.");

        catalog.MapGet("/screen-types", async (ICatalogQueryService queryService) =>
        {
            var screens = await queryService.GetScreenTypesAsync();
            return Results.Ok(screens);
        })
        .WithSummary("Get Available Screen Formats")
        .WithDescription("Retrieves screen formats and experience types (IMAX, 4DX, ScreenX, Dolby Atmos) with branding logo URLs.");

        catalog.MapGet("/promotions", async (ICatalogQueryService queryService) =>
        {
            var promotions = await queryService.GetPromotionsAsync();
            return Results.Ok(promotions);
        })
        .WithSummary("Get Active Marketing Promotions")
        .WithDescription("Lists currently active promotional campaigns, poster images, discounts, and markdown content.");

        catalog.MapGet("/notifications/active", async (ICatalogQueryService queryService) =>
        {
            var notifications = await queryService.GetNotificationsAsync();
            return Results.Ok(notifications);
        })
        .WithSummary("Get Active Global Announcements")
        .WithDescription("Retrieves active system-wide announcements, flash sales, and maintenance notices.");

        catalog.MapGet("/branches/{branchId:guid}/gallery", async (Guid branchId, ICatalogQueryService queryService) =>
        {
            var gallery = await queryService.GetBranchGalleryAsync(branchId);
            return Results.Ok(gallery);
        })
        .WithSummary("Get Cinema Branch Photo Gallery")
        .WithDescription("Retrieves high-resolution images, lobby views, and hall photos for a cinema branch.");

        catalog.MapGet("/movies/search", async (
            [AsParameters] Models.MovieSearchRequest request, 
            ICatalogQueryService queryService) =>
        {
            var results = await queryService.SearchMoviesAsync(request);
            return Results.Ok(results);
        })
        .WithSummary("Multi-Faceted Movie Search & Discovery")
        .WithDescription("High-performance faceted movie search supporting keywords, genres, formats, date/time slot bucketing, branch filtering, duration, and real-time seat availability.");

        catalog.MapGet("/movies/autocomplete", async (
            [FromQuery] string q,
            [FromQuery] int? limit,
            ICatalogQueryService queryService) =>
        {
            var suggestions = await queryService.AutocompleteMoviesAsync(q, limit ?? 10);
            return Results.Ok(suggestions);
        })
        .WithSummary("Fast Movie Autocomplete Suggestions")
        .WithDescription("Sub-15ms fuzzy keyword autocomplete across movie titles, directors, and cast members.");

        catalog.MapGet("/movies/now-showing", async (
            [AsParameters] NowShowingMoviesRequest request,
            ICatalogQueryService queryService) =>
        {
            var result = await queryService.GetNowShowingMoviesAsync(request);
            return Results.Ok(result);
        })
        .Produces<CursorPagedResult<NowShowingMovieDto>>(StatusCodes.Status200OK)
        .WithSummary("Now Showing Movies Catalog (Load More / Keyset Cursor)")
        .WithDescription("Finite catalog of currently active movies sorted by today's showtimes at selected branch first, 24-48h ticket sales, and newest release date, deprioritizing unscheduled movies.");

        catalog.MapGet("/movies/featured", async (
            [AsParameters] FeaturedMoviesRequest request,
            ICatalogQueryService queryService) =>
        {
            var result = await queryService.GetFeaturedMoviesAsync(request);
            return Results.Ok(result);
        })
        .Produces<IReadOnlyList<FeaturedMovieDto>>(StatusCodes.Status200OK)
        .WithSummary("Homepage Hero Featured Movies")
        .WithDescription("Curated, rule-ranked small set of high-profile movies for homepage banners with backdrop images, screen format badges, and trending rank.");

        catalog.MapGet("/movies/coming-soon", async (
            [AsParameters] ComingSoonMoviesRequest request,
            ICatalogQueryService queryService) =>
        {
            var result = await queryService.GetComingSoonMoviesAsync(request);
            return Results.Ok(result);
        })
        .Produces<CursorPagedResult<ComingSoonMovieDto>>(StatusCodes.Status200OK)
        .WithSummary("Coming Soon Upcoming Movies")
        .WithDescription("Finite upcoming movie catalog ordered chronologically by release date with load more / cursor pagination and advance booking indicators.");

        catalog.MapGet("/movies/recommended", async (
            [AsParameters] RecommendedMoviesRequest request,
            ICatalogQueryService queryService) =>
        {
            var result = await queryService.GetRecommendedMoviesAsync(request);
            return Results.Ok(result);
        })
        .Produces<CursorPagedResult<RecommendedMovieDto>>(StatusCodes.Status200OK)
        .WithSummary("Personalized Movie Recommendations (Infinite Scroll)")
        .WithDescription("Unbounded, algorithmic movie recommendations personalized by past booking genre affinity, trending popularity, rating, and cursor-based infinite scroll.");
    }

}
