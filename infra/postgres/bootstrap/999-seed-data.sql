-- Seed Sample Branches
INSERT INTO Branches (branch_id, name, location_details)
VALUES 
    ('11111111-1111-1111-1111-111111111111', 'Cinema City - Downtown Mall', 'Downtown Complex, Level 4, Phnom Penh'),
    ('22222222-2222-2222-2222-222222222222', 'Cinema City - Riverside Plaza', 'Riverside Promenade, Hall A, Phnom Penh')
ON CONFLICT DO NOTHING;

INSERT INTO catalog.branches (branch_id, code, name, address, timezone)
VALUES 
    ('11111111-1111-1111-1111-111111111111', 'BR-001', 'Cinema City - Downtown Mall', 'Downtown Complex, Level 4, Phnom Penh', 'Asia/Phnom_Penh'),
    ('22222222-2222-2222-2222-222222222222', 'BR-002', 'Cinema City - Riverside Plaza', 'Riverside Promenade, Hall A, Phnom Penh', 'Asia/Phnom_Penh')
ON CONFLICT DO NOTHING;

-- Seed Sample Movies
INSERT INTO Movies (movie_id, title, duration_minutes, genre, rating)
VALUES 
    ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'Avatar: The Way of Water', 192, 'Sci-Fi/Action', 7.8),
    ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'Dune: Part Two', 166, 'Sci-Fi/Adventure', 8.5),
    ('cccccccc-cccc-cccc-cccc-cccccccccccc', 'Oppenheimer', 180, 'Biography/Drama', 8.9)
ON CONFLICT DO NOTHING;

INSERT INTO catalog.movies (
    movie_id, title, duration_minutes, genre, classification, 
    release_date, teaser_text, synopsis, trailer_url, poster_url, backdrop_url, 
    audio_language, subtitle_language, director, cast_members, is_active
) VALUES 
    (
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 
        'Avatar: The Way of Water', 
        192, 
        'Sci-Fi/Action', 
        'PG-13',
        '2022-12-16',
        'Return to Pandora for an epic oceanic adventure.',
        'Jake Sully lives with his newfound family formed on the extrasolar moon Pandora. Once a familiar threat returns to finish what was previously started, Jake must work with Neytiri and the army of the Na''vi race to protect their home.',
        'https://www.youtube.com/watch?v=d9MyW72ELq0',
        'https://assets.cinema.local/posters/avatar-2.jpg',
        'https://assets.cinema.local/backdrops/avatar-2-wide.jpg',
        'English',
        'Khmer & English',
        'James Cameron',
        'Sam Worthington, Zoe Saldana, Sigourney Weaver, Stephen Lang',
        true
    ),
    (
        'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 
        'Dune: Part Two', 
        166, 
        'Sci-Fi/Adventure', 
        'PG-13',
        '2024-03-01',
        'Long live the fighters.',
        'Paul Atreides unites with Chani and the Fremen while seeking revenge against the conspirators who destroyed his family. Facing a choice between the love of his life and the fate of the universe, he endeavors to prevent a terrible future.',
        'https://www.youtube.com/watch?v=Way9Dexny3w',
        'https://assets.cinema.local/posters/dune-2.jpg',
        'https://assets.cinema.local/backdrops/dune-2-wide.jpg',
        'English',
        'Khmer',
        'Denis Villeneuve',
        'Timothee Chalamet, Zendaya, Rebecca Ferguson, Javier Bardem',
        true
    ),
    (
        'cccccccc-cccc-cccc-cccc-cccccccccccc', 
        'Oppenheimer', 
        180, 
        'Biography/Drama', 
        'R',
        '2023-07-21',
        'The story of American scientist J. Robert Oppenheimer and his role in the Manhattan Project.',
        'A dramatization of the life story of J. Robert Oppenheimer, the physicist who had a large role in the Manhattan Project that developed the first nuclear weapons.',
        'https://www.youtube.com/watch?v=uYPbbksJxIg',
        'https://assets.cinema.local/posters/oppenheimer.jpg',
        'https://assets.cinema.local/backdrops/oppenheimer-wide.jpg',
        'English',
        'Khmer & English',
        'Christopher Nolan',
        'Cillian Murphy, Emily Blunt, Matt Damon, Robert Downey Jr.',
        true
    )
ON CONFLICT (movie_id) DO UPDATE SET 
    trailer_url = EXCLUDED.trailer_url,
    poster_url = EXCLUDED.poster_url,
    backdrop_url = EXCLUDED.backdrop_url,
    teaser_text = EXCLUDED.teaser_text,
    synopsis = EXCLUDED.synopsis,
    audio_language = EXCLUDED.audio_language,
    subtitle_language = EXCLUDED.subtitle_language;

-- Seed Sample Concession Products with Posters & Badges
INSERT INTO pos.products (product_id, branch_id, sku, name, category, unit_price, image_url, badge_text, description, is_active)
VALUES 
    ('d1111111-1111-1111-1111-111111111111', '11111111-1111-1111-1111-111111111111', 'SKU-POPCORN-L', 'Caramel Popcorn (Large)', 'Concessions', 5.50, 'https://assets.cinema.local/fnb/caramel-popcorn.png', 'Best Seller', 'Freshly popped gourmet caramel corn coated in rich golden butter caramel.', true),
    ('d2222222-2222-2222-2222-222222222222', '11111111-1111-1111-1111-111111111111', 'SKU-COLA-L', 'Soft Drink (Large)', 'Beverages', 3.00, 'https://assets.cinema.local/fnb/soda-large.png', 'Refreshing', 'Ice-cold fountain drink with free refill before showtime starts.', true),
    ('d3333333-3333-3333-3333-333333333333', '11111111-1111-1111-1111-111111111111', 'SKU-COMBO-1', 'Classic Couple Combo', 'Combos', 10.00, 'https://assets.cinema.local/fnb/couple-combo.png', 'Save 20%', '1 Large Popcorn + 2 Large Sodas + 1 Nachos cheese snack pack.', true)
ON CONFLICT (product_id) DO UPDATE SET
    image_url = EXCLUDED.image_url,
    badge_text = EXCLUDED.badge_text,
    description = EXCLUDED.description;

-- Seed Sample Auditoriums
INSERT INTO catalog.auditoriums (auditorium_id, branch_id, name, capacity)
VALUES 
    ('44444444-4444-4444-4444-444444444444', '11111111-1111-1111-1111-111111111111', 'Auditorium 1 (IMAX)', 10),
    ('55555555-5555-5555-5555-555555555555', '11111111-1111-1111-1111-111111111111', 'Auditorium 2 (Standard)', 10)
ON CONFLICT DO NOTHING;

-- Seed Sample Seats (10 Seats for Auditorium 1)
INSERT INTO catalog.seats (seat_id, auditorium_id, row_label, seat_number, seat_type, is_accessible, is_active)
VALUES 
    ('e1111111-0000-0000-0000-000000000001', '44444444-4444-4444-4444-444444444444', 'A', 1, 'standard', true, true),
    ('e1111111-0000-0000-0000-000000000002', '44444444-4444-4444-4444-444444444444', 'A', 2, 'standard', false, true),
    ('e1111111-0000-0000-0000-000000000003', '44444444-4444-4444-4444-444444444444', 'A', 3, 'standard', false, true),
    ('e1111111-0000-0000-0000-000000000004', '44444444-4444-4444-4444-444444444444', 'A', 4, 'standard', false, true),
    ('e1111111-0000-0000-0000-000000000005', '44444444-4444-4444-4444-444444444444', 'A', 5, 'vip', false, true),
    ('e1111111-0000-0000-0000-000000000006', '44444444-4444-4444-4444-444444444444', 'A', 6, 'vip', false, true),
    ('e1111111-0000-0000-0000-000000000007', '44444444-4444-4444-4444-444444444444', 'A', 7, 'standard', false, true),
    ('e1111111-0000-0000-0000-000000000008', '44444444-4444-4444-4444-444444444444', 'A', 8, 'standard', false, true),
    ('e1111111-0000-0000-0000-000000000009', '44444444-4444-4444-4444-444444444444', 'A', 9, 'standard', false, true),
    ('e1111111-0000-0000-0000-000000000010', '44444444-4444-4444-4444-444444444444', 'A', 10, 'standard', false, true)
ON CONFLICT DO NOTHING;

-- Also seed Module 1.1 Canonical Seats
INSERT INTO Seats (seat_id, branch_id, row_identifier, seat_number, seat_type)
VALUES 
    ('e1111111-0000-0000-0000-000000000001', '11111111-1111-1111-1111-111111111111', 'A', 1, 'Standard'),
    ('e1111111-0000-0000-0000-000000000002', '11111111-1111-1111-1111-111111111111', 'A', 2, 'Standard'),
    ('e1111111-0000-0000-0000-000000000005', '11111111-1111-1111-1111-111111111111', 'A', 5, 'VIP'),
    ('e1111111-0000-0000-0000-000000000006', '11111111-1111-1111-1111-111111111111', 'A', 6, 'VIP')
ON CONFLICT DO NOTHING;

-- Seed Sample Showtimes
INSERT INTO catalog.showtimes (showtime_id, movie_id, auditorium_id, starts_at, ends_at, base_price, status, price_card_id)
VALUES 
    ('33333333-3333-3333-3333-333333333333', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', '44444444-4444-4444-4444-444444444444', now() + interval '2 hours', now() + interval '5 hours', 6.50, 'scheduled', 'b1111111-1111-1111-1111-111111111111'),
    ('33333333-3333-3333-3333-333333333334', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', '44444444-4444-4444-4444-444444444444', now() + interval '6 hours', now() + interval '9 hours', 8.50, 'scheduled', 'b1111111-1111-1111-1111-111111111111')
ON CONFLICT DO NOTHING;

-- Seed Sample Staff Users (PIN 1234 for cashier, PIN 9999 for supervisor, PIN admin123 for admin)
-- SHA256 of '1234' = '03ac674216f3e15c761ee1a5e255f067953623c8b388b4459e13f978d7c846f4'
-- SHA256 of '9999' = '888df25ae35772424a560c7152a1de794440e0ea5cfee62828333a456a506e05'
-- SHA256 of 'admin123' = '240be518fabd2724ddb6f04eeb1da5967448d7e831c08c8fa822809f74c720a9'
INSERT INTO identity.users (user_id, branch_id, username, display_name, pin_hash, is_active)
VALUES 
    ('99999999-9999-9999-9999-999999999999', '11111111-1111-1111-1111-111111111111', 'cashier1', 'Lead Box-Office Cashier', '03ac674216f3e15c761ee1a5e255f067953623c8b388b4459e13f978d7c846f4', true),
    ('88888888-8888-8888-8888-888888888888', '11111111-1111-1111-1111-111111111111', 'supervisor1', 'Shift Supervisor', '888df25ae35772424a560c7152a1de794440e0ea5cfee62828333a456a506e05', true),
    ('77777777-7777-7777-7777-777777777777', '11111111-1111-1111-1111-111111111111', 'admin1', 'System Administrator', '240be518fabd2724ddb6f04eeb1da5967448d7e831c08c8fa822809f74c720a9', true)
ON CONFLICT (username) DO UPDATE SET pin_hash = EXCLUDED.pin_hash;

-- Map Users to Roles
INSERT INTO identity.user_roles (user_id, role_id)
SELECT '99999999-9999-9999-9999-999999999999', role_id FROM identity.roles WHERE name = 'cashier'
ON CONFLICT DO NOTHING;

INSERT INTO identity.user_roles (user_id, role_id)
SELECT '88888888-8888-8888-8888-888888888888', role_id FROM identity.roles WHERE name = 'supervisor'
ON CONFLICT DO NOTHING;

INSERT INTO identity.user_roles (user_id, role_id)
SELECT '77777777-7777-7777-7777-777777777777', role_id FROM identity.roles WHERE name = 'system_admin'
ON CONFLICT DO NOTHING;

-- Seed Phase 2 Auditorium Layouts (AuditoriumID, BranchID, SeatMap JSONB)
INSERT INTO AuditoriumLayouts (auditorium_id, branch_id, seat_map)
VALUES 
    ('44444444-4444-4444-4444-444444444444', '11111111-1111-1111-1111-111111111111', 
     '[{"seatId":"e1111111-0000-0000-0000-000000000001","row":"A","number":1,"type":"standard","accessible":true},
       {"seatId":"e1111111-0000-0000-0000-000000000002","row":"A","number":2,"type":"standard","accessible":false},
       {"seatId":"e1111111-0000-0000-0000-000000000005","row":"A","number":5,"type":"vip","accessible":false}]'::jsonb)
ON CONFLICT DO NOTHING;

INSERT INTO catalog.auditorium_layouts (auditorium_id, branch_id, seat_map)
VALUES 
    ('44444444-4444-4444-4444-444444444444', '11111111-1111-1111-1111-111111111111', 
     '[{"seatId":"e1111111-0000-0000-0000-000000000001","row":"A","number":1,"type":"standard","accessible":true},
       {"seatId":"e1111111-0000-0000-0000-000000000002","row":"A","number":2,"type":"standard","accessible":false},
       {"seatId":"e1111111-0000-0000-0000-000000000005","row":"A","number":5,"type":"vip","accessible":false}]'::jsonb)
ON CONFLICT DO NOTHING;

-- =========================================================================
-- Phase 3: Guest Checkout & Digital Ticket Enhancements
-- =========================================================================
ALTER TABLE reservations.reservations 
    ADD COLUMN IF NOT EXISTS guest_email VARCHAR(255) NULL,
    ADD COLUMN IF NOT EXISTS guest_phone VARCHAR(50) NULL,
    ADD COLUMN IF NOT EXISTS guest_name VARCHAR(150) NULL,
    ADD COLUMN IF NOT EXISTS is_guest BOOLEAN NOT NULL DEFAULT false;

CREATE INDEX IF NOT EXISTS ix_reservations_guest_email 
    ON reservations.reservations(guest_email) 
    WHERE guest_email IS NOT NULL;

DO $$ 
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'chk_reservation_identity') THEN
        ALTER TABLE reservations.reservations 
            ADD CONSTRAINT chk_reservation_identity CHECK (
                (is_guest = false) OR 
                (is_guest = true AND (guest_email IS NOT NULL OR status = 'hold'))
            );
    END IF;
END $$;

ALTER TABLE pos.orders 
    ADD COLUMN IF NOT EXISTS channel VARCHAR(20) NOT NULL DEFAULT 'pos',
    ADD COLUMN IF NOT EXISTS customer_email VARCHAR(255) NULL,
    ADD COLUMN IF NOT EXISTS customer_phone VARCHAR(50) NULL;

DO $$ 
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'chk_order_channel') THEN
        ALTER TABLE pos.orders ADD CONSTRAINT chk_order_channel CHECK (channel IN ('pos', 'web', 'kiosk', 'mobile'));
    END IF;
END $$;

DO $$ 
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'chk_order_cashier_channel') THEN
        ALTER TABLE pos.orders 
            ADD CONSTRAINT chk_order_cashier_channel CHECK (
                (channel = 'web') OR 
                (channel = 'pos' AND status = 'draft') OR 
                (channel = 'pos' AND cashier_id IS NOT NULL)
            );
    END IF;
END $$;

ALTER TABLE tickets.tickets 
    ADD COLUMN IF NOT EXISTS delivery_channel VARCHAR(20) NOT NULL DEFAULT 'print',
    ADD COLUMN IF NOT EXISTS email_recipient VARCHAR(255) NULL,
    ADD COLUMN IF NOT EXISTS email_sent BOOLEAN NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS email_sent_at TIMESTAMPTZ NULL,
    ADD COLUMN IF NOT EXISTS email_message_id VARCHAR(100) NULL,
    ADD COLUMN IF NOT EXISTS access_hmac_secret VARCHAR(64) NULL;

CREATE INDEX IF NOT EXISTS ix_tickets_email_sent 
    ON tickets.tickets(email_sent) 
    WHERE email_sent = false;

-- F&B Concessions Digital Pickup Tracking for Guest & Online Orders
ALTER TABLE pos.orders 
    ADD COLUMN IF NOT EXISTS concession_pickup_code VARCHAR(64) NULL,
    ADD COLUMN IF NOT EXISTS concession_status VARCHAR(30) NOT NULL DEFAULT 'none';

ALTER TABLE pos.order_lines 
    ADD COLUMN IF NOT EXISTS is_fulfilled BOOLEAN NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS fulfilled_at TIMESTAMPTZ NULL;

CREATE INDEX IF NOT EXISTS ix_pos_orders_concession_pickup 
    ON pos.orders(concession_pickup_code) 
    WHERE concession_pickup_code IS NOT NULL;

-- Seed Sample Branch Gallery Images
INSERT INTO catalog.branch_images (image_id, branch_id, image_url, caption, display_order, is_primary) VALUES
    ('f1111111-1111-1111-1111-111111111111', '11111111-1111-1111-1111-111111111111', 'https://assets.cinema.local/branches/pp-facade.jpg', 'Grand Cinema Mall Exterior & Main Entrance', 1, true),
    ('f2222222-2222-2222-2222-222222222222', '11111111-1111-1111-1111-111111111111', 'https://assets.cinema.local/branches/pp-lobby.jpg', 'VIP Lounge and Automated Self-Service Kiosks', 2, false),
    ('f3333333-3333-3333-3333-333333333333', '11111111-1111-1111-1111-111111111111', 'https://assets.cinema.local/branches/pp-imax-hall.jpg', 'Auditorium 1 Laser IMAX Giant Screen', 3, false)
ON CONFLICT (image_id) DO NOTHING;

-- Seed Marketing Promotions
INSERT INTO catalog.promotions (
    promotion_id, title, subtitle, poster_url, banner_url, 
    content_text, discount_type, discount_value, promo_code, starts_at, ends_at, is_active
) VALUES 
    (
        'a1111111-1111-1111-1111-111111111111',
        'Popcorn Frenzy Wednesday',
        '50% OFF all Large Caramel and Butter Popcorn every Wednesday!',
        'https://assets.cinema.local/promos/wednesday-popcorn-poster.jpg',
        'https://assets.cinema.local/promos/wednesday-popcorn-banner.jpg',
        '### Midweek Movie Magic!\nEnjoy 50% discount on all large gourmet popcorn tubs when booking any movie showing on Wednesdays. Valid for online and box-office checkout.',
        'percentage',
        50.00,
        'POPWED50',
        now() - interval '1 day',
        now() + interval '30 days',
        true
    ),
    (
        'a2222222-2222-2222-2222-222222222222',
        'Student Movie Mania',
        'Special $3.50 tickets for students with valid student ID.',
        'https://assets.cinema.local/promos/student-mania-poster.jpg',
        'https://assets.cinema.local/promos/student-mania-banner.jpg',
        '### Exclusive Student Offer\nShow your high school or university student card at any counter terminal to unlock flat $3.50 standard movie tickets from Monday to Thursday before 6:00 PM.',
        'fixed',
        3.50,
        'STUDENTPASS',
        now() - interval '5 days',
        now() + interval '60 days',
        true
    )
ON CONFLICT (promotion_id) DO NOTHING;

-- Seed Global System Notifications
INSERT INTO catalog.global_notifications (
    notification_id, title, message, type, action_url, starts_at, expires_at, is_active
) VALUES 
    (
        'e1111111-1111-1111-1111-111111111111',
        'New IMAX with Laser Hall Open!',
        'Experience crystal-clear 4K laser projection and thunderous 12-channel surround sound in Auditorium 1.',
        'promo',
        '/movies/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        now() - interval '1 day',
        now() + interval '14 days',
        true
    )
ON CONFLICT (notification_id) DO NOTHING;





