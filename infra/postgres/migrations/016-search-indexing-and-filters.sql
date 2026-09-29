-- =========================================================================
-- 016-search-indexing-and-filters.sql
-- Phase 2: High-Performance Search, Filter & Autocomplete Engine (Movies & F&B)
-- =========================================================================

-- 1. PostgreSQL Trigram Extension for Fast Fuzzy Search & Autocomplete
CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- 2. GIN Trigram Index on Movies (Title, Director, Cast)
CREATE INDEX IF NOT EXISTS ix_movies_fts ON catalog.movies 
    USING gin (
        title gin_trgm_ops, 
        coalesce(director, '') gin_trgm_ops, 
        coalesce(cast_members, '') gin_trgm_ops
    );

-- 3. Composite Index on Showtimes for Time Slot & Cutoff Filtering
CREATE INDEX IF NOT EXISTS ix_showtimes_filtering 
    ON catalog.showtimes (starts_at, auditorium_id) 
    INCLUDE (movie_id, base_price, status);

CREATE INDEX IF NOT EXISTS ix_showtimes_movie_starts 
    ON catalog.showtimes (movie_id, starts_at) 
    INCLUDE (auditorium_id, base_price, status);

-- 4. Composite Index on Auditoriums for Branch & Screen Type Join
CREATE INDEX IF NOT EXISTS ix_auditoriums_branch_screen 
    ON catalog.auditoriums (branch_id, screen_type_id);

-- 5. F&B Concessions Product Filtering Columns
ALTER TABLE pos.products
    ADD COLUMN IF NOT EXISTS dietary_tags TEXT[] NOT NULL DEFAULT '{}',
    ADD COLUMN IF NOT EXISTS is_combo_only BOOLEAN NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS min_loyalty_tier VARCHAR(30) NOT NULL DEFAULT 'none',
    ADD COLUMN IF NOT EXISTS stock_quantity INT NOT NULL DEFAULT 999;

-- 6. GIN Index on F&B Dietary Tags Array for Lightning-Fast Tag Filtering
CREATE INDEX IF NOT EXISTS ix_products_dietary 
    ON pos.products USING gin (dietary_tags);

-- 7. GIN Trigram Index on Product Name & Description for Fast Menu Search
CREATE INDEX IF NOT EXISTS ix_products_search 
    ON pos.products USING gin (
        name gin_trgm_ops, 
        coalesce(description, '') gin_trgm_ops
    );

-- 8. Enrich Sample F&B Products with Dietary Tags, Stock, and Tiers
UPDATE pos.products 
SET dietary_tags = ARRAY['vegetarian', 'gluten_free', 'halal'], 
    stock_quantity = 500,
    min_loyalty_tier = 'none'
WHERE name ILIKE '%popcorn%' AND name NOT ILIKE '%combo%';

UPDATE pos.products 
SET dietary_tags = ARRAY['vegan', 'vegetarian', 'halal', 'gluten_free'], 
    stock_quantity = 1000,
    min_loyalty_tier = 'none'
WHERE name ILIKE '%coke%' OR name ILIKE '%drink%' OR name ILIKE '%water%';

UPDATE pos.products 
SET dietary_tags = ARRAY['vegetarian', 'halal'], 
    stock_quantity = 250,
    min_loyalty_tier = 'none'
WHERE name ILIKE '%combo%';

UPDATE pos.products 
SET dietary_tags = ARRAY['vegetarian', 'spicy'], 
    stock_quantity = 150,
    min_loyalty_tier = 'silver'
WHERE name ILIKE '%nacho%' OR name ILIKE '%hot%';
