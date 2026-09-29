using System.Data;
using System.Data.Common;
using Catalog.Api.Models;
using Catalog.Api.Services;
using Cinema.Foundation.Data;
using Dapper;

namespace Catalog.Api.Repositories;

public class CatalogRepository : ICatalogRepository
{
    private readonly IDbConnectionFactory _dbFactory;

    public CatalogRepository(IDbConnectionFactory dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<IEnumerable<BranchDto>> GetActiveBranchesAsync()
    {
        using var conn = _dbFactory.CreateReadConnection();
        const string sql = @"
            SELECT branch_id as BranchId, code as Code, name as Name, address as Address, 
                   timezone as Timezone, hero_image_url as HeroImageUrl, is_active as IsActive
            FROM catalog.branches
            WHERE is_active = true
            ORDER BY name";

        return await conn.QueryAsync<BranchDto>(sql);
    }

    public async Task<IEnumerable<BranchImageDto>> GetBranchGalleryAsync(Guid branchId)
    {
        using var conn = _dbFactory.CreateReadConnection();
        const string sql = @"
            SELECT image_id as ImageId, branch_id as BranchId, image_url as ImageUrl,
                   caption as Caption, display_order as DisplayOrder, is_primary as IsPrimary
            FROM catalog.branch_images
            WHERE branch_id = @BranchId
            ORDER BY display_order, created_at";

        return await conn.QueryAsync<BranchImageDto>(sql, new { BranchId = branchId });
    }

    public async Task<IEnumerable<MovieDto>> GetActiveMoviesAsync()
    {
        using var conn = _dbFactory.CreateReadConnection();
        const string sql = @"
            SELECT movie_id as MovieId, title as Title, duration_minutes as DurationMinutes, 
                   genre as Genre, classification as Classification,
                   COALESCE(age_rating, classification, 'G') as CensorRating,
                   COALESCE(release_status, 'now_showing') as ReleaseStatus,
                   ARRAY['STANDARD']::text[] as SupportedFormats,
                   COALESCE(rating, 8.0) as Rating,
                   release_date as ReleaseDate, teaser_text as TeaserText, trailer_url as TrailerUrl,
                   poster_url as PosterUrl, backdrop_url as BackdropUrl,
                   audio_language as AudioLanguage, subtitle_language as SubtitleLanguage,
                   is_active as IsActive
            FROM catalog.movies
            WHERE is_active = true
            ORDER BY title";

        return await conn.QueryAsync<MovieDto>(sql);
    }

    public async Task<MovieDetailsDto?> GetMovieByIdAsync(Guid movieId)
    {
        using var conn = _dbFactory.CreateReadConnection();
        const string sql = @"
            SELECT movie_id as MovieId, title as Title, duration_minutes as DurationMinutes, 
                   genre as Genre, classification as Classification,
                   COALESCE(age_rating, classification, 'G') as CensorRating,
                   advisory_text as CensorAdvisory,
                   COALESCE(release_status, 'now_showing') as ReleaseStatus,
                   ARRAY['STANDARD']::text[] as SupportedFormats,
                   COALESCE(rating, 8.0) as Rating,
                   release_date as ReleaseDate, teaser_text as TeaserText, synopsis as Synopsis,
                   trailer_url as TrailerUrl, poster_url as PosterUrl, backdrop_url as BackdropUrl,
                   audio_language as AudioLanguage, subtitle_language as SubtitleLanguage,
                   director as Director, cast_members as CastMembers,
                   is_active as IsActive, created_at as CreatedAt
            FROM catalog.movies
            WHERE movie_id = @MovieId";

        return await conn.QueryFirstOrDefaultAsync<MovieDetailsDto>(sql, new { MovieId = movieId });
    }

    public async Task<IEnumerable<ScreenTypeDto>> GetScreenTypesAsync()
    {
        using var conn = _dbFactory.CreateReadConnection();
        const string sql = @"
            SELECT screen_type_id as ScreenTypeId, code as Code, name as Name, 
                   logo_url as LogoUrl, description as Description, is_active as IsActive
            FROM catalog.screen_types
            WHERE is_active = true
            ORDER BY name";

        return await conn.QueryAsync<ScreenTypeDto>(sql);
    }

    public async Task<IEnumerable<PromotionDto>> GetActivePromotionsAsync()
    {
        using var conn = _dbFactory.CreateReadConnection();
        const string sql = @"
            SELECT promotion_id as PromotionId, title as Title, subtitle as Subtitle,
                   poster_url as PosterUrl, banner_url as BannerUrl, content_text as ContentText,
                   discount_type as DiscountType, discount_value as DiscountValue, promo_code as PromoCode,
                   starts_at as StartsAt, ends_at as EndsAt, is_active as IsActive
            FROM catalog.promotions
            WHERE is_active = true AND now() BETWEEN starts_at AND ends_at
            ORDER BY starts_at DESC";

        return await conn.QueryAsync<PromotionDto>(sql);
    }

    public async Task<IEnumerable<GlobalNotificationDto>> GetActiveNotificationsAsync()
    {
        using var conn = _dbFactory.CreateReadConnection();
        const string sql = @"
            SELECT notification_id as NotificationId, title as Title, message as Message,
                   type as Type, action_url as ActionUrl, starts_at as StartsAt,
                   expires_at as ExpiresAt, is_active as IsActive
            FROM catalog.global_notifications
            WHERE is_active = true AND now() BETWEEN starts_at AND expires_at
            ORDER BY starts_at DESC";

        return await conn.QueryAsync<GlobalNotificationDto>(sql);
    }

    public async Task<IEnumerable<ShowtimeDto>> GetShowtimesAsync(Guid? branchId, Guid? movieId)
    {
        using var conn = _dbFactory.CreateReadConnection();
        const string sql = @"
            SELECT s.showtime_id as ShowtimeId, s.movie_id as MovieId, a.branch_id as BranchId,
                   a.name as AuditoriumName, s.starts_at as StartsAt, s.ends_at as EndsAt,
                   s.base_price as BasePrice, s.price_card_id as PriceCardId, s.status as Status
            FROM catalog.showtimes s
            JOIN catalog.auditoriums a ON s.auditorium_id = a.auditorium_id
            WHERE (@BranchId IS NULL OR a.branch_id = @BranchId)
              AND (@MovieId IS NULL OR s.movie_id = @MovieId)
            ORDER BY s.starts_at";

        return await conn.QueryAsync<ShowtimeDto>(sql, new { BranchId = branchId, MovieId = movieId });
    }

    public async Task<ShowtimeInfo?> GetShowtimeInfoAsync(Guid showtimeId)
    {
        using var conn = _dbFactory.CreateReadConnection();
        const string sql = @"
            SELECT s.showtime_id as ShowtimeId, a.auditorium_id as AuditoriumId, a.name as AuditoriumName, 
                   a.capacity as Capacity, s.base_price as BasePrice, s.price_card_id as PriceCardId
            FROM catalog.showtimes s
            JOIN catalog.auditoriums a ON s.auditorium_id = a.auditorium_id
            WHERE s.showtime_id = @ShowtimeId";

        return await conn.QueryFirstOrDefaultAsync<ShowtimeInfo>(sql, new { ShowtimeId = showtimeId });
    }

    public async Task<IEnumerable<SeatInfo>> GetSeatsByAuditoriumAsync(Guid auditoriumId)
    {
        using var conn = _dbFactory.CreateReadConnection();
        const string sql = @"
            SELECT seat_id as SeatId, row_label as RowLabel, seat_number as SeatNumber, 
                   seat_type as SeatType, is_accessible as IsAccessible
            FROM catalog.seats
            WHERE auditorium_id = @AuditoriumId AND is_active = true
            ORDER BY row_label, seat_number";

        return await conn.QueryAsync<SeatInfo>(sql, new { AuditoriumId = auditoriumId });
    }

    public async Task<HashSet<Guid>> GetBookedSeatIdsAsync(Guid showtimeId)
    {
        using var conn = _dbFactory.CreateReadConnection();
        const string sql = "SELECT seat_id FROM reservations.confirmed_seats WHERE showtime_id = @ShowtimeId";
        var ids = await conn.QueryAsync<Guid>(sql, new { ShowtimeId = showtimeId });
        return ids.ToHashSet();
    }

    public async Task<dynamic?> GetAuditoriumLayoutAsync(Guid auditoriumId)
    {
        using var conn = _dbFactory.CreateReadConnection();
        const string sql = @"
            SELECT auditorium_id as AuditoriumId, branch_id as BranchId, seat_map::text as SeatMapJson
            FROM catalog.auditorium_layouts
            WHERE auditorium_id = @AuditoriumId";

        return await conn.QueryFirstOrDefaultAsync<dynamic>(sql, new { AuditoriumId = auditoriumId });
    }

    public async Task<IEnumerable<TicketTypeDto>> GetTicketTypesAsync()
    {
        using var conn = _dbFactory.CreateReadConnection();
        const string sql = @"
            SELECT ticket_type_id as TicketTypeId, code as Code, name as Name 
            FROM catalog.ticket_types 
            WHERE is_active = true 
            ORDER BY name";
        return await conn.QueryAsync<TicketTypeDto>(sql);
    }

    public async Task<IEnumerable<PriceCardEntryDto>> GetPricingMatrixAsync(Guid priceCardId)
    {
        using var conn = _dbFactory.CreateReadConnection();
        const string sql = @"
            SELECT ticket_type_id as TicketTypeId, seat_type as SeatType, price as Price
            FROM catalog.price_card_entries
            WHERE price_card_id = @PriceCardId";
        return await conn.QueryAsync<PriceCardEntryDto>(sql, new { PriceCardId = priceCardId });
    }

    public async Task<IEnumerable<MovieAutocompleteDto>> AutocompleteMoviesAsync(string query, int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Enumerable.Empty<MovieAutocompleteDto>();

        using var conn = _dbFactory.CreateReadConnection();
        const string sql = @"
            SELECT m.movie_id as MovieId, m.title as Title, m.poster_url as PosterUrl, 
                   m.genre as Genre, m.classification as Classification,
                   CASE 
                       WHEN m.title ILIKE @Pattern THEN 'title'
                       WHEN m.director ILIKE @Pattern THEN 'director'
                       WHEN m.cast_members ILIKE @Pattern THEN 'actor'
                       ELSE 'title'
                   END as MatchType,
                   CASE 
                       WHEN m.title ILIKE @Pattern THEN m.title
                       WHEN m.director ILIKE @Pattern THEN m.director
                       WHEN m.cast_members ILIKE @Pattern THEN m.cast_members
                       ELSE m.title
                   END as MatchedText
            FROM catalog.movies m
            WHERE m.is_active = true
              AND (m.title ILIKE @Pattern OR m.director ILIKE @Pattern OR m.cast_members ILIKE @Pattern)
            ORDER BY similarity(m.title, @RawQuery) DESC, m.title ASC
            LIMIT @Limit";

        return await conn.QueryAsync<MovieAutocompleteDto>(sql, new 
        { 
            Pattern = $"%{query}%", 
            RawQuery = query, 
            Limit = Math.Clamp(limit, 1, 20) 
        });
    }

    public async Task<PagedResult<MovieSearchResultDto>> SearchMoviesAsync(MovieSearchRequest request)
    {
        using var conn = _dbFactory.CreateReadConnection();
        var dynamicParams = new DynamicParameters();

        var movieConditions = new List<string> { "m.is_active = true" };

        if (!string.IsNullOrWhiteSpace(request.Q))
        {
            dynamicParams.Add("QPattern", $"%{request.Q.Trim()}%");
            movieConditions.Add("(m.title ILIKE @QPattern OR m.director ILIKE @QPattern OR m.cast_members ILIKE @QPattern)");
        }

        if (request.Genres != null && request.Genres.Length > 0)
        {
            var genreConditions = new List<string>();
            for (int i = 0; i < request.Genres.Length; i++)
            {
                var paramName = $"Genre_{i}";
                dynamicParams.Add(paramName, $"%{request.Genres[i].Trim()}%");
                genreConditions.Add($"m.genre ILIKE @{paramName}");
            }
            movieConditions.Add($"({string.Join(" OR ", genreConditions)})");
        }

        if (!string.IsNullOrWhiteSpace(request.AudioLanguage))
        {
            dynamicParams.Add("AudioLanguage", $"%{request.AudioLanguage.Trim()}%");
            movieConditions.Add("m.audio_language ILIKE @AudioLanguage");
        }

        if (!string.IsNullOrWhiteSpace(request.SubtitleLanguage))
        {
            dynamicParams.Add("SubtitleLanguage", $"%{request.SubtitleLanguage.Trim()}%");
            movieConditions.Add("m.subtitle_language ILIKE @SubtitleLanguage");
        }

        if (!string.IsNullOrWhiteSpace(request.AgeRating))
        {
            dynamicParams.Add("AgeRating", request.AgeRating.Trim());
            movieConditions.Add("m.classification = @AgeRating");
        }

        if (!string.IsNullOrWhiteSpace(request.DurationBucket))
        {
            switch (request.DurationBucket.ToLowerInvariant())
            {
                case "under_90":
                    movieConditions.Add("m.duration_minutes < 90");
                    break;
                case "90_120":
                    movieConditions.Add("m.duration_minutes BETWEEN 90 AND 120");
                    break;
                case "over_120":
                    movieConditions.Add("m.duration_minutes > 120");
                    break;
            }
        }

        if (!string.IsNullOrWhiteSpace(request.ReleaseStatus))
        {
            switch (request.ReleaseStatus.ToLowerInvariant())
            {
                case "now_showing":
                    movieConditions.Add("(m.release_date IS NULL OR m.release_date <= CURRENT_DATE)");
                    break;
                case "coming_soon":
                    movieConditions.Add("m.release_date > CURRENT_DATE");
                    break;
                case "advance_booking":
                    movieConditions.Add("m.release_date > CURRENT_DATE");
                    break;
            }
        }

        // Showtime-level filters if specified
        var showtimeConditions = new List<string>
        {
            "s.status = 'scheduled'",
            "s.starts_at > (NOW() + INTERVAL '5 minutes')" // Cutoff rule
        };

        if (request.BranchId.HasValue && request.BranchId.Value != Guid.Empty)
        {
            dynamicParams.Add("BranchId", request.BranchId.Value);
            showtimeConditions.Add("a.branch_id = @BranchId");
        }

        if (request.Formats != null && request.Formats.Length > 0)
        {
            var formatParams = new List<string>();
            for (int i = 0; i < request.Formats.Length; i++)
            {
                var paramName = $"Format_{i}";
                dynamicParams.Add(paramName, request.Formats[i].Trim());
                formatParams.Add($"@{paramName}");
            }
            showtimeConditions.Add($"stype.code IN ({string.Join(", ", formatParams)})");
        }

        if (!string.IsNullOrWhiteSpace(request.Date))
        {
            var dateStr = request.Date.Trim().ToLowerInvariant();
            if (dateStr == "today")
            {
                showtimeConditions.Add("s.starts_at::date = CURRENT_DATE");
            }
            else if (dateStr == "tomorrow")
            {
                showtimeConditions.Add("s.starts_at::date = (CURRENT_DATE + 1)");
            }
            else if (dateStr == "this_weekend")
            {
                showtimeConditions.Add("EXTRACT(DOW FROM s.starts_at) IN (0, 5, 6)");
            }
            else if (DateOnly.TryParse(request.Date, out var parsedDate))
            {
                dynamicParams.Add("TargetDate", parsedDate.ToDateTime(TimeOnly.MinValue));
                showtimeConditions.Add("s.starts_at::date = @TargetDate::date");
            }
        }

        if (!string.IsNullOrWhiteSpace(request.TimeSlot))
        {
            switch (request.TimeSlot.ToLowerInvariant())
            {
                case "morning":
                    showtimeConditions.Add("EXTRACT(HOUR FROM s.starts_at) < 12");
                    break;
                case "afternoon":
                    showtimeConditions.Add("EXTRACT(HOUR FROM s.starts_at) >= 12 AND EXTRACT(HOUR FROM s.starts_at) < 17");
                    break;
                case "evening":
                    showtimeConditions.Add("EXTRACT(HOUR FROM s.starts_at) >= 17 AND EXTRACT(HOUR FROM s.starts_at) < 21");
                    break;
                case "late_night":
                    showtimeConditions.Add("EXTRACT(HOUR FROM s.starts_at) >= 21");
                    break;
            }
        }

        // If showtime filters are active (e.g. branch, date, format), filter movies that have matching showtimes
        bool hasShowtimeFilters = request.BranchId.HasValue || 
                                  (request.Formats != null && request.Formats.Length > 0) || 
                                  !string.IsNullOrWhiteSpace(request.Date) || 
                                  !string.IsNullOrWhiteSpace(request.TimeSlot);

        if (hasShowtimeFilters)
        {
            var existsSql = $@"
                EXISTS (
                    SELECT 1 
                    FROM catalog.showtimes s
                    JOIN catalog.auditoriums a ON a.auditorium_id = s.auditorium_id
                    LEFT JOIN catalog.screen_types stype ON stype.screen_type_id = a.screen_type_id
                    WHERE s.movie_id = m.movie_id AND {string.Join(" AND ", showtimeConditions)}
                )";
            movieConditions.Add(existsSql);
        }

        var whereClause = string.Join(" AND ", movieConditions);

        // Sorting
        var orderBy = "m.title ASC";
        if (!string.IsNullOrWhiteSpace(request.SortBy))
        {
            switch (request.SortBy.ToLowerInvariant())
            {
                case "release_date":
                    orderBy = "m.release_date DESC NULLS LAST, m.title ASC";
                    break;
                case "rating":
                    orderBy = "m.classification ASC, m.title ASC";
                    break;
                case "title_asc":
                    orderBy = "m.title ASC";
                    break;
                case "soonest_showtime":
                    orderBy = "(SELECT MIN(st.starts_at) FROM catalog.showtimes st WHERE st.movie_id = m.movie_id AND st.status = 'scheduled' AND st.starts_at > NOW()) ASC NULLS LAST, m.title ASC";
                    break;
            }
        }

        // Count total matching movies
        var countSql = $"SELECT COUNT(*) FROM catalog.movies m WHERE {whereClause}";
        var totalCount = await conn.ExecuteScalarAsync<int>(countSql, dynamicParams);

        var page = Math.Max(1, request.Page ?? 1);
        var pageSize = Math.Clamp(request.PageSize ?? 20, 1, 50);
        var offset = (page - 1) * pageSize;

        dynamicParams.Add("Limit", pageSize);
        dynamicParams.Add("Offset", offset);

        // Fetch movies
        var moviesSql = $@"
            SELECT m.movie_id as MovieId, m.title as Title, m.duration_minutes as DurationMinutes,
                   m.genre as Genre, m.classification as Classification, 8.0 as Rating,
                   m.release_date as ReleaseDate, m.teaser_text as TeaserText, m.poster_url as PosterUrl,
                   m.backdrop_url as BackdropUrl, m.trailer_url as TrailerUrl,
                   m.audio_language as AudioLanguage, m.subtitle_language as SubtitleLanguage,
                   m.director as Director, m.cast_members as CastMembers
            FROM catalog.movies m
            WHERE {whereClause}
            ORDER BY {orderBy}
            LIMIT @Limit OFFSET @Offset";

        var movies = (await conn.QueryAsync<MovieSearchResultDto>(moviesSql, dynamicParams)).ToList();

        if (movies.Count == 0)
        {
            return new PagedResult<MovieSearchResultDto>
            {
                Items = Array.Empty<MovieSearchResultDto>(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        // Fetch showtimes with real-time seat availability CTE in one batch
        var movieIds = movies.Select(m => m.MovieId).ToList();
        dynamicParams.Add("MovieIds", movieIds);

        var showtimeWhere = string.Join(" AND ", showtimeConditions);
        var showtimesSql = $@"
            WITH showtime_stats AS (
                SELECT s.showtime_id,
                       s.movie_id,
                       s.auditorium_id,
                       a.name as auditorium_name,
                       a.hall_type,
                       a.hall_logo_url,
                       COALESCE(stype.code, 'STANDARD') as screen_type_code,
                       stype.logo_url as screen_type_logo_url,
                       b.branch_id,
                       b.name as branch_name,
                       s.starts_at,
                       s.ends_at,
                       s.base_price,
                       a.capacity as total_capacity,
                       GREATEST(0, a.capacity - COALESCE(cs.booked_count, 0)) as available_seats
                FROM catalog.showtimes s
                JOIN catalog.auditoriums a ON a.auditorium_id = s.auditorium_id
                JOIN catalog.branches b ON b.branch_id = a.branch_id
                LEFT JOIN catalog.screen_types stype ON stype.screen_type_id = a.screen_type_id
                LEFT JOIN (
                    SELECT showtime_id, COUNT(*) as booked_count
                    FROM reservations.confirmed_seats
                    GROUP BY showtime_id
                ) cs ON cs.showtime_id = s.showtime_id
                WHERE s.movie_id = ANY(@MovieIds) 
                  AND {showtimeWhere}
            )
            SELECT showtime_id as ShowtimeId,
                   auditorium_id as AuditoriumId,
                   auditorium_name as AuditoriumName,
                   hall_type as HallType,
                   hall_logo_url as HallLogoUrl,
                   screen_type_code as ScreenTypeCode,
                   screen_type_logo_url as ScreenTypeLogoUrl,
                   branch_id as BranchId,
                   branch_name as BranchName,
                   starts_at as StartsAt,
                   ends_at as EndsAt,
                   base_price as BasePrice,
                   total_capacity as TotalCapacity,
                   available_seats as AvailableSeats,
                   movie_id as MovieId
            FROM showtime_stats
            ORDER BY starts_at ASC";

        var showtimeRows = await conn.QueryAsync<dynamic>(showtimesSql, dynamicParams);
        var showtimesByMovie = showtimeRows
            .GroupBy(r => (Guid)r.movieid)
            .ToDictionary(
                g => g.Key,
                g => g.Select(r => new ShowtimeSearchResultDto
                {
                    ShowtimeId = r.showtimeid,
                    AuditoriumId = r.auditoriumid,
                    AuditoriumName = r.auditoriumname,
                    HallType = r.halltype,
                    HallLogoUrl = r.halllogourl,
                    ScreenTypeCode = r.screentypecode,
                    ScreenTypeLogoUrl = r.screentypelogourl,
                    BranchId = r.branchid,
                    BranchName = r.branchname,
                    StartsAt = r.startsat,
                    EndsAt = r.endsat,
                    BasePrice = r.baseprice,
                    TotalCapacity = r.totalcapacity,
                    AvailableSeats = r.availableseats
                }).ToList()
            );

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        foreach (var movie in movies)
        {
            if (showtimesByMovie.TryGetValue(movie.MovieId, out var sts))
            {
                movie.Showtimes = sts;
            }

            bool isFuture = movie.ReleaseDate.HasValue && movie.ReleaseDate.Value > today;
            if (isFuture && movie.Showtimes.Count == 0)
            {
                movie.IsBookable = false;
                movie.NotifyMeEnabled = true;
            }
            else
            {
                movie.IsBookable = movie.Showtimes.Any(s => !s.IsSoldOut);
                movie.NotifyMeEnabled = false;
            }
        }

        return new PagedResult<MovieSearchResultDto>
        {
            Items = movies,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    private sealed class ShowtimePillRow
    {
        public Guid ShowtimeId { get; set; }
        public Guid MovieId { get; set; }
        public DateTime StartsAt { get; set; }
        public DateTime EndsAt { get; set; }
        public string AuditoriumName { get; set; } = "";
        public string ScreenTypeCode { get; set; } = "STANDARD";
        public string? ScreenTypeLogoUrl { get; set; }
        public decimal BasePrice { get; set; }
        public int AvailableSeats { get; set; }
    }

    private sealed class MovieFormatRow
    {
        public Guid MovieId { get; set; }
        public string FormatCode { get; set; } = "STANDARD";
    }

    private sealed class RecommendedCandidateRow
    {
        public Guid MovieId { get; set; }
        public string Title { get; set; } = "";
        public int DurationMinutes { get; set; }
        public string? Genre { get; set; }
        public string? Classification { get; set; }
        public decimal Rating { get; set; }
        public DateOnly? ReleaseDate { get; set; }
        public string? TeaserText { get; set; }
        public string? PosterUrl { get; set; }
        public string? BackdropUrl { get; set; }
        public string? TrailerUrl { get; set; }
        public int Sales48h { get; set; }
        public int TodayShowsCount { get; set; }
        public int ActiveShowsCount { get; set; }
    }

    public async Task<CursorPagedResult<NowShowingMovieDto>> GetNowShowingMoviesAsync(NowShowingMoviesRequest request)
    {
        using var conn = _dbFactory.CreateReadConnection();
        var dynamicParams = new DynamicParameters();

        int limit = Math.Clamp(request.Limit ?? 20, 1, 50);
        int offset = request.Page.HasValue
            ? (Math.Max(1, request.Page.Value) - 1) * limit
            : CursorHelper.DecodeOffset(request.Cursor);

        dynamicParams.Add("BranchId", request.BranchId, DbType.Guid);
        dynamicParams.Add("FilterNoShowtimes", request.FilterNoShowtimes ?? false);
        dynamicParams.Add("Limit", limit);
        dynamicParams.Add("Offset", offset);

        const string countSql = @"
            WITH next_7days AS (
                SELECT s.movie_id, COUNT(*) as next7_count
                FROM catalog.showtimes s
                JOIN catalog.auditoriums a ON a.auditorium_id = s.auditorium_id
                WHERE s.status = 'scheduled'
                  AND s.starts_at > (NOW() + INTERVAL '5 minutes')
                  AND s.starts_at <= (NOW() + INTERVAL '7 days')
                  AND (CAST(@BranchId AS uuid) IS NULL OR a.branch_id = @BranchId)
                GROUP BY s.movie_id
            )
            SELECT COUNT(*)
            FROM catalog.movies m
            LEFT JOIN next_7days n7 ON n7.movie_id = m.movie_id
            WHERE m.is_active = true
              AND (m.release_date IS NULL OR m.release_date <= CURRENT_DATE)
              AND (@FilterNoShowtimes = false OR COALESCE(n7.next7_count, 0) > 0)";

        var totalCount = await conn.ExecuteScalarAsync<int>(countSql, dynamicParams);

        const string moviesSql = @"
            WITH today_shows AS (
                SELECT s.movie_id,
                       COUNT(*) as today_count
                FROM catalog.showtimes s
                JOIN catalog.auditoriums a ON a.auditorium_id = s.auditorium_id
                WHERE s.status = 'scheduled'
                  AND s.starts_at::date = CURRENT_DATE
                  AND s.starts_at > (NOW() + INTERVAL '5 minutes')
                  AND (CAST(@BranchId AS uuid) IS NULL OR a.branch_id = @BranchId)
                GROUP BY s.movie_id
            ),
            next_7days AS (
                SELECT s.movie_id,
                       COUNT(*) as next7_count,
                       MIN(s.starts_at) as soonest_showtime
                FROM catalog.showtimes s
                JOIN catalog.auditoriums a ON a.auditorium_id = s.auditorium_id
                WHERE s.status = 'scheduled'
                  AND s.starts_at > (NOW() + INTERVAL '5 minutes')
                  AND s.starts_at <= (NOW() + INTERVAL '7 days')
                  AND (CAST(@BranchId AS uuid) IS NULL OR a.branch_id = @BranchId)
                GROUP BY s.movie_id
            ),
            sales_48h AS (
                SELECT s.movie_id,
                       COUNT(cs.seat_id) as sales_count
                FROM reservations.confirmed_seats cs
                JOIN catalog.showtimes s ON s.showtime_id = cs.showtime_id
                WHERE cs.booked_at >= (NOW() - INTERVAL '48 hours')
                GROUP BY s.movie_id
            )
            SELECT m.movie_id as MovieId,
                   m.title as Title,
                   m.duration_minutes as DurationMinutes,
                   m.genre as Genre,
                   m.classification as Classification,
                   COALESCE(m.rating, 8.0) as Rating,
                   m.release_date as ReleaseDate,
                   m.teaser_text as TeaserText,
                   m.poster_url as PosterUrl,
                   m.backdrop_url as BackdropUrl,
                   m.trailer_url as TrailerUrl,
                   m.audio_language as AudioLanguage,
                   m.subtitle_language as SubtitleLanguage,
                   m.director as Director,
                   m.cast_members as CastMembers,
                   CASE WHEN COALESCE(ts.today_count, 0) > 0 THEN true ELSE false END as HasShowtimesToday,
                   CASE WHEN COALESCE(n7.next7_count, 0) > 0 THEN true ELSE false END as HasShowtimesNext7Days,
                   COALESCE(n7.next7_count, 0) as AvailableShowtimesCount,
                   n7.soonest_showtime as SoonestShowtime,
                   COALESCE(s48.sales_count, 0) as TicketSalesLast48h,
                   CASE WHEN COALESCE(n7.next7_count, 0) > 0 THEN true ELSE false END as IsBookable
            FROM catalog.movies m
            LEFT JOIN today_shows ts ON ts.movie_id = m.movie_id
            LEFT JOIN next_7days n7 ON n7.movie_id = m.movie_id
            LEFT JOIN sales_48h s48 ON s48.movie_id = m.movie_id
            WHERE m.is_active = true
              AND (m.release_date IS NULL OR m.release_date <= CURRENT_DATE)
              AND (@FilterNoShowtimes = false OR COALESCE(n7.next7_count, 0) > 0)
            ORDER BY 
                (CASE WHEN COALESCE(ts.today_count, 0) > 0 THEN 1 ELSE 0 END) DESC,
                (CASE WHEN COALESCE(n7.next7_count, 0) > 0 THEN 1 ELSE 0 END) DESC,
                COALESCE(s48.sales_count, 0) DESC,
                m.release_date DESC NULLS LAST,
                m.movie_id ASC
            LIMIT @Limit OFFSET @Offset";

        var movies = (await conn.QueryAsync<NowShowingMovieDto>(moviesSql, dynamicParams)).ToList();

        if (movies.Count > 0)
        {
            var movieIds = movies.Select(m => m.MovieId).ToList();
            dynamicParams.Add("MovieIds", movieIds);

            const string pillsSql = @"
                SELECT s.showtime_id as ShowtimeId,
                       s.movie_id as MovieId,
                       s.starts_at as StartsAt,
                       s.ends_at as EndsAt,
                       a.name as AuditoriumName,
                       COALESCE(stype.code, 'STANDARD') as ScreenTypeCode,
                       stype.logo_url as ScreenTypeLogoUrl,
                       s.base_price as BasePrice,
                       GREATEST(0, a.capacity - (
                           SELECT COUNT(*) 
                           FROM reservations.confirmed_seats cs 
                           WHERE cs.showtime_id = s.showtime_id
                       )) as AvailableSeats
                FROM catalog.showtimes s
                JOIN catalog.auditoriums a ON a.auditorium_id = s.auditorium_id
                LEFT JOIN catalog.screen_types stype ON stype.screen_type_id = a.screen_type_id
                WHERE s.movie_id = ANY(@MovieIds)
                  AND s.status = 'scheduled'
                  AND s.starts_at::date = CURRENT_DATE
                  AND s.starts_at > (NOW() + INTERVAL '5 minutes')
                  AND (CAST(@BranchId AS uuid) IS NULL OR a.branch_id = @BranchId)
                ORDER BY s.starts_at ASC";

            var pills = await conn.QueryAsync<ShowtimePillRow>(pillsSql, dynamicParams);
            var pillsByMovie = pills
                .GroupBy(p => p.MovieId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(p => new ShowtimePillDto
                    {
                        ShowtimeId = p.ShowtimeId,
                        StartsAt = p.StartsAt,
                        EndsAt = p.EndsAt,
                        AuditoriumName = p.AuditoriumName,
                        ScreenTypeCode = p.ScreenTypeCode,
                        ScreenTypeLogoUrl = p.ScreenTypeLogoUrl,
                        BasePrice = p.BasePrice,
                        AvailableSeats = p.AvailableSeats
                    }).ToList()
                );

            foreach (var m in movies)
            {
                if (pillsByMovie.TryGetValue(m.MovieId, out var moviePills))
                {
                    m.TodayShowtimes = moviePills;
                }
            }
        }

        bool hasMore = (offset + movies.Count) < totalCount;
        string? nextCursor = hasMore ? CursorHelper.EncodeOffset(offset + movies.Count) : null;
        int page = (offset / limit) + 1;

        return new CursorPagedResult<NowShowingMovieDto>
        {
            Items = movies,
            NextCursor = nextCursor,
            HasMore = hasMore,
            TotalCount = totalCount,
            Page = page,
            PageSize = limit
        };
    }

    public async Task<IReadOnlyList<FeaturedMovieDto>> GetFeaturedMoviesAsync(FeaturedMoviesRequest request)
    {
        using var conn = _dbFactory.CreateReadConnection();
        int limit = Math.Clamp(request.Limit ?? 5, 1, 10);

        var dynamicParams = new DynamicParameters();
        dynamicParams.Add("BranchId", request.BranchId, DbType.Guid);
        dynamicParams.Add("Limit", limit);

        const string sql = @"
            WITH sales_48h AS (
                SELECT s.movie_id,
                       COUNT(cs.seat_id) as sales_count
                FROM reservations.confirmed_seats cs
                JOIN catalog.showtimes s ON s.showtime_id = cs.showtime_id
                WHERE cs.booked_at >= (NOW() - INTERVAL '48 hours')
                GROUP BY s.movie_id
            ),
            next_shows AS (
                SELECT s.movie_id,
                       MIN(s.starts_at) as next_showtime,
                       COUNT(*) as active_showtimes
                FROM catalog.showtimes s
                JOIN catalog.auditoriums a ON a.auditorium_id = s.auditorium_id
                WHERE s.status = 'scheduled'
                  AND s.starts_at > (NOW() + INTERVAL '5 minutes')
                  AND (CAST(@BranchId AS uuid) IS NULL OR a.branch_id = @BranchId)
                GROUP BY s.movie_id
            )
            SELECT m.movie_id as MovieId,
                   m.title as Title,
                   m.duration_minutes as DurationMinutes,
                   m.genre as Genre,
                   m.classification as Classification,
                   COALESCE(m.rating, 8.0) as Rating,
                   m.release_date as ReleaseDate,
                   m.teaser_text as TeaserText,
                   m.synopsis as Synopsis,
                   m.poster_url as PosterUrl,
                   m.backdrop_url as BackdropUrl,
                   m.trailer_url as TrailerUrl,
                   m.audio_language as AudioLanguage,
                   m.subtitle_language as SubtitleLanguage,
                   m.banner_custom_tag as BannerCustomTag,
                   COALESCE(m.is_featured, false) as IsFeatured,
                   COALESCE(s48.sales_count, 0) as TicketSalesLast48h,
                   ns.next_showtime as NextShowtime,
                   CASE WHEN COALESCE(ns.active_showtimes, 0) > 0 THEN true ELSE false END as IsBookable
            FROM catalog.movies m
            LEFT JOIN sales_48h s48 ON s48.movie_id = m.movie_id
            LEFT JOIN next_shows ns ON ns.movie_id = m.movie_id
            WHERE m.is_active = true
              AND (m.backdrop_url IS NOT NULL OR m.poster_url IS NOT NULL)
            ORDER BY 
                COALESCE(m.is_featured, false) DESC,
                COALESCE(s48.sales_count, 0) DESC,
                COALESCE(m.rating, 8.0) DESC,
                m.release_date DESC NULLS LAST,
                m.title ASC
            LIMIT @Limit";

        var movies = (await conn.QueryAsync<FeaturedMovieDto>(sql, dynamicParams)).ToList();

        if (movies.Count > 0)
        {
            var movieIds = movies.Select(m => m.MovieId).ToList();
            dynamicParams.Add("MovieIds", movieIds);

            const string formatsSql = @"
                SELECT DISTINCT s.movie_id as MovieId, COALESCE(stype.code, 'STANDARD') as FormatCode
                FROM catalog.showtimes s
                JOIN catalog.auditoriums a ON a.auditorium_id = s.auditorium_id
                LEFT JOIN catalog.screen_types stype ON stype.screen_type_id = a.screen_type_id
                WHERE s.movie_id = ANY(@MovieIds)
                  AND s.status = 'scheduled'
                  AND s.starts_at > (NOW() + INTERVAL '5 minutes')
                  AND (CAST(@BranchId AS uuid) IS NULL OR a.branch_id = @BranchId)";

            var formatRows = await conn.QueryAsync<MovieFormatRow>(formatsSql, dynamicParams);
            var formatsByMovie = formatRows
                .GroupBy(r => r.MovieId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(r => r.FormatCode).ToList()
                );

            for (int i = 0; i < movies.Count; i++)
            {
                var movie = movies[i];
                movie.TrendingRank = i + 1;
                if (formatsByMovie.TryGetValue(movie.MovieId, out var fmts))
                {
                    movie.Formats = fmts;
                }
            }
        }

        return movies;
    }

    public async Task<CursorPagedResult<ComingSoonMovieDto>> GetComingSoonMoviesAsync(ComingSoonMoviesRequest request)
    {
        using var conn = _dbFactory.CreateReadConnection();
        var dynamicParams = new DynamicParameters();

        int limit = Math.Clamp(request.Limit ?? 20, 1, 50);
        int offset = request.Page.HasValue
            ? (Math.Max(1, request.Page.Value) - 1) * limit
            : CursorHelper.DecodeOffset(request.Cursor);

        dynamicParams.Add("BranchId", request.BranchId, DbType.Guid);
        dynamicParams.Add("Limit", limit);
        dynamicParams.Add("Offset", offset);

        const string countSql = @"
            SELECT COUNT(*)
            FROM catalog.movies m
            WHERE m.is_active = true
              AND m.release_date > CURRENT_DATE";

        var totalCount = await conn.ExecuteScalarAsync<int>(countSql, dynamicParams);

        const string moviesSql = @"
            WITH advance_shows AS (
                SELECT s.movie_id,
                       COUNT(*) as advance_count,
                       MIN(s.starts_at) as soonest_advance
                FROM catalog.showtimes s
                JOIN catalog.auditoriums a ON a.auditorium_id = s.auditorium_id
                WHERE s.status = 'scheduled'
                  AND s.starts_at > (NOW() + INTERVAL '5 minutes')
                  AND (CAST(@BranchId AS uuid) IS NULL OR a.branch_id = @BranchId)
                GROUP BY s.movie_id
            )
            SELECT m.movie_id as MovieId,
                   m.title as Title,
                   m.duration_minutes as DurationMinutes,
                   m.genre as Genre,
                   m.classification as Classification,
                   COALESCE(m.rating, 8.0) as Rating,
                   m.release_date as ReleaseDate,
                   m.teaser_text as TeaserText,
                   m.synopsis as Synopsis,
                   m.poster_url as PosterUrl,
                   m.backdrop_url as BackdropUrl,
                   m.trailer_url as TrailerUrl,
                   m.director as Director,
                   m.cast_members as CastMembers,
                   COALESCE(adv.advance_count, 0) as AdvanceShowtimesCount,
                   adv.soonest_advance as SoonestAdvanceShowtime,
                   CASE WHEN COALESCE(adv.advance_count, 0) > 0 THEN true ELSE false END as IsAdvanceBooking
            FROM catalog.movies m
            LEFT JOIN advance_shows adv ON adv.movie_id = m.movie_id
            WHERE m.is_active = true
              AND m.release_date > CURRENT_DATE
            ORDER BY 
                m.release_date ASC,
                m.title ASC,
                m.movie_id ASC
            LIMIT @Limit OFFSET @Offset";

        var movies = (await conn.QueryAsync<ComingSoonMovieDto>(moviesSql, dynamicParams)).ToList();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        foreach (var m in movies)
        {
            if (m.ReleaseDate.HasValue)
            {
                m.DaysUntilRelease = Math.Max(0, m.ReleaseDate.Value.DayNumber - today.DayNumber);
            }
            m.NotifyMeEnabled = !m.IsAdvanceBooking;
        }

        bool hasMore = (offset + movies.Count) < totalCount;
        string? nextCursor = hasMore ? CursorHelper.EncodeOffset(offset + movies.Count) : null;
        int page = (offset / limit) + 1;

        return new CursorPagedResult<ComingSoonMovieDto>
        {
            Items = movies,
            NextCursor = nextCursor,
            HasMore = hasMore,
            TotalCount = totalCount,
            Page = page,
            PageSize = limit
        };
    }

    public async Task<CursorPagedResult<RecommendedMovieDto>> GetRecommendedMoviesAsync(RecommendedMoviesRequest request)
    {
        using var conn = _dbFactory.CreateReadConnection();
        var dynamicParams = new DynamicParameters();

        int limit = Math.Clamp(request.Limit ?? 10, 1, 50);
        int offset = CursorHelper.DecodeOffset(request.Cursor);

        dynamicParams.Add("BranchId", request.BranchId, DbType.Guid);

        // 1. Gather personalized preferred genres from user's booking history if provided
        var preferredGenres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (request.Genres != null && request.Genres.Length > 0)
        {
            foreach (var g in request.Genres)
            {
                if (!string.IsNullOrWhiteSpace(g))
                {
                    preferredGenres.Add(g.Trim());
                }
            }
        }

        if (request.UserId.HasValue && request.UserId.Value != Guid.Empty)
        {
            const string userHistorySql = @"
                SELECT DISTINCT m.genre
                FROM reservations.reservations r
                JOIN catalog.showtimes s ON s.showtime_id = r.showtime_id
                JOIN catalog.movies m ON m.movie_id = s.movie_id
                WHERE r.customer_id = @UserId AND r.status = 'confirmed'";

            var historyGenres = await conn.QueryAsync<string>(userHistorySql, new { UserId = request.UserId.Value });
            foreach (var gStr in historyGenres)
            {
                if (string.IsNullOrWhiteSpace(gStr)) continue;
                var tokens = gStr.Split(new[] { '/', ',', '&' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var t in tokens)
                {
                    preferredGenres.Add(t.Trim());
                }
            }
        }

        // 2. Query candidates with 48h popularity and showtime metrics
        const string candidatesSql = @"
            WITH sales_48h AS (
                SELECT s.movie_id,
                       COUNT(cs.seat_id) as sales_count
                FROM reservations.confirmed_seats cs
                JOIN catalog.showtimes s ON s.showtime_id = cs.showtime_id
                WHERE cs.booked_at >= (NOW() - INTERVAL '48 hours')
                GROUP BY s.movie_id
            ),
            today_shows AS (
                SELECT s.movie_id,
                       COUNT(*) as today_count
                FROM catalog.showtimes s
                JOIN catalog.auditoriums a ON a.auditorium_id = s.auditorium_id
                WHERE s.status = 'scheduled'
                  AND s.starts_at::date = CURRENT_DATE
                  AND s.starts_at > (NOW() + INTERVAL '5 minutes')
                  AND (CAST(@BranchId AS uuid) IS NULL OR a.branch_id = @BranchId)
                GROUP BY s.movie_id
            ),
            active_shows AS (
                SELECT s.movie_id,
                       COUNT(*) as active_count
                FROM catalog.showtimes s
                JOIN catalog.auditoriums a ON a.auditorium_id = s.auditorium_id
                WHERE s.status = 'scheduled'
                  AND s.starts_at > (NOW() + INTERVAL '5 minutes')
                  AND (CAST(@BranchId AS uuid) IS NULL OR a.branch_id = @BranchId)
                GROUP BY s.movie_id
            )
            SELECT m.movie_id as MovieId,
                   m.title as Title,
                   m.duration_minutes as DurationMinutes,
                   m.genre as Genre,
                   m.classification as Classification,
                   COALESCE(m.rating, 8.0) as Rating,
                   m.release_date as ReleaseDate,
                   m.teaser_text as TeaserText,
                   m.poster_url as PosterUrl,
                   m.backdrop_url as BackdropUrl,
                   m.trailer_url as TrailerUrl,
                   COALESCE(s48.sales_count, 0) as Sales48h,
                   COALESCE(ts.today_count, 0) as TodayShowsCount,
                   COALESCE(ash.active_count, 0) as ActiveShowsCount
            FROM catalog.movies m
            LEFT JOIN sales_48h s48 ON s48.movie_id = m.movie_id
            LEFT JOIN today_shows ts ON ts.movie_id = m.movie_id
            LEFT JOIN active_shows ash ON ash.movie_id = m.movie_id
            WHERE m.is_active = true";

        var candidates = (await conn.QueryAsync<RecommendedCandidateRow>(candidatesSql, dynamicParams)).ToList();

        var todayDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var scoredList = new List<RecommendedMovieDto>();

        foreach (var c in candidates)
        {
            string genreStr = c.Genre ?? "";
            decimal rating = c.Rating;
            int sales48h = c.Sales48h;
            int todayShows = c.TodayShowsCount;
            int activeShows = c.ActiveShowsCount;
            DateOnly? releaseDate = c.ReleaseDate;

            var matchedGenres = new List<string>();
            if (preferredGenres.Count > 0)
            {
                foreach (var pref in preferredGenres)
                {
                    if (genreStr.Contains(pref, StringComparison.OrdinalIgnoreCase))
                    {
                        matchedGenres.Add(pref);
                    }
                }
            }

            // Algorithmic Scoring
            decimal score = 20.0m; // Baseline
            if (matchedGenres.Count > 0)
            {
                score += 35.0m + Math.Min(15.0m, (matchedGenres.Count - 1) * 5.0m);
            }

            // Ticket sales boost in last 48h (up to 20 pts)
            score += Math.Min(20.0m, sales48h * 4.0m);

            // Rating score (up to 20 pts)
            score += rating * 2.0m;

            // Showing today bonus
            if (todayShows > 0)
            {
                score += 15.0m;
            }

            // Recency bonus (only for recently released movies in past 45 days)
            if (releaseDate.HasValue)
            {
                int daysSinceRelease = todayDate.DayNumber - releaseDate.Value.DayNumber;
                if (daysSinceRelease >= 0 && daysSinceRelease <= 45)
                {
                    score += 8.0m;
                }
            }

            decimal finalScore = Math.Clamp(Math.Round(score, 1), 10.0m, 99.0m);

            string reason;
            if (matchedGenres.Count > 0)
            {
                reason = $"Because you like {string.Join(" & ", matchedGenres.Take(2))}";
            }
            else if (sales48h >= 5)
            {
                reason = "Trending in Theaters (High Demand)";
            }
            else if (rating >= 8.5m)
            {
                reason = $"Critically Acclaimed ({rating:F1}/10)";
            }
            else if (todayShows > 0)
            {
                reason = "Showing Today Near You";
            }
            else
            {
                reason = "Popular with Audiences";
            }

            scoredList.Add(new RecommendedMovieDto
            {
                MovieId = c.MovieId,
                Title = c.Title,
                DurationMinutes = c.DurationMinutes,
                Genre = c.Genre ?? "",
                Classification = c.Classification ?? "",
                Rating = rating,
                ReleaseDate = releaseDate,
                TeaserText = c.TeaserText,
                PosterUrl = c.PosterUrl,
                BackdropUrl = c.BackdropUrl,
                TrailerUrl = c.TrailerUrl,
                RecommendationReason = reason,
                MatchScore = finalScore,
                MatchedGenres = matchedGenres,
                IsBookable = activeShows > 0,
                HasShowtimesToday = todayShows > 0
            });
        }

        // Rank by MatchScore DESC, Rating DESC, MovieId ASC
        var ordered = scoredList
            .OrderByDescending(m => m.MatchScore)
            .ThenByDescending(m => m.Rating)
            .ThenBy(m => m.MovieId)
            .ToList();

        int totalCount = ordered.Count;
        var pagedItems = ordered.Skip(offset).Take(limit).ToList();

        bool hasMore = (offset + pagedItems.Count) < totalCount;
        string? nextCursor = hasMore ? CursorHelper.EncodeOffset(offset + pagedItems.Count) : null;
        int page = (offset / limit) + 1;

        return new CursorPagedResult<RecommendedMovieDto>
        {
            Items = pagedItems,
            NextCursor = nextCursor,
            HasMore = hasMore,
            TotalCount = totalCount,
            Page = page,
            PageSize = limit
        };
    }

    // =========================================================================
    // Milestone 5.2: Admin Movie Lifecycle
    // =========================================================================

    public async Task<IEnumerable<AdminMovieDetailsDto>> GetAdminMoviesAsync(
        string? search, string? releaseStatus, string? format, string? censorRating, bool? isActive, int page, int pageSize)
    {
        using var conn = _dbFactory.CreateReadConnection();
        var dynamicParams = new DynamicParameters();
        var conditions = new List<string>();

        if (!string.IsNullOrWhiteSpace(search))
        {
            dynamicParams.Add("SearchPattern", $"%{search.Trim()}%");
            conditions.Add("(m.title ILIKE @SearchPattern OR m.director ILIKE @SearchPattern OR m.cast_members ILIKE @SearchPattern)");
        }

        if (!string.IsNullOrWhiteSpace(releaseStatus))
        {
            dynamicParams.Add("ReleaseStatus", releaseStatus.Trim().ToLowerInvariant());
            conditions.Add("m.release_status = @ReleaseStatus");
        }

        if (!string.IsNullOrWhiteSpace(censorRating))
        {
            dynamicParams.Add("CensorRating", censorRating.Trim());
            conditions.Add("(m.age_rating = @CensorRating OR m.classification = @CensorRating)");
        }

        if (!string.IsNullOrWhiteSpace(format))
        {
            dynamicParams.Add("Format", format.Trim().ToUpperInvariant());
            conditions.Add("@Format = ANY(m.supported_formats)");
        }

        if (isActive.HasValue)
        {
            dynamicParams.Add("IsActive", isActive.Value);
            conditions.Add("m.is_active = @IsActive");
        }

        string whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
        int offset = Math.Max(0, (page - 1) * pageSize);
        dynamicParams.Add("PageSize", pageSize);
        dynamicParams.Add("Offset", offset);

        string sql = $@"
            SELECT m.movie_id as MovieId, m.title as Title, m.duration_minutes as DurationMinutes,
                   m.genre as Genre, m.classification as Classification, 
                   COALESCE(m.age_rating, m.classification, 'G') as CensorRating,
                   m.advisory_text as CensorAdvisory,
                   COALESCE(m.release_status, 'now_showing') as ReleaseStatus,
                   COALESCE(m.supported_formats, ARRAY['STANDARD']) as SupportedFormats,
                   COALESCE(m.rating, 8.0) as Rating, m.release_date as ReleaseDate,
                   m.teaser_text as TeaserText, m.synopsis as Synopsis,
                   m.trailer_url as TrailerUrl, m.poster_url as PosterUrl, m.backdrop_url as BackdropUrl,
                   m.audio_language as AudioLanguage, m.subtitle_language as SubtitleLanguage,
                   m.director as Director, m.cast_members as CastMembers,
                   m.is_active as IsActive, m.is_featured as IsFeatured,
                   m.banner_custom_tag as BannerCustomTag, m.created_at as CreatedAt
            FROM catalog.movies m
            {whereClause}
            ORDER BY m.created_at DESC
            LIMIT @PageSize OFFSET @Offset";

        return await conn.QueryAsync<AdminMovieDetailsDto>(sql, dynamicParams);
    }

    public async Task<int> GetAdminMoviesCountAsync(
        string? search, string? releaseStatus, string? format, string? censorRating, bool? isActive)
    {
        using var conn = _dbFactory.CreateReadConnection();
        var dynamicParams = new DynamicParameters();
        var conditions = new List<string>();

        if (!string.IsNullOrWhiteSpace(search))
        {
            dynamicParams.Add("SearchPattern", $"%{search.Trim()}%");
            conditions.Add("(m.title ILIKE @SearchPattern OR m.director ILIKE @SearchPattern OR m.cast_members ILIKE @SearchPattern)");
        }

        if (!string.IsNullOrWhiteSpace(releaseStatus))
        {
            dynamicParams.Add("ReleaseStatus", releaseStatus.Trim().ToLowerInvariant());
            conditions.Add("m.release_status = @ReleaseStatus");
        }

        if (!string.IsNullOrWhiteSpace(censorRating))
        {
            dynamicParams.Add("CensorRating", censorRating.Trim());
            conditions.Add("(m.age_rating = @CensorRating OR m.classification = @CensorRating)");
        }

        if (!string.IsNullOrWhiteSpace(format))
        {
            dynamicParams.Add("Format", format.Trim().ToUpperInvariant());
            conditions.Add("@Format = ANY(m.supported_formats)");
        }

        if (isActive.HasValue)
        {
            dynamicParams.Add("IsActive", isActive.Value);
            conditions.Add("m.is_active = @IsActive");
        }

        string whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
        string sql = $"SELECT COUNT(*) FROM catalog.movies m {whereClause}";

        return await conn.ExecuteScalarAsync<int>(sql, dynamicParams);
    }

    public async Task<AdminMovieDetailsDto?> GetAdminMovieByIdAsync(Guid movieId)
    {
        using var conn = _dbFactory.CreateReadConnection();
        const string sql = @"
            SELECT m.movie_id as MovieId, m.title as Title, m.duration_minutes as DurationMinutes,
                   m.genre as Genre, m.classification as Classification, 
                   COALESCE(m.age_rating, m.classification, 'G') as CensorRating,
                   m.advisory_text as CensorAdvisory,
                   COALESCE(m.release_status, 'now_showing') as ReleaseStatus,
                   COALESCE(m.supported_formats, ARRAY['STANDARD']) as SupportedFormats,
                   COALESCE(m.rating, 8.0) as Rating, m.release_date as ReleaseDate,
                   m.teaser_text as TeaserText, m.synopsis as Synopsis,
                   m.trailer_url as TrailerUrl, m.poster_url as PosterUrl, m.backdrop_url as BackdropUrl,
                   m.audio_language as AudioLanguage, m.subtitle_language as SubtitleLanguage,
                   m.director as Director, m.cast_members as CastMembers,
                   m.is_active as IsActive, m.is_featured as IsFeatured,
                   m.banner_custom_tag as BannerCustomTag, m.created_at as CreatedAt
            FROM catalog.movies m
            WHERE m.movie_id = @MovieId";

        return await conn.QueryFirstOrDefaultAsync<AdminMovieDetailsDto>(sql, new { MovieId = movieId });
    }

    public async Task<Guid> CreateMovieAsync(CreateMovieRequest request)
    {
        using var conn = _dbFactory.CreateConnection();
        const string sql = @"
            INSERT INTO catalog.movies (
                title, duration_minutes, genre, classification, age_rating, advisory_text,
                release_status, supported_formats, rating, release_date, teaser_text, synopsis,
                trailer_url, poster_url, backdrop_url, audio_language, subtitle_language,
                director, cast_members, is_active, is_featured, banner_custom_tag
            ) VALUES (
                @Title, @DurationMinutes, @Genre, @Classification, @CensorRating, @CensorAdvisory,
                @ReleaseStatus, @SupportedFormats, @Rating, @ReleaseDate, @TeaserText, @Synopsis,
                @TrailerUrl, @PosterUrl, @BackdropUrl, @AudioLanguage, @SubtitleLanguage,
                @Director, @CastMembers, true, @IsFeatured, @BannerCustomTag
            ) RETURNING movie_id;";

        var releaseStatus = !string.IsNullOrWhiteSpace(request.ReleaseStatus)
            ? request.ReleaseStatus.ToLowerInvariant()
            : (request.ReleaseDate.HasValue && request.ReleaseDate.Value > DateOnly.FromDateTime(DateTime.UtcNow) ? "coming_soon" : "now_showing");

        var formats = (request.SupportedFormats != null && request.SupportedFormats.Length > 0)
            ? request.SupportedFormats
            : new[] { "STANDARD" };

        var censorRating = !string.IsNullOrWhiteSpace(request.CensorRating)
            ? request.CensorRating
            : (!string.IsNullOrWhiteSpace(request.Classification) ? request.Classification : "G");

        return await conn.ExecuteScalarAsync<Guid>(sql, new
        {
            request.Title,
            request.DurationMinutes,
            Genre = request.Genre ?? "General",
            Classification = censorRating,
            CensorRating = censorRating,
            request.CensorAdvisory,
            ReleaseStatus = releaseStatus,
            SupportedFormats = formats,
            Rating = request.Rating ?? 8.0m,
            ReleaseDate = request.ReleaseDate.HasValue ? request.ReleaseDate.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
            request.TeaserText,
            request.Synopsis,
            request.TrailerUrl,
            request.PosterUrl,
            request.BackdropUrl,
            AudioLanguage = request.AudioLanguage ?? "Khmer",
            SubtitleLanguage = request.SubtitleLanguage ?? "English",
            request.Director,
            request.CastMembers,
            IsFeatured = request.IsFeatured ?? false,
            request.BannerCustomTag
        });
    }

    public async Task<bool> UpdateMovieAsync(Guid movieId, UpdateMovieRequest request)
    {
        using var conn = _dbFactory.CreateConnection();
        const string sql = @"
            UPDATE catalog.movies SET
                title = COALESCE(@Title, title),
                duration_minutes = COALESCE(@DurationMinutes, duration_minutes),
                genre = COALESCE(@Genre, genre),
                classification = COALESCE(@Classification, classification),
                age_rating = COALESCE(@CensorRating, age_rating),
                advisory_text = COALESCE(@CensorAdvisory, advisory_text),
                release_status = COALESCE(@ReleaseStatus, release_status),
                supported_formats = COALESCE(@SupportedFormats, supported_formats),
                rating = COALESCE(@Rating, rating),
                release_date = COALESCE(@ReleaseDate, release_date),
                teaser_text = COALESCE(@TeaserText, teaser_text),
                synopsis = COALESCE(@Synopsis, synopsis),
                trailer_url = COALESCE(@TrailerUrl, trailer_url),
                poster_url = COALESCE(@PosterUrl, poster_url),
                backdrop_url = COALESCE(@BackdropUrl, backdrop_url),
                audio_language = COALESCE(@AudioLanguage, audio_language),
                subtitle_language = COALESCE(@SubtitleLanguage, subtitle_language),
                director = COALESCE(@Director, director),
                cast_members = COALESCE(@CastMembers, cast_members),
                is_active = COALESCE(@IsActive, is_active),
                is_featured = COALESCE(@IsFeatured, is_featured),
                banner_custom_tag = COALESCE(@BannerCustomTag, banner_custom_tag)
            WHERE movie_id = @MovieId;";

        var rows = await conn.ExecuteAsync(sql, new
        {
            MovieId = movieId,
            request.Title,
            request.DurationMinutes,
            request.Genre,
            Classification = request.CensorRating ?? request.Classification,
            CensorRating = request.CensorRating ?? request.Classification,
            request.CensorAdvisory,
            ReleaseStatus = request.ReleaseStatus?.ToLowerInvariant(),
            request.SupportedFormats,
            request.Rating,
            ReleaseDate = request.ReleaseDate.HasValue ? request.ReleaseDate.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
            request.TeaserText,
            request.Synopsis,
            request.TrailerUrl,
            request.PosterUrl,
            request.BackdropUrl,
            request.AudioLanguage,
            request.SubtitleLanguage,
            request.Director,
            request.CastMembers,
            request.IsActive,
            request.IsFeatured,
            request.BannerCustomTag
        });

        return rows > 0;
    }

    public async Task<bool> UpdateMovieStatusAsync(Guid movieId, string releaseStatus, bool? isActive)
    {
        using var conn = _dbFactory.CreateConnection();
        const string sql = @"
            UPDATE catalog.movies SET
                release_status = @ReleaseStatus,
                is_active = COALESCE(@IsActive, is_active)
            WHERE movie_id = @MovieId;";

        var rows = await conn.ExecuteAsync(sql, new
        {
            MovieId = movieId,
            ReleaseStatus = releaseStatus.ToLowerInvariant(),
            IsActive = isActive
        });

        return rows > 0;
    }

    public async Task<bool> SoftDeleteMovieAsync(Guid movieId)
    {
        using var conn = _dbFactory.CreateConnection();
        const string sql = @"
            UPDATE catalog.movies SET 
                is_active = false, 
                release_status = 'ended' 
            WHERE movie_id = @MovieId;";

        var rows = await conn.ExecuteAsync(sql, new { MovieId = movieId });
        return rows > 0;
    }

    public async Task<bool> HasActiveShowtimesAsync(Guid movieId)
    {
        using var conn = _dbFactory.CreateReadConnection();
        const string sql = @"
            SELECT EXISTS (
                SELECT 1 FROM catalog.showtimes 
                WHERE movie_id = @MovieId 
                  AND status != 'cancelled' 
                  AND ends_at > NOW()
            );";

        return await conn.ExecuteScalarAsync<bool>(sql, new { MovieId = movieId });
    }

    // =========================================================================
    // Milestone 5.2: Collision-Safe Showtime Engine
    // =========================================================================

        public async Task<AuditoriumDetailsDto?> GetAuditoriumDetailsAsync(Guid auditoriumId)
    {
        using var conn = _dbFactory.CreateReadConnection();
        const string sql = @"
            SELECT a.auditorium_id as AuditoriumId, a.branch_id as BranchId, b.name as BranchName,
                   a.name as Name, a.capacity as Capacity, 
                   COALESCE(a.cleaning_buffer_minutes, 15) as CleaningBufferMinutes,
                   a.screen_type_id as ScreenTypeId, st.code as ScreenTypeCode
            FROM catalog.auditoriums a
            JOIN catalog.branches b ON a.branch_id = b.branch_id
            LEFT JOIN catalog.screen_types st ON a.screen_type_id = st.screen_type_id
            WHERE a.auditorium_id = @AuditoriumId;";

        return await conn.QueryFirstOrDefaultAsync<AuditoriumDetailsDto>(sql, new { AuditoriumId = auditoriumId });
    }

    public async Task<IEnumerable<AuditoriumDetailsDto>> GetAuditoriumsAsync(Guid? branchId)
    {
        using var conn = _dbFactory.CreateReadConnection();
        var sql = @"
            SELECT a.auditorium_id as AuditoriumId, a.branch_id as BranchId, 
                   a.name as Name, a.capacity as Capacity, 
                   COALESCE(st.code, 'STANDARD') as ScreenTypeCode, COALESCE(a.cleaning_buffer_minutes, 15) as CleaningBufferMinutes
            FROM catalog.auditoriums a
            LEFT JOIN catalog.screen_types st ON a.screen_type_id = st.screen_type_id
            WHERE (@BranchId IS NULL OR a.branch_id = @BranchId)
            ORDER BY a.name;";
        return await conn.QueryAsync<AuditoriumDetailsDto>(sql, new { BranchId = branchId });
    }

    public async Task<AdminShowtimeDto?> GetAdminShowtimeByIdAsync(Guid showtimeId)
    {
        using var conn = _dbFactory.CreateReadConnection();
        const string sql = @"
            SELECT s.showtime_id as ShowtimeId, s.movie_id as MovieId, m.title as MovieTitle,
                   m.duration_minutes as MovieDurationMinutes,
                   s.auditorium_id as AuditoriumId, a.name as AuditoriumName,
                   COALESCE(a.cleaning_buffer_minutes, 15) as CleaningBufferMinutes,
                   COALESCE(st.code, 'STANDARD') as ScreenTypeCode,
                   a.branch_id as BranchId, b.name as BranchName,
                   s.starts_at as StartsAt, s.ends_at as EndsAt,
                   s.base_price as BasePrice, s.price_card_id as PriceCardId,
                   s.status as Status, a.capacity as Capacity,
                   (SELECT count(*) FROM reservations.confirmed_seats cs WHERE cs.showtime_id = s.showtime_id) as BookedSeatsCount,
                   (SELECT count(*) FROM catalog.seat_blocks sb WHERE sb.showtime_id = s.showtime_id OR (sb.showtime_id IS NULL AND sb.auditorium_id = s.auditorium_id)) as BlockedSeatsCount,
                   s.created_at as CreatedAt
            FROM catalog.showtimes s
            JOIN catalog.movies m ON s.movie_id = m.movie_id
            JOIN catalog.auditoriums a ON s.auditorium_id = a.auditorium_id
            JOIN catalog.branches b ON a.branch_id = b.branch_id
            LEFT JOIN catalog.screen_types st ON a.screen_type_id = st.screen_type_id
            WHERE s.showtime_id = @ShowtimeId;";

        return await conn.QueryFirstOrDefaultAsync<AdminShowtimeDto>(sql, new { ShowtimeId = showtimeId });
    }

    public async Task<IEnumerable<AdminShowtimeDto>> GetAdminShowtimesAsync(
        Guid? branchId, Guid? auditoriumId, Guid? movieId, DateTimeOffset? fromDate, DateTimeOffset? toDate)
    {
        using var conn = _dbFactory.CreateReadConnection();
        const string sql = @"
            SELECT s.showtime_id as ShowtimeId, s.movie_id as MovieId, m.title as MovieTitle,
                   m.duration_minutes as MovieDurationMinutes,
                   s.auditorium_id as AuditoriumId, a.name as AuditoriumName,
                   COALESCE(a.cleaning_buffer_minutes, 15) as CleaningBufferMinutes,
                   COALESCE(st.code, 'STANDARD') as ScreenTypeCode,
                   a.branch_id as BranchId, b.name as BranchName,
                   s.starts_at as StartsAt, s.ends_at as EndsAt,
                   s.base_price as BasePrice, s.price_card_id as PriceCardId,
                   s.status as Status, a.capacity as Capacity,
                   (SELECT count(*) FROM reservations.confirmed_seats cs WHERE cs.showtime_id = s.showtime_id) as BookedSeatsCount,
                   (SELECT count(*) FROM catalog.seat_blocks sb WHERE sb.showtime_id = s.showtime_id OR (sb.showtime_id IS NULL AND sb.auditorium_id = s.auditorium_id)) as BlockedSeatsCount,
                   s.created_at as CreatedAt
            FROM catalog.showtimes s
            JOIN catalog.movies m ON s.movie_id = m.movie_id
            JOIN catalog.auditoriums a ON s.auditorium_id = a.auditorium_id
            JOIN catalog.branches b ON a.branch_id = b.branch_id
            LEFT JOIN catalog.screen_types st ON a.screen_type_id = st.screen_type_id
            WHERE (@BranchId IS NULL OR a.branch_id = @BranchId)
              AND (@AuditoriumId IS NULL OR s.auditorium_id = @AuditoriumId)
              AND (@MovieId IS NULL OR s.movie_id = @MovieId)
              AND (@FromDate IS NULL OR s.starts_at >= @FromDate)
              AND (@ToDate IS NULL OR s.starts_at <= @ToDate)
            ORDER BY s.starts_at;";

        return await conn.QueryAsync<AdminShowtimeDto>(sql, new
        {
            BranchId = branchId,
            AuditoriumId = auditoriumId,
            MovieId = movieId,
            FromDate = fromDate,
            ToDate = toDate
        });
    }

    public async Task<ShowtimeCollisionDto?> CheckShowtimeCollisionAsync(
        Guid auditoriumId, DateTimeOffset startsAt, DateTimeOffset endsAt, Guid? excludeShowtimeId)
    {
        using var conn = _dbFactory.CreateReadConnection();
        const string sql = @"
            SELECT s.showtime_id as ShowtimeId, s.starts_at as StartsAt, s.ends_at as EndsAt, m.title as MovieTitle
            FROM catalog.showtimes s
            JOIN catalog.movies m ON s.movie_id = m.movie_id
            WHERE s.auditorium_id = @AuditoriumId
              AND s.status != 'cancelled'
              AND (@ExcludeShowtimeId IS NULL OR s.showtime_id != @ExcludeShowtimeId)
              AND tstzrange(s.starts_at, s.ends_at, '[)') && tstzrange(@StartsAt, @EndsAt, '[)')
            LIMIT 1;";

        return await conn.QueryFirstOrDefaultAsync<ShowtimeCollisionDto>(sql, new
        {
            AuditoriumId = auditoriumId,
            StartsAt = startsAt,
            EndsAt = endsAt,
            ExcludeShowtimeId = excludeShowtimeId
        });
    }

    public async Task<Guid> CreateShowtimeAsync(
        Guid movieId, Guid auditoriumId, DateTimeOffset startsAt, DateTimeOffset endsAt, decimal basePrice, Guid? priceCardId)
    {
        using var conn = _dbFactory.CreateConnection();
        const string sql = @"
            INSERT INTO catalog.showtimes (
                movie_id, auditorium_id, starts_at, ends_at, base_price, price_card_id, status
            ) VALUES (
                @MovieId, @AuditoriumId, @StartsAt, @EndsAt, @BasePrice, @PriceCardId, 'scheduled'
            ) RETURNING showtime_id;";

        return await conn.ExecuteScalarAsync<Guid>(sql, new
        {
            MovieId = movieId,
            AuditoriumId = auditoriumId,
            StartsAt = startsAt,
            EndsAt = endsAt,
            BasePrice = basePrice,
            PriceCardId = priceCardId
        });
    }

    public async Task<bool> UpdateShowtimeAsync(
        Guid showtimeId, DateTimeOffset startsAt, DateTimeOffset endsAt, decimal basePrice, Guid? priceCardId, string? status)
    {
        using var conn = _dbFactory.CreateConnection();
        const string sql = @"
            UPDATE catalog.showtimes SET
                starts_at = @StartsAt,
                ends_at = @EndsAt,
                base_price = @BasePrice,
                price_card_id = COALESCE(@PriceCardId, price_card_id),
                status = COALESCE(@Status, status)
            WHERE showtime_id = @ShowtimeId;";

        var rows = await conn.ExecuteAsync(sql, new
        {
            ShowtimeId = showtimeId,
            StartsAt = startsAt,
            EndsAt = endsAt,
            BasePrice = basePrice,
            PriceCardId = priceCardId,
            Status = status
        });

        return rows > 0;
    }

    public async Task<bool> CancelShowtimeAsync(Guid showtimeId, string? reason)
    {
        using var conn = _dbFactory.CreateConnection();
        const string sql = @"
            UPDATE catalog.showtimes SET 
                status = 'cancelled' 
            WHERE showtime_id = @ShowtimeId;";

        var rows = await conn.ExecuteAsync(sql, new { ShowtimeId = showtimeId });
        return rows > 0;
    }

    public async Task<int> GetConfirmedBookingsCountForShowtimeAsync(Guid showtimeId)
    {
        using var conn = _dbFactory.CreateReadConnection();
        const string sql = "SELECT COUNT(*) FROM reservations.confirmed_seats WHERE showtime_id = @ShowtimeId;";
        return await conn.ExecuteScalarAsync<int>(sql, new { ShowtimeId = showtimeId });
    }

    // =========================================================================
    // Milestone 5.2: Seat Blocks & Maintenance Holds
    // =========================================================================

    public async Task<HashSet<Guid>> GetBlockedSeatIdsAsync(Guid showtimeId, Guid auditoriumId)
    {
        using var conn = _dbFactory.CreateReadConnection();
        const string sql = @"
            SELECT seat_id 
            FROM catalog.seat_blocks 
            WHERE showtime_id = @ShowtimeId OR (showtime_id IS NULL AND auditorium_id = @AuditoriumId);";

        var ids = await conn.QueryAsync<Guid>(sql, new { ShowtimeId = showtimeId, AuditoriumId = auditoriumId });
        return ids.ToHashSet();
    }

    public async Task<IEnumerable<SeatBlockDto>> GetSeatBlocksAsync(Guid showtimeId, Guid auditoriumId)
    {
        using var conn = _dbFactory.CreateReadConnection();
        const string sql = @"
            SELECT sb.block_id as BlockId, sb.auditorium_id as AuditoriumId, sb.showtime_id as ShowtimeId,
                   sb.seat_id as SeatId, s.row_label as RowLabel, s.seat_number as SeatNumber,
                   s.seat_type as SeatType, sb.reason as Reason, sb.blocked_by as BlockedBy,
                   sb.created_at as CreatedAt
            FROM catalog.seat_blocks sb
            JOIN catalog.seats s ON sb.seat_id = s.seat_id
            WHERE sb.showtime_id = @ShowtimeId OR (sb.showtime_id IS NULL AND sb.auditorium_id = @AuditoriumId)
            ORDER BY s.row_label, s.seat_number;";

        return await conn.QueryAsync<SeatBlockDto>(sql, new { ShowtimeId = showtimeId, AuditoriumId = auditoriumId });
    }

    public async Task<List<Guid>> CreateSeatBlocksAsync(
        Guid auditoriumId, Guid? showtimeId, IEnumerable<Guid> seatIds, string reason, Guid blockedBy)
    {
        using var conn = _dbFactory.CreateConnection();
        if (conn.State != ConnectionState.Open)
        {
            if (conn is DbConnection dbConn) await dbConn.OpenAsync();
            else conn.Open();
        }

        using var tran = conn.BeginTransaction();
        const string sql = @"
            INSERT INTO catalog.seat_blocks (
                auditorium_id, showtime_id, seat_id, reason, blocked_by
            ) VALUES (
                @AuditoriumId, @ShowtimeId, @SeatId, @Reason, @BlockedBy
            ) RETURNING block_id;";

        var createdIds = new List<Guid>();
        foreach (var seatId in seatIds)
        {
            var id = await conn.ExecuteScalarAsync<Guid>(sql, new
            {
                AuditoriumId = auditoriumId,
                ShowtimeId = showtimeId,
                SeatId = seatId,
                Reason = reason,
                BlockedBy = blockedBy
            }, tran);
            createdIds.Add(id);
        }

        tran.Commit();
        return createdIds;
    }

    public async Task<bool> DeleteSeatBlockAsync(Guid? showtimeId, Guid seatId)
    {
        using var conn = _dbFactory.CreateConnection();
        const string sql = @"
            DELETE FROM catalog.seat_blocks
            WHERE (@ShowtimeId IS NULL OR showtime_id = @ShowtimeId) AND seat_id = @SeatId;";

        var rows = await conn.ExecuteAsync(sql, new { ShowtimeId = showtimeId, SeatId = seatId });
        return rows > 0;
    }

    // =========================================================================
    // Milestone 5.2: Bulk Showtime Importer
    // =========================================================================

    public async Task<List<BulkImportRowResult>> ExecuteBulkShowtimeImportAsync(List<BulkImportRowResult> validRows)
    {
        using var conn = _dbFactory.CreateConnection();
        if (conn.State != ConnectionState.Open)
        {
            if (conn is DbConnection dbConn) await dbConn.OpenAsync();
            else conn.Open();
        }

        using var tran = conn.BeginTransaction();
        const string sql = @"
            INSERT INTO catalog.showtimes (
                movie_id, auditorium_id, starts_at, ends_at, base_price, status
            ) VALUES (
                @MovieId, @AuditoriumId, @StartsAt, @EndsAt, @BasePrice, 'scheduled'
            ) RETURNING showtime_id;";

        foreach (var row in validRows)
        {
            if (row.MovieId.HasValue && row.AuditoriumId.HasValue && row.StartsAt.HasValue && row.EndsAt.HasValue && row.BasePrice.HasValue)
            {
                var id = await conn.ExecuteScalarAsync<Guid>(sql, new
                {
                    MovieId = row.MovieId.Value,
                    AuditoriumId = row.AuditoriumId.Value,
                    StartsAt = row.StartsAt.Value,
                    EndsAt = row.EndsAt.Value,
                    BasePrice = row.BasePrice.Value
                }, tran);
                row.ShowtimeId = id;
            }
        }

        tran.Commit();
        return validRows;
    }

    // =========================================================================
    // Milestone 5.3: Ticket Pricing Cards, Surcharges & Matrix
    // =========================================================================

    public async Task<IEnumerable<TicketTypeDto>> GetAllTicketTypesAsync()
    {
        using var conn = _dbFactory.CreateReadConnection();
        const string sql = @"
            SELECT ticket_type_id as TicketTypeId, code as Code, name as Name, is_active as IsActive
            FROM catalog.ticket_types
            ORDER BY name";
        return await conn.QueryAsync<TicketTypeDto>(sql);
    }

    public async Task<Guid> CreateTicketTypeAsync(string code, string name)
    {
        using var conn = _dbFactory.CreateConnection();
        const string sql = @"
            INSERT INTO catalog.ticket_types (code, name, is_active)
            VALUES (UPPER(@Code), @Name, true)
            RETURNING ticket_type_id;";
        return await conn.ExecuteScalarAsync<Guid>(sql, new { Code = code.Trim(), Name = name.Trim() });
    }

    public async Task<bool> UpdateTicketTypeStatusAsync(Guid ticketTypeId, bool isActive)
    {
        using var conn = _dbFactory.CreateConnection();
        const string sql = @"
            UPDATE catalog.ticket_types
            SET is_active = @IsActive
            WHERE ticket_type_id = @TicketTypeId;";
        var affected = await conn.ExecuteAsync(sql, new { TicketTypeId = ticketTypeId, IsActive = isActive });
        return affected > 0;
    }

    public async Task<IEnumerable<PriceCardSummaryDto>> GetPriceCardsAsync()
    {
        using var conn = _dbFactory.CreateReadConnection();
        const string sql = @"
            SELECT pc.price_card_id as PriceCardId,
                   pc.name as Name,
                   pc.description as Description,
                   pc.is_active as IsActive,
                   COUNT(pce.entry_id)::int as EntryCount
            FROM catalog.price_cards pc
            LEFT JOIN catalog.price_card_entries pce ON pc.price_card_id = pce.price_card_id
            GROUP BY pc.price_card_id, pc.name, pc.description, pc.is_active
            ORDER BY pc.name";
        return await conn.QueryAsync<PriceCardSummaryDto>(sql);
    }

    public async Task<PriceCardDetailDto?> GetPriceCardDetailByIdAsync(Guid priceCardId)
    {
        using var conn = _dbFactory.CreateReadConnection();
        const string cardSql = @"
            SELECT price_card_id as PriceCardId, name as Name, description as Description, is_active as IsActive
            FROM catalog.price_cards
            WHERE price_card_id = @PriceCardId;";
        var card = await conn.QuerySingleOrDefaultAsync<PriceCardDetailDto>(cardSql, new { PriceCardId = priceCardId });
        if (card == null) return null;

        const string entriesSql = @"
            SELECT pce.entry_id as EntryId,
                   pce.price_card_id as PriceCardId,
                   pce.ticket_type_id as TicketTypeId,
                   tt.code as TicketTypeCode,
                   tt.name as TicketTypeName,
                   pce.seat_type as SeatType,
                   pce.price as Price
            FROM catalog.price_card_entries pce
            JOIN catalog.ticket_types tt ON pce.ticket_type_id = tt.ticket_type_id
            WHERE pce.price_card_id = @PriceCardId
            ORDER BY tt.name, pce.seat_type";
        var entries = await conn.QueryAsync<PriceCardEntryDetailDto>(entriesSql, new { PriceCardId = priceCardId });
        card.Entries = entries.ToList();
        return card;
    }

    public async Task<Guid> CreatePriceCardAsync(string name, string? description, List<PriceCardEntryInput> entries)
    {
        using var conn = _dbFactory.CreateConnection();
        if (conn.State != ConnectionState.Open)
        {
            if (conn is DbConnection dbConn) await dbConn.OpenAsync();
            else conn.Open();
        }
        using var tx = conn.BeginTransaction();
        try
        {
            const string cardSql = @"
                INSERT INTO catalog.price_cards (name, description, is_active)
                VALUES (@Name, @Description, true)
                RETURNING price_card_id;";
            var cardId = await conn.ExecuteScalarAsync<Guid>(cardSql, new { Name = name, Description = description }, tx);

            if (entries != null && entries.Count > 0)
            {
                const string entrySql = @"
                    INSERT INTO catalog.price_card_entries (price_card_id, ticket_type_id, seat_type, price)
                    VALUES (@PriceCardId, @TicketTypeId, @SeatType, @Price);";
                foreach (var e in entries)
                {
                    await conn.ExecuteAsync(entrySql, new
                    {
                        PriceCardId = cardId,
                        TicketTypeId = e.TicketTypeId,
                        SeatType = e.SeatType.ToLowerInvariant(),
                        Price = e.Price
                    }, tx);
                }
            }
            tx.Commit();
            return cardId;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<bool> UpdatePriceCardAsync(Guid priceCardId, string? name, string? description, bool? isActive, List<PriceCardEntryInput>? entries)
    {
        using var conn = _dbFactory.CreateConnection();
        if (conn.State != ConnectionState.Open)
        {
            if (conn is DbConnection dbConn) await dbConn.OpenAsync();
            else conn.Open();
        }
        using var tx = conn.BeginTransaction();
        try
        {
            const string updateCardSql = @"
                UPDATE catalog.price_cards
                SET name = COALESCE(@Name, name),
                   description = COALESCE(@Description, description),
                   is_active = COALESCE(@IsActive, is_active)
                WHERE price_card_id = @PriceCardId;";
            var affected = await conn.ExecuteAsync(updateCardSql, new
            {
                PriceCardId = priceCardId,
                Name = name,
                Description = description,
                IsActive = isActive
            }, tx);

            if (affected == 0)
            {
                tx.Rollback();
                return false;
            }

            if (entries != null)
            {
                const string deleteSql = "DELETE FROM catalog.price_card_entries WHERE price_card_id = @PriceCardId;";
                await conn.ExecuteAsync(deleteSql, new { PriceCardId = priceCardId }, tx);

                const string insertSql = @"
                    INSERT INTO catalog.price_card_entries (price_card_id, ticket_type_id, seat_type, price)
                    VALUES (@PriceCardId, @TicketTypeId, @SeatType, @Price);";
                foreach (var e in entries)
                {
                    await conn.ExecuteAsync(insertSql, new
                    {
                        PriceCardId = priceCardId,
                        TicketTypeId = e.TicketTypeId,
                        SeatType = e.SeatType.ToLowerInvariant(),
                        Price = e.Price
                    }, tx);
                }
            }
            tx.Commit();
            return true;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<bool> DeletePriceCardAsync(Guid priceCardId)
    {
        using var conn = _dbFactory.CreateConnection();
        // Check if showtimes reference this price card
        const string checkSql = "SELECT COUNT(*) FROM catalog.showtimes WHERE price_card_id = @PriceCardId AND status != 'cancelled';";
        var inUseCount = await conn.ExecuteScalarAsync<int>(checkSql, new { PriceCardId = priceCardId });
        if (inUseCount > 0)
        {
            // Soft-deactivate if in use
            const string softDel = "UPDATE catalog.price_cards SET is_active = false WHERE price_card_id = @PriceCardId;";
            return await conn.ExecuteAsync(softDel, new { PriceCardId = priceCardId }) > 0;
        }

        const string deleteSql = "DELETE FROM catalog.price_cards WHERE price_card_id = @PriceCardId;";
        return await conn.ExecuteAsync(deleteSql, new { PriceCardId = priceCardId }) > 0;
    }

    public async Task<IEnumerable<PricingRuleDto>> GetPricingRulesAsync(bool? activeOnly)
    {
        using var conn = _dbFactory.CreateReadConnection();
        const string sql = @"
            SELECT rule_id as RuleId,
                   name as Name,
                   rule_type as RuleType,
                   day_of_week as DayOfWeek,
                   start_time as StartTime,
                   end_time as EndTime,
                   screen_type_code as ScreenTypeCode,
                   adjustment_type as AdjustmentType,
                   adjustment_value as AdjustmentValue,
                   is_active as IsActive,
                   created_at as CreatedAt
            FROM catalog.pricing_rules
            WHERE (@ActiveOnly IS NULL OR is_active = @ActiveOnly)
            ORDER BY rule_type, name";
        return await conn.QueryAsync<PricingRuleDto>(sql, new { ActiveOnly = activeOnly });
    }

    public async Task<PricingRuleDto?> GetPricingRuleByIdAsync(Guid ruleId)
    {
        using var conn = _dbFactory.CreateReadConnection();
        const string sql = @"
            SELECT rule_id as RuleId,
                   name as Name,
                   rule_type as RuleType,
                   day_of_week as DayOfWeek,
                   start_time as StartTime,
                   end_time as EndTime,
                   screen_type_code as ScreenTypeCode,
                   adjustment_type as AdjustmentType,
                   adjustment_value as AdjustmentValue,
                   is_active as IsActive,
                   created_at as CreatedAt
            FROM catalog.pricing_rules
            WHERE rule_id = @RuleId;";
        return await conn.QuerySingleOrDefaultAsync<PricingRuleDto>(sql, new { RuleId = ruleId });
    }

    public async Task<Guid> CreatePricingRuleAsync(CreatePricingRuleRequest req)
    {
        using var conn = _dbFactory.CreateConnection();
        const string sql = @"
            INSERT INTO catalog.pricing_rules (
                name, rule_type, day_of_week, start_time, end_time, screen_type_code, adjustment_type, adjustment_value, is_active
            ) VALUES (
                @Name, @RuleType, @DayOfWeek, @StartTime, @EndTime, @ScreenTypeCode, @AdjustmentType, @AdjustmentValue, true
            ) RETURNING rule_id;";
        return await conn.ExecuteScalarAsync<Guid>(sql, new
        {
            req.Name,
            req.RuleType,
            req.DayOfWeek,
            req.StartTime,
            req.EndTime,
            req.ScreenTypeCode,
            req.AdjustmentType,
            req.AdjustmentValue
        });
    }

    public async Task<bool> UpdatePricingRuleAsync(Guid ruleId, UpdatePricingRuleRequest req)
    {
        using var conn = _dbFactory.CreateConnection();
        const string sql = @"
            UPDATE catalog.pricing_rules
            SET name = COALESCE(@Name, name),
                rule_type = COALESCE(@RuleType, rule_type),
                day_of_week = COALESCE(@DayOfWeek, day_of_week),
                start_time = COALESCE(@StartTime, start_time),
                end_time = COALESCE(@EndTime, end_time),
                screen_type_code = COALESCE(@ScreenTypeCode, screen_type_code),
                adjustment_type = COALESCE(@AdjustmentType, adjustment_type),
                adjustment_value = COALESCE(@AdjustmentValue, adjustment_value),
                is_active = COALESCE(@IsActive, is_active)
            WHERE rule_id = @RuleId;";
        var affected = await conn.ExecuteAsync(sql, new
        {
            RuleId = ruleId,
            req.Name,
            req.RuleType,
            req.DayOfWeek,
            req.StartTime,
            req.EndTime,
            req.ScreenTypeCode,
            req.AdjustmentType,
            req.AdjustmentValue,
            req.IsActive
        });
        return affected > 0;
    }

    public async Task<bool> UpdatePricingRuleStatusAsync(Guid ruleId, bool isActive)
    {
        using var conn = _dbFactory.CreateConnection();
        const string sql = @"
            UPDATE catalog.pricing_rules
            SET is_active = @IsActive
            WHERE rule_id = @RuleId;";
        return await conn.ExecuteAsync(sql, new { RuleId = ruleId, IsActive = isActive }) > 0;
    }

    public async Task<CalculatedTicketPriceResponse?> CalculateTicketPriceAsync(
        Guid priceCardId,
        string ticketTypeCode,
        string seatType,
        DateTimeOffset showtimeStartsAt,
        string? screenTypeCode)
    {
        using var conn = _dbFactory.CreateReadConnection();

        // 1. Fetch base price from matrix
        const string baseSql = @"
            SELECT pce.price
            FROM catalog.price_card_entries pce
            JOIN catalog.ticket_types tt ON pce.ticket_type_id = tt.ticket_type_id
            WHERE pce.price_card_id = @PriceCardId
              AND UPPER(tt.code) = UPPER(@TicketTypeCode)
              AND LOWER(pce.seat_type) = LOWER(@SeatType);";

        var basePriceNullable = await conn.ExecuteScalarAsync<decimal?>(baseSql, new
        {
            PriceCardId = priceCardId,
            TicketTypeCode = ticketTypeCode.Trim(),
            SeatType = seatType.Trim()
        });

        if (!basePriceNullable.HasValue)
        {
            return null;
        }

        var basePrice = basePriceNullable.Value;
        var adjustments = new List<PriceAdjustmentDto>();
        var currentPrice = basePrice;

        // 2. Fetch active rules
        var rules = await GetPricingRulesAsync(activeOnly: true);

        foreach (var rule in rules)
        {
            bool ruleApplies = false;

            switch (rule.RuleType.ToLowerInvariant())
            {
                case "day_of_week":
                    if (rule.DayOfWeek.HasValue && rule.DayOfWeek.Value == (int)showtimeStartsAt.DayOfWeek)
                    {
                        ruleApplies = true;
                    }
                    break;

                case "matinee":
                    var showTime = TimeOnly.FromTimeSpan(showtimeStartsAt.TimeOfDay);
                    var start = rule.StartTime ?? TimeOnly.MinValue;
                    var end = rule.EndTime ?? new TimeOnly(12, 0, 0);
                    if (showTime >= start && showTime < end)
                    {
                        ruleApplies = true;
                    }
                    break;

                case "weekend_surge":
                    if (showtimeStartsAt.DayOfWeek == DayOfWeek.Saturday || showtimeStartsAt.DayOfWeek == DayOfWeek.Sunday)
                    {
                        ruleApplies = true;
                    }
                    break;

                case "format_surcharge":
                    if (!string.IsNullOrWhiteSpace(screenTypeCode) &&
                        !string.IsNullOrWhiteSpace(rule.ScreenTypeCode) &&
                        string.Equals(rule.ScreenTypeCode, screenTypeCode, StringComparison.OrdinalIgnoreCase))
                    {
                        ruleApplies = true;
                    }
                    break;
            }

            if (ruleApplies)
            {
                decimal amount;
                if (string.Equals(rule.AdjustmentType, "percentage", StringComparison.OrdinalIgnoreCase))
                {
                    amount = Math.Round(basePrice * (rule.AdjustmentValue / 100m), 2);
                }
                else
                {
                    amount = rule.AdjustmentValue;
                }

                adjustments.Add(new PriceAdjustmentDto
                {
                    RuleName = rule.Name,
                    RuleType = rule.RuleType,
                    Amount = amount
                });

                currentPrice += amount;
            }
        }

        var finalPrice = Math.Max(0m, currentPrice);

        return new CalculatedTicketPriceResponse
        {
            PriceCardId = priceCardId,
            TicketTypeCode = ticketTypeCode.ToUpperInvariant(),
            SeatType = seatType.ToLowerInvariant(),
            BasePrice = basePrice,
            Adjustments = adjustments,
            FinalPrice = finalPrice
        };
    }
}







