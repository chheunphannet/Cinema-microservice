-- =========================================================================
-- 020-showtime-scheduling-and-seat-blocks.sql
-- Milestone 5.2: Movie Lifecycle, Collision-Safe Showtime Engine & Seat Blocks
-- =========================================================================

-- 1. Ensure btree_gist extension is enabled for exclusion constraints
CREATE EXTENSION IF NOT EXISTS btree_gist;

-- 2. Enhance catalog.movies with lifecycle release status and age ratings
ALTER TABLE catalog.movies 
    ADD COLUMN IF NOT EXISTS release_status VARCHAR(30) NOT NULL DEFAULT 'now_showing',
    ADD COLUMN IF NOT EXISTS age_rating VARCHAR(20) NOT NULL DEFAULT 'PG-13',
    ADD COLUMN IF NOT EXISTS advisory_text TEXT NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'chk_movies_release_status'
    ) THEN
        ALTER TABLE catalog.movies 
        ADD CONSTRAINT chk_movies_release_status 
        CHECK (release_status IN ('coming_soon', 'now_showing', 'ended'));
    END IF;
END $$;

-- 3. Enhance catalog.auditoriums with cleaning buffer & maintenance mode
ALTER TABLE catalog.auditoriums 
    ADD COLUMN IF NOT EXISTS cleaning_buffer_minutes INT NOT NULL DEFAULT 15,
    ADD COLUMN IF NOT EXISTS maintenance_mode BOOLEAN NOT NULL DEFAULT false;

-- 4. Enforce temporal collision exclusion constraint on catalog.showtimes
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'no_overlapping_showtimes'
    ) THEN
        ALTER TABLE catalog.showtimes 
        ADD CONSTRAINT no_overlapping_showtimes 
        EXCLUDE USING gist (
            auditorium_id WITH =,
            tstzrange(starts_at, ends_at, '[)') WITH &&
        ) WHERE (status != 'cancelled');
    END IF;
END $$;

-- 5. Movie screen format mapping table
CREATE TABLE IF NOT EXISTS catalog.movie_screen_types (
    movie_id UUID NOT NULL REFERENCES catalog.movies(movie_id) ON DELETE CASCADE,
    screen_type_id UUID NOT NULL REFERENCES catalog.screen_types(screen_type_id) ON DELETE CASCADE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (movie_id, screen_type_id)
);

-- Seed default screen type association for existing movies
INSERT INTO catalog.movie_screen_types (movie_id, screen_type_id)
SELECT m.movie_id, st.screen_type_id
FROM catalog.movies m
CROSS JOIN catalog.screen_types st
WHERE st.code IN ('STANDARD', 'IMAX')
ON CONFLICT DO NOTHING;

-- 6. Table: catalog.seat_blocks (Administrative Holds & Out-of-Order Seats)
CREATE TABLE IF NOT EXISTS catalog.seat_blocks (
    block_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    auditorium_id UUID NOT NULL REFERENCES catalog.auditoriums(auditorium_id) ON DELETE CASCADE,
    showtime_id UUID NULL REFERENCES catalog.showtimes(showtime_id) ON DELETE CASCADE,
    seat_id UUID NOT NULL REFERENCES catalog.seats(seat_id) ON DELETE CASCADE,
    reason VARCHAR(50) NOT NULL DEFAULT 'maintenance' CHECK (reason IN ('maintenance', 'vip_hold', 'press', 'broken', 'sponsor')),
    notes TEXT NULL,
    blocked_by UUID NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE(showtime_id, seat_id)
);

CREATE INDEX IF NOT EXISTS ix_seat_blocks_showtime ON catalog.seat_blocks(showtime_id);
CREATE INDEX IF NOT EXISTS ix_seat_blocks_auditorium ON catalog.seat_blocks(auditorium_id);
