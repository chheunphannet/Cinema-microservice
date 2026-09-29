-- =========================================================================
-- 017-screen-optimized-catalog.sql
-- Screen-Optimized Catalog Discovery: Now Showing, Featured, Coming Soon, Recommended
-- =========================================================================

-- 1. Movie Schema Enhancements for Curated & Rule-Ranked Feeds
ALTER TABLE catalog.movies
    ADD COLUMN IF NOT EXISTS rating NUMERIC(3, 1) NOT NULL DEFAULT 8.0,
    ADD COLUMN IF NOT EXISTS is_featured BOOLEAN NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS banner_custom_tag VARCHAR(50);

-- Backfill ratings from canonical Movies table if present
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'movies' AND table_schema = 'public') THEN
        UPDATE catalog.movies cm
        SET rating = m.rating
        FROM Movies m
        WHERE cm.movie_id = m.movie_id AND m.rating IS NOT NULL;
    END IF;
END $$;

-- 2. Seed / Enhance Sample Movies with Featured Badges and Ratings
UPDATE catalog.movies
SET is_featured = true,
    banner_custom_tag = 'IMAX Laser Premiere',
    rating = 8.8
WHERE movie_id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';

UPDATE catalog.movies
SET is_featured = true,
    banner_custom_tag = 'Trending #1 Worldwide',
    rating = 8.6
WHERE movie_id = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';

UPDATE catalog.movies
SET rating = 8.9
WHERE movie_id = 'cccccccc-cccc-cccc-cccc-cccccccccccc';

-- Seed a sample "Coming Soon" movie if not already present
INSERT INTO catalog.movies (
    movie_id, title, duration_minutes, genre, classification, rating,
    release_date, teaser_text, synopsis, trailer_url, poster_url, backdrop_url,
    audio_language, subtitle_language, director, cast_members, is_active, is_featured, banner_custom_tag
) VALUES (
    'dddddddd-dddd-dddd-dddd-dddddddddddd',
    'Avatar: Fire and Ash',
    195,
    'Sci-Fi/Action',
    'PG-13',
    9.0,
    CURRENT_DATE + INTERVAL '90 days',
    'The battle for Pandora enters the fire realm.',
    'Following the oceanic conflict, Jake Sully and Neytiri encounter an aggressive volcanic clan of Na''vi known as the Ash People.',
    'https://www.youtube.com/watch?v=d9MyW72ELq0',
    'https://assets.cinema.local/posters/avatar-3.jpg',
    'https://assets.cinema.local/backdrops/avatar-3-wide.jpg',
    'English',
    'Khmer & English',
    'James Cameron',
    'Sam Worthington, Zoe Saldana, Michelle Yeoh',
    true,
    true,
    'Advance Booking Soon'
) ON CONFLICT (movie_id) DO NOTHING;

-- 3. High-Performance Discovery Indexes
CREATE INDEX IF NOT EXISTS ix_movies_now_showing 
    ON catalog.movies (is_active, release_date);

CREATE INDEX IF NOT EXISTS ix_movies_featured 
    ON catalog.movies (is_featured, is_active);

CREATE INDEX IF NOT EXISTS ix_confirmed_seats_booked_at 
    ON reservations.confirmed_seats (booked_at);

CREATE INDEX IF NOT EXISTS ix_showtimes_movie_starts_status 
    ON catalog.showtimes (movie_id, starts_at, status);
