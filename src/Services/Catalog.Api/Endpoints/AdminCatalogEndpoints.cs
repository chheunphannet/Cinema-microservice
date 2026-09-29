using System.Data;
using System.Security.Claims;
using Catalog.Api.Models;
using Catalog.Api.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Npgsql;

namespace Catalog.Api.Endpoints;

public static class AdminCatalogEndpoints
{
    public static void MapAdminCatalogEndpoints(this IEndpointRouteBuilder routes)
    {
        var admin = routes.MapGroup("/api/v1/admin")
            .WithTags("Back-Office Admin — Movies & Showtimes")
            .RequireAuthorization();

        // =========================================================================
        // Module 1: Movie & Content Lifecycle Management
        // =========================================================================

        admin.MapGet("/movies", async (
            [FromQuery] string? search,
            [FromQuery] string? releaseStatus,
            [FromQuery] string? format,
            [FromQuery] string? censorRating,
            [FromQuery] bool? isActive,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            ICatalogRepository repository) =>
        {
            int currentPage = page.GetValueOrDefault(1);
            int size = pageSize.GetValueOrDefault(20);
            if (currentPage < 1) currentPage = 1;
            if (size < 1 || size > 100) size = 20;

            var items = await repository.GetAdminMoviesAsync(search, releaseStatus, format, censorRating, isActive, currentPage, size);
            var total = await repository.GetAdminMoviesCountAsync(search, releaseStatus, format, censorRating, isActive);

            return Results.Ok(new PagedResult<AdminMovieDetailsDto>
            {
                Items = items.ToList(),
                TotalCount = total,
                Page = currentPage,
                PageSize = size
            });
        })
        .RequireAuthorization("ContentManager")
        .WithSummary("List Admin Movies")
        .WithDescription("Retrieves cinema movies filtered by release status, screen format, censor rating, active status, or search query.");

        admin.MapGet("/movies/{movieId:guid}", async (
            Guid movieId,
            ICatalogRepository repository) =>
        {
            var movie = await repository.GetAdminMovieByIdAsync(movieId);
            if (movie == null)
            {
                return Results.NotFound(new { error = $"Movie {movieId} not found." });
            }
            return Results.Ok(movie);
        })
        .RequireAuthorization("ContentManager")
        .WithSummary("Get Movie Admin Details")
        .WithDescription("Retrieves complete back-office metadata for a specific movie.");

        admin.MapPost("/movies", async (
            [FromBody] CreateMovieRequest request,
            ICatalogRepository repository,
            IDistributedCache cache) =>
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return Results.BadRequest(new { error = "Movie title is required." });
            }
            if (request.DurationMinutes <= 0)
            {
                return Results.BadRequest(new { error = "DurationMinutes must be greater than zero." });
            }

            if (!string.IsNullOrWhiteSpace(request.ReleaseStatus))
            {
                var validStatuses = new[] { "coming_soon", "now_showing", "ended" };
                if (!validStatuses.Contains(request.ReleaseStatus.Trim().ToLowerInvariant()))
                {
                    return Results.BadRequest(new { error = "ReleaseStatus must be one of: coming_soon, now_showing, ended." });
                }
            }

            var movieId = await repository.CreateMovieAsync(request);
            await cache.RemoveAsync("catalog:movies");

            var created = await repository.GetAdminMovieByIdAsync(movieId);
            return Results.Created($"/api/v1/admin/movies/{movieId}", created);
        })
        .RequireAuthorization("ContentManager")
        .WithSummary("Create Movie")
        .WithDescription("Creates a new movie in the catalog with format compatibility and regional censor ratings.");

        admin.MapPut("/movies/{movieId:guid}", async (
            Guid movieId,
            [FromBody] UpdateMovieRequest request,
            ICatalogRepository repository,
            IDistributedCache cache) =>
        {
            var existing = await repository.GetAdminMovieByIdAsync(movieId);
            if (existing == null)
            {
                return Results.NotFound(new { error = $"Movie {movieId} not found." });
            }

            if (request.DurationMinutes.HasValue && request.DurationMinutes.Value <= 0)
            {
                return Results.BadRequest(new { error = "DurationMinutes must be greater than zero." });
            }

            if (!string.IsNullOrWhiteSpace(request.ReleaseStatus))
            {
                var validStatuses = new[] { "coming_soon", "now_showing", "ended" };
                if (!validStatuses.Contains(request.ReleaseStatus.Trim().ToLowerInvariant()))
                {
                    return Results.BadRequest(new { error = "ReleaseStatus must be one of: coming_soon, now_showing, ended." });
                }
            }

            var success = await repository.UpdateMovieAsync(movieId, request);
            if (!success)
            {
                return Results.NotFound(new { error = $"Failed to update movie {movieId}." });
            }

            await cache.RemoveAsync("catalog:movies");
            await cache.RemoveAsync($"catalog:movie:{movieId}");

            var updated = await repository.GetAdminMovieByIdAsync(movieId);
            return Results.Ok(updated);
        })
        .RequireAuthorization("ContentManager")
        .WithSummary("Update Movie")
        .WithDescription("Updates movie metadata, duration, formats, or regional censor ratings.");

        admin.MapPatch("/movies/{movieId:guid}/status", async (
            Guid movieId,
            [FromBody] UpdateMovieStatusRequest request,
            ICatalogRepository repository,
            IDistributedCache cache) =>
        {
            var existing = await repository.GetAdminMovieByIdAsync(movieId);
            if (existing == null)
            {
                return Results.NotFound(new { error = $"Movie {movieId} not found." });
            }

            var validStatuses = new[] { "coming_soon", "now_showing", "ended" };
            string normalized = request.ReleaseStatus?.Trim().ToLowerInvariant() ?? "";
            if (!validStatuses.Contains(normalized))
            {
                return Results.BadRequest(new { error = "ReleaseStatus must be one of: coming_soon, now_showing, ended." });
            }

            var success = await repository.UpdateMovieStatusAsync(movieId, normalized, request.IsActive);
            if (!success)
            {
                return Results.NotFound(new { error = $"Failed to update status for movie {movieId}." });
            }

            await cache.RemoveAsync("catalog:movies");
            await cache.RemoveAsync($"catalog:movie:{movieId}");

            return Results.Ok(new
            {
                movieId,
                previousStatus = existing.ReleaseStatus,
                releaseStatus = normalized,
                isActive = request.IsActive ?? existing.IsActive
            });
        })
        .RequireAuthorization("ContentManager")
        .WithSummary("Update Movie Release Status")
        .WithDescription("Executes release status transitions: coming_soon -> now_showing -> ended.");

        admin.MapDelete("/movies/{movieId:guid}", async (
            Guid movieId,
            ICatalogRepository repository,
            IDistributedCache cache) =>
        {
            var existing = await repository.GetAdminMovieByIdAsync(movieId);
            if (existing == null)
            {
                return Results.NotFound(new { error = $"Movie {movieId} not found." });
            }

            bool hasActiveShows = await repository.HasActiveShowtimesAsync(movieId);
            await repository.SoftDeleteMovieAsync(movieId);

            await cache.RemoveAsync("catalog:movies");
            await cache.RemoveAsync($"catalog:movie:{movieId}");

            if (hasActiveShows)
            {
                return Results.Ok(new
                {
                    movieId,
                    action = "soft_deactivated",
                    message = "Movie has active future showtimes; it has been soft-deactivated and marked as ended."
                });
            }

            return Results.Ok(new
            {
                movieId,
                action = "deactivated",
                message = "Movie deactivated and marked as ended."
            });
        })
        .RequireAuthorization("SuperAdmin")
        .WithSummary("Delete / Deactivate Movie")
        .WithDescription("Deactivates a movie. If future scheduled showtimes exist, safely soft-deactivates the movie.");

        // =========================================================================
        // Module 2: Collision-Safe Showtime Engine & Schedulers
        // =========================================================================

        admin.MapGet("/auditoriums", async (
            HttpContext context,
            [FromQuery] Guid? branchId,
            ICatalogRepository repository) =>
        {
            var (callerBranchId, isSuperAdmin, isContentManager) = GetCallerContext(context);
            if (!isSuperAdmin && !isContentManager && callerBranchId.HasValue)
            {
                branchId = callerBranchId;
            }

            var auditoriums = await repository.GetAuditoriumsAsync(branchId);
            return Results.Ok(auditoriums);
        })
        .RequireAuthorization(policy => policy.RequireRole("content_manager", "branch_manager", "super_admin", "system_admin", "staff"))
        .WithSummary("List Admin Auditoriums")
        .WithDescription("Retrieves auditoriums with branch and screen details for showtime scheduling.");

        admin.MapGet("/showtimes", async (
            HttpContext context,
            [FromQuery] Guid? branchId,
            [FromQuery] Guid? auditoriumId,
            [FromQuery] Guid? movieId,
            [FromQuery] DateTimeOffset? fromDate,
            [FromQuery] DateTimeOffset? toDate,
            ICatalogRepository repository) =>
        {
            var (callerBranchId, isSuperAdmin, isContentManager) = GetCallerContext(context);
            if (!isSuperAdmin && !isContentManager)
            {
                if (callerBranchId == null)
                {
                    return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Caller has no branch assignment.");
                }
                if (branchId.HasValue && branchId.Value != callerBranchId.Value)
                {
                    return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Cannot access showtimes of a different branch.");
                }
                branchId = callerBranchId;
            }

            var showtimes = await repository.GetAdminShowtimesAsync(branchId, auditoriumId, movieId, fromDate, toDate);
            return Results.Ok(showtimes);
        })
        .RequireAuthorization(policy => policy.RequireRole("content_manager", "branch_manager", "super_admin", "system_admin"))
        .WithSummary("List Admin Showtimes")
        .WithDescription("Lists showtimes with capacity, booking counts, maintenance holds, and branch scoping.");

        admin.MapGet("/showtimes/{showtimeId:guid}", async (
            HttpContext context,
            Guid showtimeId,
            ICatalogRepository repository) =>
        {
            var showtime = await repository.GetAdminShowtimeByIdAsync(showtimeId);
            if (showtime == null)
            {
                return Results.NotFound(new { error = $"Showtime {showtimeId} not found." });
            }

            var (callerBranchId, isSuperAdmin, isContentManager) = GetCallerContext(context);
            if (!isSuperAdmin && !isContentManager && callerBranchId.HasValue && showtime.BranchId != callerBranchId.Value)
            {
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Cannot access showtimes of a different branch.");
            }

            return Results.Ok(showtime);
        })
        .RequireAuthorization(policy => policy.RequireRole("content_manager", "branch_manager", "super_admin", "system_admin"))
        .WithSummary("Get Showtime Admin Details")
        .WithDescription("Retrieves detailed schedule metrics for a single showtime.");

        admin.MapPost("/showtimes", async (
            HttpContext context,
            [FromBody] CreateShowtimeAdminRequest request,
            ICatalogRepository repository,
            IDistributedCache cache) =>
        {
            if (request.MovieId == Guid.Empty || request.AuditoriumId == Guid.Empty)
            {
                return Results.BadRequest(new { error = "MovieId and AuditoriumId are required." });
            }
            if (request.BasePrice < 0)
            {
                return Results.BadRequest(new { error = "BasePrice must be non-negative." });
            }

            var movie = await repository.GetAdminMovieByIdAsync(request.MovieId);
            if (movie == null || !movie.IsActive)
            {
                return Results.BadRequest(new { error = $"Movie {request.MovieId} not found or inactive." });
            }

            var auditorium = await repository.GetAuditoriumDetailsAsync(request.AuditoriumId);
            if (auditorium == null)
            {
                return Results.BadRequest(new { error = $"Auditorium {request.AuditoriumId} not found." });
            }

            var (callerBranchId, isSuperAdmin, isContentManager) = GetCallerContext(context);
            if (!isSuperAdmin && !isContentManager && callerBranchId.HasValue && auditorium.BranchId != callerBranchId.Value)
            {
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Cannot schedule showtimes in an auditorium of a different branch.");
            }

            // 1. Calculate Required Runtime (Movie Duration + Cleaning/Turnaround Buffer)
            int cleaningBuffer = auditorium.CleaningBufferMinutes > 0 ? auditorium.CleaningBufferMinutes : 15;
            int totalRuntimeMinutes = movie.DurationMinutes + cleaningBuffer;
            var minEndsAt = request.StartsAt.AddMinutes(totalRuntimeMinutes);

            DateTimeOffset endsAt = request.EndsAt ?? minEndsAt;
            if (endsAt < minEndsAt)
            {
                return Results.BadRequest(new
                {
                    error = $"Showtime ends_at ({endsAt:O}) must account for movie duration ({movie.DurationMinutes}m) + auditorium cleaning buffer ({cleaningBuffer}m) = {totalRuntimeMinutes} minutes. Minimum ends_at is {minEndsAt:O}."
                });
            }

            // 2. Format Compatibility Verification
            if (!string.IsNullOrWhiteSpace(auditorium.ScreenTypeCode) && movie.SupportedFormats.Length > 0)
            {
                bool isCompatible = movie.SupportedFormats.Any(f => 
                    string.Equals(f, auditorium.ScreenTypeCode, StringComparison.OrdinalIgnoreCase) ||
                    (string.Equals(auditorium.ScreenTypeCode, "STANDARD", StringComparison.OrdinalIgnoreCase) && 
                     (string.Equals(f, "2D", StringComparison.OrdinalIgnoreCase) || string.Equals(f, "STANDARD", StringComparison.OrdinalIgnoreCase))));

                if (!isCompatible)
                {
                    return Results.BadRequest(new
                    {
                        error = $"Movie '{movie.Title}' does not support auditorium screen format '{auditorium.ScreenTypeCode}'. Supported formats: {string.Join(", ", movie.SupportedFormats)}."
                    });
                }
            }

            // 3. Collision Guard Query (Temporal Range Overlap Check)
            var collision = await repository.CheckShowtimeCollisionAsync(request.AuditoriumId, request.StartsAt, endsAt, null);
            if (collision != null)
            {
                return Results.Conflict(new
                {
                    error = "Showtime collision detected with an existing active schedule in this auditorium.",
                    conflictingShowtimeId = collision.ShowtimeId,
                    conflictingMovie = collision.MovieTitle,
                    conflictingStartsAt = collision.StartsAt,
                    conflictingEndsAt = collision.EndsAt
                });
            }

            // 4. Atomic PostgreSQL Insert (backed by GiST exclusion constraint)
            Guid showtimeId;
            try
            {
                showtimeId = await repository.CreateShowtimeAsync(
                    request.MovieId, request.AuditoriumId, request.StartsAt, endsAt, request.BasePrice, request.PriceCardId);
            }
            catch (PostgresException ex) when (ex.SqlState == "23P01")
            {
                return Results.Conflict(new
                {
                    error = "Showtime collision detected by database exclusion constraint (concurrent scheduling race condition)."
                });
            }

            await cache.RemoveAsync($"catalog:showtimes:{auditorium.BranchId}:{request.MovieId}");
            await cache.RemoveAsync($"catalog:showtimes::{request.MovieId}");

            var created = await repository.GetAdminShowtimeByIdAsync(showtimeId);
            return Results.Created($"/api/v1/admin/showtimes/{showtimeId}", created);
        })
        .RequireAuthorization(policy => policy.RequireRole("content_manager", "branch_manager", "super_admin", "system_admin"))
        .WithSummary("Create Collision-Safe Showtime")
        .WithDescription("Schedules a new showtime enforcing mandatory turnaround/cleaning buffers and temporal GiST overlap collision checks.");

        admin.MapPut("/showtimes/{showtimeId:guid}", async (
            HttpContext context,
            Guid showtimeId,
            [FromBody] UpdateShowtimeAdminRequest request,
            ICatalogRepository repository,
            IDistributedCache cache) =>
        {
            var existing = await repository.GetAdminShowtimeByIdAsync(showtimeId);
            if (existing == null)
            {
                return Results.NotFound(new { error = $"Showtime {showtimeId} not found." });
            }

            var (callerBranchId, isSuperAdmin, isContentManager) = GetCallerContext(context);
            if (!isSuperAdmin && !isContentManager && callerBranchId.HasValue && existing.BranchId != callerBranchId.Value)
            {
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Cannot update showtimes of a different branch.");
            }

            var startsAt = request.StartsAt ?? new DateTimeOffset(existing.StartsAt, TimeSpan.Zero);
            var endsAt = request.EndsAt ?? new DateTimeOffset(existing.EndsAt, TimeSpan.Zero);

            if (request.StartsAt.HasValue || request.EndsAt.HasValue)
            {
                int cleaningBuffer = existing.CleaningBufferMinutes > 0 ? existing.CleaningBufferMinutes : 15;
                int totalRuntimeMinutes = existing.MovieDurationMinutes + cleaningBuffer;
                var minEndsAt = startsAt.AddMinutes(totalRuntimeMinutes);

                if (endsAt < minEndsAt)
                {
                    return Results.BadRequest(new
                    {
                        error = $"Showtime ends_at ({endsAt:O}) must account for movie duration ({existing.MovieDurationMinutes}m) + cleaning buffer ({cleaningBuffer}m) = {totalRuntimeMinutes} minutes. Minimum ends_at is {minEndsAt:O}."
                    });
                }

                var collision = await repository.CheckShowtimeCollisionAsync(existing.AuditoriumId, startsAt, endsAt, showtimeId);
                if (collision != null)
                {
                    return Results.Conflict(new
                    {
                        error = "Showtime collision detected with an existing active schedule in this auditorium.",
                        conflictingShowtimeId = collision.ShowtimeId,
                        conflictingMovie = collision.MovieTitle,
                        conflictingStartsAt = collision.StartsAt,
                        conflictingEndsAt = collision.EndsAt
                    });
                }
            }

            decimal basePrice = request.BasePrice ?? existing.BasePrice;
            try
            {
                await repository.UpdateShowtimeAsync(showtimeId, startsAt, endsAt, basePrice, request.PriceCardId, request.Status);
            }
            catch (PostgresException ex) when (ex.SqlState == "23P01")
            {
                return Results.Conflict(new
                {
                    error = "Showtime collision detected by database exclusion constraint."
                });
            }

            await cache.RemoveAsync($"catalog:showtimes:{existing.BranchId}:{existing.MovieId}");
            await cache.RemoveAsync($"catalog:seat-matrix:{showtimeId}");

            var updated = await repository.GetAdminShowtimeByIdAsync(showtimeId);
            return Results.Ok(updated);
        })
        .RequireAuthorization(policy => policy.RequireRole("content_manager", "branch_manager", "super_admin", "system_admin"))
        .WithSummary("Update Showtime")
        .WithDescription("Updates schedule times, pricing, or status with overlap collision recalculation.");

        admin.MapDelete("/showtimes/{showtimeId:guid}", async (
            HttpContext context,
            Guid showtimeId,
            [FromQuery] string? reason,
            ICatalogRepository repository,
            IDistributedCache cache) =>
        {
            var existing = await repository.GetAdminShowtimeByIdAsync(showtimeId);
            if (existing == null)
            {
                return Results.NotFound(new { error = $"Showtime {showtimeId} not found." });
            }

            var (callerBranchId, isSuperAdmin, isContentManager) = GetCallerContext(context);
            if (!isSuperAdmin && !isContentManager && callerBranchId.HasValue && existing.BranchId != callerBranchId.Value)
            {
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Cannot cancel showtimes of a different branch.");
            }

            int bookingsCount = await repository.GetConfirmedBookingsCountForShowtimeAsync(showtimeId);
            if (bookingsCount > 0 && string.IsNullOrWhiteSpace(reason))
            {
                return Results.Conflict(new
                {
                    error = $"Showtime has {bookingsCount} confirmed booking(s). A cancellation reason is mandatory to cancel this showtime.",
                    confirmedBookingsCount = bookingsCount
                });
            }

            await repository.CancelShowtimeAsync(showtimeId, reason);

            await cache.RemoveAsync($"catalog:showtimes:{existing.BranchId}:{existing.MovieId}");
            await cache.RemoveAsync($"catalog:seat-matrix:{showtimeId}");

            return Results.Ok(new
            {
                showtimeId,
                status = "cancelled",
                confirmedBookingsAffected = bookingsCount,
                reason = reason ?? "Cancelled by back-office management"
            });
        })
        .RequireAuthorization(policy => policy.RequireRole("content_manager", "branch_manager", "super_admin", "system_admin"))
        .WithSummary("Cancel Showtime")
        .WithDescription("Cancels a scheduled showtime. Requires a reason if confirmed customer bookings exist.");

        // =========================================================================
        // Module 2: Bulk CSV Showtime Importer
        // =========================================================================

        admin.MapPost("/showtimes/bulk-import", async (
            HttpContext context,
            [FromQuery] bool? execute,
            [FromBody] BulkImportShowtimesRequest? bodyRequest,
            ICatalogRepository repository,
            IDistributedCache cache) =>
        {
            bool shouldExecute = execute ?? bodyRequest?.Execute ?? false;
            string? csvContent = bodyRequest?.CsvContent;

            // If payload was not JSON, read from form file or body
            if (string.IsNullOrWhiteSpace(csvContent) && context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync();
                var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
                if (file != null)
                {
                    using var reader = new StreamReader(file.OpenReadStream());
                    csvContent = await reader.ReadToEndAsync();
                }
            }

            if (string.IsNullOrWhiteSpace(csvContent))
            {
                using var reader = new StreamReader(context.Request.Body);
                csvContent = await reader.ReadToEndAsync();
            }

            if (string.IsNullOrWhiteSpace(csvContent))
            {
                return Results.BadRequest(new { error = "CSV content or file upload is required." });
            }

            var lines = csvContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length <= 1)
            {
                return Results.BadRequest(new { error = "CSV file must contain a header row and at least one data row." });
            }

            // Parse header
            var headerCols = lines[0].Split(',').Select(h => h.Trim().ToLowerInvariant()).ToArray();
            int movieIdx = Array.IndexOf(headerCols, "movieid");
            int audIdx = Array.IndexOf(headerCols, "auditoriumid");
            int startIdx = Array.IndexOf(headerCols, "startsat");
            int priceIdx = Array.IndexOf(headerCols, "baseprice");

            if (movieIdx < 0 || audIdx < 0 || startIdx < 0 || priceIdx < 0)
            {
                return Results.BadRequest(new
                {
                    error = "CSV header must contain columns: MovieId, AuditoriumId, StartsAt, BasePrice",
                    detectedHeaders = headerCols
                });
            }

            var results = new List<BulkImportRowResult>();
            var validBatches = new List<BulkImportRowResult>();

            // Cache movie and auditorium metadata for fast in-batch validation
            var movieMap = new Dictionary<Guid, AdminMovieDetailsDto>();
            var auditoriumMap = new Dictionary<Guid, AuditoriumDetailsDto>();

            var (callerBranchId, isSuperAdmin, isContentManager) = GetCallerContext(context);

            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var cols = line.Split(',').Select(c => c.Trim()).ToArray();
                var rowResult = new BulkImportRowResult { RowNumber = i };

                if (cols.Length <= Math.Max(Math.Max(movieIdx, audIdx), Math.Max(startIdx, priceIdx)))
                {
                    rowResult.Status = "invalid_format";
                    rowResult.Error = "Row has fewer columns than specified by header.";
                    results.Add(rowResult);
                    continue;
                }

                // 1. MovieId
                if (!Guid.TryParse(cols[movieIdx], out var movieId))
                {
                    rowResult.Status = "invalid_format";
                    rowResult.Error = $"Invalid MovieId format: '{cols[movieIdx]}'";
                    results.Add(rowResult);
                    continue;
                }
                rowResult.MovieId = movieId;

                if (!movieMap.TryGetValue(movieId, out var movie))
                {
                    var fetched = await repository.GetAdminMovieByIdAsync(movieId);
                    if (fetched == null || !fetched.IsActive)
                    {
                        rowResult.Status = "invalid_movie";
                        rowResult.Error = $"Movie {movieId} does not exist or is inactive.";
                        results.Add(rowResult);
                        continue;
                    }
                    movie = fetched;
                    movieMap[movieId] = movie;
                }

                // 2. AuditoriumId
                if (!Guid.TryParse(cols[audIdx], out var audId))
                {
                    rowResult.Status = "invalid_format";
                    rowResult.Error = $"Invalid AuditoriumId format: '{cols[audIdx]}'";
                    results.Add(rowResult);
                    continue;
                }
                rowResult.AuditoriumId = audId;

                if (!auditoriumMap.TryGetValue(audId, out var auditorium))
                {
                    var fetchedAud = await repository.GetAuditoriumDetailsAsync(audId);
                    if (fetchedAud == null)
                    {
                        rowResult.Status = "invalid_auditorium";
                        rowResult.Error = $"Auditorium {audId} does not exist.";
                        results.Add(rowResult);
                        continue;
                    }
                    auditorium = fetchedAud;
                    auditoriumMap[audId] = auditorium;
                }

                // Branch scoping check
                if (!isSuperAdmin && !isContentManager && callerBranchId.HasValue && auditorium.BranchId != callerBranchId.Value)
                {
                    rowResult.Status = "forbidden";
                    rowResult.Error = $"Cannot schedule showtimes in auditorium of another branch ({auditorium.BranchId}).";
                    results.Add(rowResult);
                    continue;
                }

                // 3. StartsAt
                if (!DateTimeOffset.TryParse(cols[startIdx], out var startsAt))
                {
                    rowResult.Status = "invalid_format";
                    rowResult.Error = $"Invalid StartsAt timestamp format: '{cols[startIdx]}'";
                    results.Add(rowResult);
                    continue;
                }
                rowResult.StartsAt = startsAt;

                // 4. BasePrice
                if (!decimal.TryParse(cols[priceIdx], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var basePrice) || basePrice < 0)
                {
                    rowResult.Status = "invalid_format";
                    rowResult.Error = $"Invalid BasePrice: '{cols[priceIdx]}'";
                    results.Add(rowResult);
                    continue;
                }
                rowResult.BasePrice = basePrice;

                // 5. Compute EndsAt with Cleaning Buffer
                int cleaningBuffer = auditorium.CleaningBufferMinutes > 0 ? auditorium.CleaningBufferMinutes : 15;
                int totalMinutes = movie.DurationMinutes + cleaningBuffer;
                var endsAt = startsAt.AddMinutes(totalMinutes);
                rowResult.EndsAt = endsAt;

                // 6. Format Compatibility Check
                if (!string.IsNullOrWhiteSpace(auditorium.ScreenTypeCode) && movie.SupportedFormats.Length > 0)
                {
                    bool isCompatible = movie.SupportedFormats.Any(f => 
                        string.Equals(f, auditorium.ScreenTypeCode, StringComparison.OrdinalIgnoreCase) ||
                        (string.Equals(auditorium.ScreenTypeCode, "STANDARD", StringComparison.OrdinalIgnoreCase) && 
                         (string.Equals(f, "2D", StringComparison.OrdinalIgnoreCase) || string.Equals(f, "STANDARD", StringComparison.OrdinalIgnoreCase))));

                    if (!isCompatible)
                    {
                        rowResult.Status = "invalid_format";
                        rowResult.Error = $"Movie '{movie.Title}' does not support screen format '{auditorium.ScreenTypeCode}'.";
                        results.Add(rowResult);
                        continue;
                    }
                }

                // 7. Check Overlap with DB
                var dbCollision = await repository.CheckShowtimeCollisionAsync(audId, startsAt, endsAt, null);
                if (dbCollision != null)
                {
                    rowResult.Status = "conflict";
                    rowResult.Error = $"Overlaps with existing scheduled showtime ({dbCollision.MovieTitle}: {dbCollision.StartsAt:s} to {dbCollision.EndsAt:s})";
                    results.Add(rowResult);
                    continue;
                }

                // 8. Check Overlap with Earlier Rows in THIS Import Batch
                var batchConflict = validBatches.FirstOrDefault(b => 
                    b.AuditoriumId == audId && 
                    b.StartsAt < endsAt && 
                    startsAt < b.EndsAt);

                if (batchConflict != null)
                {
                    rowResult.Status = "conflict";
                    rowResult.Error = $"Overlaps with Row #{batchConflict.RowNumber} in this same import batch ({batchConflict.StartsAt:s} to {batchConflict.EndsAt:s})";
                    results.Add(rowResult);
                    continue;
                }

                rowResult.Status = "valid";
                validBatches.Add(rowResult);
                results.Add(rowResult);
            }

            int validCount = results.Count(r => r.Status == "valid");
            int conflictCount = results.Count(r => r.Status == "conflict");
            int errorCount = results.Count(r => r.Status != "valid" && r.Status != "conflict");

            var report = new BulkImportResult
            {
                Executed = false,
                TotalRows = results.Count,
                ValidCount = validCount,
                ConflictCount = conflictCount,
                ErrorCount = errorCount,
                Results = results
            };

            if (shouldExecute)
            {
                if (conflictCount > 0 || errorCount > 0)
                {
                    return Results.BadRequest(new
                    {
                        error = "Bulk import aborted: cannot execute batch commit while conflicting or invalid rows exist.",
                        summary = report
                    });
                }

                // Atomic transaction execution of all rows
                try
                {
                    await repository.ExecuteBulkShowtimeImportAsync(validBatches);
                    report.Executed = true;
                    await cache.RemoveAsync("catalog:showtimes");
                }
                catch (PostgresException ex) when (ex.SqlState == "23P01")
                {
                    return Results.Conflict(new
                    {
                        error = "Bulk import failed during database commit: concurrent scheduling collision occurred.",
                        summary = report
                    });
                }
            }

            return Results.Ok(report);
        })
        .RequireAuthorization(policy => policy.RequireRole("content_manager", "branch_manager", "super_admin", "system_admin"))
        .WithSummary("Bulk Import Showtimes CSV")
        .WithDescription("Accepts CSV showtime schedules and supports dry-run validation reporting row-level errors (valid, conflict, invalid_movie, invalid_auditorium) and atomic batch commit.");

        // =========================================================================
        // Module 2: Seat Blocks & Maintenance Holds
        // =========================================================================

        admin.MapGet("/showtimes/{showtimeId:guid}/seat-holds", async (
            Guid showtimeId,
            ICatalogRepository repository) =>
        {
            var showtime = await repository.GetAdminShowtimeByIdAsync(showtimeId);
            if (showtime == null)
            {
                return Results.NotFound(new { error = $"Showtime {showtimeId} not found." });
            }

            var blocks = await repository.GetSeatBlocksAsync(showtimeId, showtime.AuditoriumId);
            return Results.Ok(blocks);
        })
        .RequireAuthorization(policy => policy.RequireRole("content_manager", "branch_manager", "super_admin", "system_admin"))
        .WithSummary("List Showtime Seat Blocks")
        .WithDescription("Retrieves blocked seats and maintenance holds for a specific showtime.");

        admin.MapPost("/showtimes/{showtimeId:guid}/seat-holds", async (
            HttpContext context,
            Guid showtimeId,
            [FromBody] CreateSeatHoldAdminRequest request,
            ICatalogRepository repository,
            IDistributedCache cache) =>
        {
            if (request.SeatIds == null || request.SeatIds.Count == 0)
            {
                return Results.BadRequest(new { error = "At least one seatId must be provided." });
            }

            var showtime = await repository.GetAdminShowtimeByIdAsync(showtimeId);
            if (showtime == null)
            {
                return Results.NotFound(new { error = $"Showtime {showtimeId} not found." });
            }

            var (callerBranchId, isSuperAdmin, isContentManager) = GetCallerContext(context);
            if (!isSuperAdmin && !isContentManager && callerBranchId.HasValue && showtime.BranchId != callerBranchId.Value)
            {
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Cannot block seats of a different branch.");
            }

            // Verify seats are not already booked/confirmed
            var bookedSeats = await repository.GetBookedSeatIdsAsync(showtimeId);
            var conflictingSeats = request.SeatIds.Where(s => bookedSeats.Contains(s)).ToList();
            if (conflictingSeats.Count > 0)
            {
                return Results.Conflict(new
                {
                    error = "One or more seats are already confirmed/sold and cannot be blocked.",
                    conflictingSeatIds = conflictingSeats
                });
            }

            var callerUserId = GetCallerUserId(context);
            var reason = string.IsNullOrWhiteSpace(request.Reason) ? "maintenance" : request.Reason.Trim();

            var createdIds = await repository.CreateSeatBlocksAsync(
                showtime.AuditoriumId, showtimeId, request.SeatIds, reason, callerUserId);

            await cache.RemoveAsync($"catalog:seat-matrix:{showtimeId}");

            var blocks = await repository.GetSeatBlocksAsync(showtimeId, showtime.AuditoriumId);
            return Results.Created($"/api/v1/admin/showtimes/{showtimeId}/seat-holds", blocks);
        })
        .RequireAuthorization(policy => policy.RequireRole("content_manager", "branch_manager", "super_admin", "system_admin"))
        .WithSummary("Create Showtime Seat Holds")
        .WithDescription("Blocks specific seats for maintenance, VIP holds, or repairs without monetary reservation.");

        admin.MapDelete("/showtimes/{showtimeId:guid}/seat-holds/{seatId:guid}", async (
            HttpContext context,
            Guid showtimeId,
            Guid seatId,
            ICatalogRepository repository,
            IDistributedCache cache) =>
        {
            var showtime = await repository.GetAdminShowtimeByIdAsync(showtimeId);
            if (showtime == null)
            {
                return Results.NotFound(new { error = $"Showtime {showtimeId} not found." });
            }

            var (callerBranchId, isSuperAdmin, isContentManager) = GetCallerContext(context);
            if (!isSuperAdmin && !isContentManager && callerBranchId.HasValue && showtime.BranchId != callerBranchId.Value)
            {
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Cannot release seats of a different branch.");
            }

            var success = await repository.DeleteSeatBlockAsync(showtimeId, seatId);
            await cache.RemoveAsync($"catalog:seat-matrix:{showtimeId}");

            return Results.Ok(new
            {
                showtimeId,
                seatId,
                action = "unblocked",
                released = success
            });
        })
        .RequireAuthorization(policy => policy.RequireRole("content_manager", "branch_manager", "super_admin", "system_admin"))
        .WithSummary("Release Showtime Seat Hold")
        .WithDescription("Releases a maintenance hold on a seat, returning it to available status.");

        // =========================================================================
        // Module 3: Dynamic Pricing Cards, Ticket Types & Surcharges
        // =========================================================================

        // --- Ticket Types ---
        admin.MapGet("/pricing/ticket-types", async (ICatalogRepository repository) =>
        {
            var types = await repository.GetAllTicketTypesAsync();
            return Results.Ok(types);
        })
        .RequireAuthorization(policy => policy.RequireRole("content_manager", "branch_manager", "super_admin", "system_admin"))
        .WithSummary("List All Ticket Types")
        .WithDescription("Retrieves all ticket types (e.g. Adult, Child, Senior, Student) including active and inactive.");

        admin.MapPost("/pricing/ticket-types", async (CreateTicketTypeRequest req, ICatalogRepository repository) =>
        {
            if (string.IsNullOrWhiteSpace(req.Code) || string.IsNullOrWhiteSpace(req.Name))
            {
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Code and Name are required.");
            }
            try
            {
                var id = await repository.CreateTicketTypeAsync(req.Code, req.Name);
                return Results.Created($"/api/v1/admin/pricing/ticket-types/{id}", new { ticketTypeId = id, code = req.Code.ToUpperInvariant(), name = req.Name });
            }
            catch (PostgresException ex) when (ex.SqlState == "23505")
            {
                return Results.Conflict(new { error = $"Ticket type code '{req.Code}' already exists." });
            }
        })
        .RequireAuthorization(policy => policy.RequireRole("content_manager", "super_admin", "system_admin"))
        .WithSummary("Create Ticket Type")
        .WithDescription("Creates a new ticket type (e.g. VIP_COUPLE, STUDENT).");

        admin.MapPatch("/pricing/ticket-types/{id:guid}/status", async (Guid id, [FromBody] UpdateTicketTypeRequest req, ICatalogRepository repository) =>
        {
            if (!req.IsActive.HasValue)
            {
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "IsActive boolean is required.");
            }
            var success = await repository.UpdateTicketTypeStatusAsync(id, req.IsActive.Value);
            return success ? Results.Ok(new { ticketTypeId = id, isActive = req.IsActive.Value }) : Results.NotFound(new { error = $"Ticket type {id} not found." });
        })
        .RequireAuthorization(policy => policy.RequireRole("content_manager", "super_admin", "system_admin"))
        .WithSummary("Toggle Ticket Type Status");

        // --- Price Cards ---
        admin.MapGet("/pricing/price-cards", async (ICatalogRepository repository) =>
        {
            var cards = await repository.GetPriceCardsAsync();
            return Results.Ok(cards);
        })
        .RequireAuthorization(policy => policy.RequireRole("content_manager", "branch_manager", "super_admin", "system_admin"))
        .WithSummary("List Price Cards")
        .WithDescription("Lists all price cards with entry counts.");

        admin.MapGet("/pricing/price-cards/{id:guid}", async (Guid id, ICatalogRepository repository) =>
        {
            var card = await repository.GetPriceCardDetailByIdAsync(id);
            return card != null ? Results.Ok(card) : Results.NotFound(new { error = $"Price card {id} not found." });
        })
        .RequireAuthorization(policy => policy.RequireRole("content_manager", "branch_manager", "super_admin", "system_admin"))
        .WithSummary("Get Price Card Details")
        .WithDescription("Gets a price card with its complete seat type x ticket type pricing matrix.");

        admin.MapPost("/pricing/price-cards", async (CreatePriceCardRequest req, ICatalogRepository repository) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name))
            {
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Price card name is required.");
            }
            var id = await repository.CreatePriceCardAsync(req.Name, req.Description, req.Entries);
            var created = await repository.GetPriceCardDetailByIdAsync(id);
            return Results.Created($"/api/v1/admin/pricing/price-cards/{id}", created);
        })
        .RequireAuthorization(policy => policy.RequireRole("content_manager", "super_admin", "system_admin"))
        .WithSummary("Create Price Card")
        .WithDescription("Creates a new price card and populates the seat x ticket pricing matrix.");

        admin.MapPut("/pricing/price-cards/{id:guid}", async (Guid id, UpdatePriceCardRequest req, ICatalogRepository repository) =>
        {
            var success = await repository.UpdatePriceCardAsync(id, req.Name, req.Description, req.IsActive, req.Entries);
            if (!success)
            {
                return Results.NotFound(new { error = $"Price card {id} not found." });
            }
            var updated = await repository.GetPriceCardDetailByIdAsync(id);
            return Results.Ok(updated);
        })
        .RequireAuthorization(policy => policy.RequireRole("content_manager", "super_admin", "system_admin"))
        .WithSummary("Update Price Card")
        .WithDescription("Updates price card metadata and/or replaces the pricing matrix entries.");

        admin.MapDelete("/pricing/price-cards/{id:guid}", async (Guid id, ICatalogRepository repository) =>
        {
            var success = await repository.DeletePriceCardAsync(id);
            return success ? Results.Ok(new { priceCardId = id, deleted = true }) : Results.NotFound(new { error = $"Price card {id} not found." });
        })
        .RequireAuthorization(policy => policy.RequireRole("super_admin", "system_admin"))
        .WithSummary("Delete Price Card")
        .WithDescription("Deletes or deactivates a price card if in use by showtimes.");

        // --- Dynamic Pricing Rules & Surcharges ---
        admin.MapGet("/pricing/rules", async ([FromQuery] bool? activeOnly, ICatalogRepository repository) =>
        {
            var rules = await repository.GetPricingRulesAsync(activeOnly);
            return Results.Ok(rules);
        })
        .RequireAuthorization(policy => policy.RequireRole("content_manager", "branch_manager", "super_admin", "system_admin"))
        .WithSummary("List Pricing Rules & Surcharges")
        .WithDescription("Lists dynamic pricing rules (Cinema Wednesday, Matinee discounts, Weekend surges, 3D/IMAX surcharges).");

        admin.MapPost("/pricing/rules", async (CreatePricingRuleRequest req, ICatalogRepository repository) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.RuleType))
            {
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Name and RuleType are required.");
            }
            var id = await repository.CreatePricingRuleAsync(req);
            var created = await repository.GetPricingRuleByIdAsync(id);
            return Results.Created($"/api/v1/admin/pricing/rules/{id}", created);
        })
        .RequireAuthorization(policy => policy.RequireRole("content_manager", "super_admin", "system_admin"))
        .WithSummary("Create Pricing Rule");

        admin.MapPut("/pricing/rules/{id:guid}", async (Guid id, UpdatePricingRuleRequest req, ICatalogRepository repository) =>
        {
            var success = await repository.UpdatePricingRuleAsync(id, req);
            if (!success)
            {
                return Results.NotFound(new { error = $"Pricing rule {id} not found." });
            }
            var updated = await repository.GetPricingRuleByIdAsync(id);
            return Results.Ok(updated);
        })
        .RequireAuthorization(policy => policy.RequireRole("content_manager", "super_admin", "system_admin"))
        .WithSummary("Update Pricing Rule");

        admin.MapPatch("/pricing/rules/{id:guid}/status", async (Guid id, [FromBody] UpdatePricingRuleRequest req, ICatalogRepository repository) =>
        {
            if (!req.IsActive.HasValue)
            {
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "IsActive boolean is required.");
            }
            var success = await repository.UpdatePricingRuleStatusAsync(id, req.IsActive.Value);
            return success ? Results.Ok(new { ruleId = id, isActive = req.IsActive.Value }) : Results.NotFound(new { error = $"Pricing rule {id} not found." });
        })
        .RequireAuthorization(policy => policy.RequireRole("content_manager", "super_admin", "system_admin"))
        .WithSummary("Toggle Pricing Rule Status");

        // --- Calculate Dynamic Price ---
        admin.MapPost("/pricing/calculate", async (CalculateTicketPriceRequest req, ICatalogRepository repository) =>
        {
            var result = await repository.CalculateTicketPriceAsync(req.PriceCardId, req.TicketTypeCode, req.SeatType, req.ShowtimeStartsAt, req.ScreenTypeCode);
            if (result == null)
            {
                return Results.NotFound(new { error = $"No price found for PriceCard {req.PriceCardId}, TicketType '{req.TicketTypeCode}', SeatType '{req.SeatType}'." });
            }
            return Results.Ok(result);
        })
        .RequireAuthorization(policy => policy.RequireRole("content_manager", "branch_manager", "cashier", "staff", "super_admin", "system_admin"))
        .WithSummary("Calculate Dynamic Ticket Price")
        .WithDescription("Calculates base price matrix and applies matinee, day-of-week, weekend surge, and screen format surcharges.");
    }


    private static (Guid? BranchId, bool IsSuperAdmin, bool IsContentManager) GetCallerContext(HttpContext context)
    {
        var isSuperAdmin = context.User.IsInRole("super_admin") || context.User.IsInRole("system_admin");
        var isContentManager = context.User.IsInRole("content_manager");
        var branchClaim = context.User.FindFirst("branchId")?.Value 
            ?? context.User.FindFirst("branch_id")?.Value;
        Guid? branchId = Guid.TryParse(branchClaim, out var b) ? b : null;
        return (branchId, isSuperAdmin, isContentManager);
    }

    private static Guid GetCallerUserId(HttpContext context)
    {
        var idClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? context.User.FindFirst("sub")?.Value;
        return Guid.TryParse(idClaim, out var uid) ? uid : Guid.Empty;
    }
}

