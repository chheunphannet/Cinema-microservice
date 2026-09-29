-- =========================================================================
-- 015-rich-catalog-and-notifications.sql
-- Phase 1: Rich Movie Metadata, Screen/Hall Logos, Promotions & Notifications
-- =========================================================================

-- 1. Movie Properties Expansion
ALTER TABLE catalog.movies
    ADD COLUMN IF NOT EXISTS release_date DATE,
    ADD COLUMN IF NOT EXISTS teaser_text TEXT,
    ADD COLUMN IF NOT EXISTS synopsis TEXT,
    ADD COLUMN IF NOT EXISTS trailer_url TEXT,
    ADD COLUMN IF NOT EXISTS backdrop_url TEXT,
    ADD COLUMN IF NOT EXISTS audio_language VARCHAR(50) NOT NULL DEFAULT 'Khmer',
    ADD COLUMN IF NOT EXISTS subtitle_language VARCHAR(50) NOT NULL DEFAULT 'English',
    ADD COLUMN IF NOT EXISTS director VARCHAR(200),
    ADD COLUMN IF NOT EXISTS cast_members TEXT;

-- 2. Screen Formats / Screen Types (IMAX, 4DX, ScreenX, etc.)
CREATE TABLE IF NOT EXISTS catalog.screen_types (
    screen_type_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code VARCHAR(30) UNIQUE NOT NULL,
    name VARCHAR(100) NOT NULL,
    logo_url TEXT,
    description TEXT,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Seed Standard Screen Types
INSERT INTO catalog.screen_types (screen_type_id, code, name, logo_url, description) VALUES
    ('c1111111-1111-1111-1111-111111111111', 'IMAX', 'IMAX with Laser', 'https://assets.cinema.local/logos/screens/imax-laser.svg', 'Unmatched immersion with dual-4K laser projection and 12-channel sound.'),
    ('c2222222-2222-2222-2222-222222222222', '4DX', '4DX Experience', 'https://assets.cinema.local/logos/screens/4dx.svg', 'Multi-sensory cinema with motion seats, wind, fog, water, and scent effects.'),
    ('c3333333-3333-3333-3333-333333333333', 'SCREENX', 'ScreenX 270°', 'https://assets.cinema.local/logos/screens/screenx.svg', 'World first multi-projection immersive cinema extending to theater side walls.'),
    ('c4444444-4444-4444-4444-444444444444', 'ATMOS', 'Dolby Atmos Sound', 'https://assets.cinema.local/logos/screens/dolby-atmos.svg', 'Next-generation object-based audio creating realistic 3D soundscapes.'),
    ('c5555555-5555-5555-5555-555555555555', 'VIP', 'VIP Lounge & Recliners', 'https://assets.cinema.local/logos/screens/vip-gold.svg', 'Ultimate comfort featuring motorized leather recliners and in-hall food service.'),
    ('c6666666-6666-6666-6666-666666666666', 'STANDARD', 'Standard Digital 2D', 'https://assets.cinema.local/logos/screens/standard-2d.svg', 'High-definition digital projection with pristine multi-channel stereo.')
ON CONFLICT (code) DO NOTHING;

-- 3. Auditorium Hall Enhancements
ALTER TABLE catalog.auditoriums
    ADD COLUMN IF NOT EXISTS screen_type_id UUID REFERENCES catalog.screen_types(screen_type_id),
    ADD COLUMN IF NOT EXISTS hall_type VARCHAR(50) NOT NULL DEFAULT 'Standard',
    ADD COLUMN IF NOT EXISTS hall_logo_url TEXT;

-- Update existing auditorium to IMAX screen type
UPDATE catalog.auditoriums 
SET screen_type_id = 'c1111111-1111-1111-1111-111111111111', 
    hall_type = 'IMAX Hall', 
    hall_logo_url = 'https://assets.cinema.local/logos/halls/hall-imax.svg'
WHERE auditorium_id = '44444444-4444-4444-4444-444444444444';

-- 4. Branch Gallery Images
ALTER TABLE catalog.branches
    ADD COLUMN IF NOT EXISTS hero_image_url TEXT;

CREATE TABLE IF NOT EXISTS catalog.branch_images (
    image_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    branch_id UUID NOT NULL REFERENCES catalog.branches(branch_id) ON DELETE CASCADE,
    image_url TEXT NOT NULL,
    caption VARCHAR(255),
    display_order INT NOT NULL DEFAULT 0,
    is_primary BOOLEAN NOT NULL DEFAULT false,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Seed Branch Hero Image
UPDATE catalog.branches 
SET hero_image_url = 'https://assets.cinema.local/branches/phnom-penh-flagship-hero.jpg' 
WHERE branch_id = '11111111-1111-1111-1111-111111111111';

-- 5. F&B Product Images & Concession Badges
ALTER TABLE pos.products
    ADD COLUMN IF NOT EXISTS image_url TEXT,
    ADD COLUMN IF NOT EXISTS badge_text VARCHAR(50),
    ADD COLUMN IF NOT EXISTS description TEXT;

-- 6. Marketing Promotions & Campaign Banners
CREATE TABLE IF NOT EXISTS catalog.promotions (
    promotion_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    title VARCHAR(255) NOT NULL,
    subtitle VARCHAR(255),
    poster_url TEXT NOT NULL,
    banner_url TEXT,
    content_text TEXT NOT NULL,
    discount_type VARCHAR(30) NOT NULL DEFAULT 'none',
    discount_value NUMERIC(12,2),
    promo_code VARCHAR(50),
    starts_at TIMESTAMPTZ NOT NULL,
    ends_at TIMESTAMPTZ NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_promotions_active_dates ON catalog.promotions(is_active, starts_at, ends_at);

-- 7. Global Notifications & System Announcements
CREATE TABLE IF NOT EXISTS catalog.global_notifications (
    notification_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    title VARCHAR(255) NOT NULL,
    message TEXT NOT NULL,
    type VARCHAR(30) NOT NULL DEFAULT 'info',
    action_url TEXT,
    starts_at TIMESTAMPTZ NOT NULL,
    expires_at TIMESTAMPTZ NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_global_notifications_active ON catalog.global_notifications(is_active, starts_at, expires_at);
