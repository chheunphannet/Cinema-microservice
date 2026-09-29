-- Phase 2: Dynamic Pricing & Ticket Types

-- 1. Create Ticket Types
CREATE TABLE catalog.ticket_types (
    ticket_type_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code VARCHAR(20) UNIQUE NOT NULL, -- 'ADULT', 'CHILD', 'SENIOR', 'STUDENT'
    name VARCHAR(100) NOT NULL,
    is_active BOOLEAN DEFAULT true
);

-- 2. Create Price Cards
CREATE TABLE catalog.price_cards (
    price_card_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(100) NOT NULL,
    description TEXT,
    is_active BOOLEAN DEFAULT true
);

-- 3. Create Price Card Entries (The matrix)
CREATE TABLE catalog.price_card_entries (
    entry_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    price_card_id UUID REFERENCES catalog.price_cards ON DELETE CASCADE,
    ticket_type_id UUID REFERENCES catalog.ticket_types,
    seat_type VARCHAR(30) NOT NULL, -- 'standard', 'vip', 'accessible'
    price NUMERIC(12,2) NOT NULL CHECK(price >= 0),
    UNIQUE(price_card_id, ticket_type_id, seat_type)
);

-- 4. Add price_card_id to showtimes
ALTER TABLE catalog.showtimes 
    ADD COLUMN price_card_id UUID REFERENCES catalog.price_cards;


-- SEED DATA --

-- Seed Ticket Types
INSERT INTO catalog.ticket_types (ticket_type_id, code, name) VALUES 
('a1111111-1111-1111-1111-111111111111', 'ADULT', 'Adult'),
('a2222222-2222-2222-2222-222222222222', 'CHILD', 'Child (Under 12)'),
('a3333333-3333-3333-3333-333333333333', 'SENIOR', 'Senior (65+)');

-- Seed a Default Price Card
INSERT INTO catalog.price_cards (price_card_id, name, description) VALUES 
('b1111111-1111-1111-1111-111111111111', 'Standard 2D', 'Default standard pricing');

-- Seed entries for Default Price Card
INSERT INTO catalog.price_card_entries (price_card_id, ticket_type_id, seat_type, price) VALUES 
-- Adult prices
('b1111111-1111-1111-1111-111111111111', 'a1111111-1111-1111-1111-111111111111', 'standard', 8.50),
('b1111111-1111-1111-1111-111111111111', 'a1111111-1111-1111-1111-111111111111', 'vip', 12.00),

-- Child prices (discounted)
('b1111111-1111-1111-1111-111111111111', 'a2222222-2222-2222-2222-222222222222', 'standard', 6.00),
('b1111111-1111-1111-1111-111111111111', 'a2222222-2222-2222-2222-222222222222', 'vip', 12.00), -- Child pays full VIP price

-- Senior prices
('b1111111-1111-1111-1111-111111111111', 'a3333333-3333-3333-3333-333333333333', 'standard', 6.50),
('b1111111-1111-1111-1111-111111111111', 'a3333333-3333-3333-3333-333333333333', 'vip', 12.00);

-- Assign to existing showtimes (so they don't break)
UPDATE catalog.showtimes SET price_card_id = 'b1111111-1111-1111-1111-111111111111' WHERE price_card_id IS NULL;

-- Make it NOT NULL for future
-- ALTER TABLE catalog.showtimes ALTER COLUMN price_card_id SET NOT NULL;
