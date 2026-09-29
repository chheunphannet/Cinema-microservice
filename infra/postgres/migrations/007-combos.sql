-- Phase 2 Module 3: Combo Recognition Engine Schema
CREATE TABLE IF NOT EXISTS catalog.combos (
    combo_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(100) NOT NULL,
    price NUMERIC(12, 2) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS catalog.combo_items (
    combo_item_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    combo_id UUID NOT NULL REFERENCES catalog.combos(combo_id) ON DELETE CASCADE,
    item_type VARCHAR(50) NOT NULL CHECK (item_type IN ('ticket', 'product')),
    target_id UUID NOT NULL, -- Either ticket_type_id or product_id
    quantity INTEGER NOT NULL CHECK (quantity > 0)
);

-- Seed Data for Combos

-- 1. Family Pass Combo: $45.00
-- Consists of: 2x Adult Tickets, 2x Child Tickets
INSERT INTO catalog.combos (combo_id, name, price)
VALUES ('c1111111-1111-1111-1111-111111111111', 'Family Pass', 45.00)
ON CONFLICT DO NOTHING;

-- 2 Adult Tickets
INSERT INTO catalog.combo_items (combo_id, item_type, target_id, quantity)
VALUES ('c1111111-1111-1111-1111-111111111111', 'ticket', 'a1111111-1111-1111-1111-111111111111', 2)
ON CONFLICT DO NOTHING;
-- 2 Child Tickets
INSERT INTO catalog.combo_items (combo_id, item_type, target_id, quantity)
VALUES ('c1111111-1111-1111-1111-111111111111', 'ticket', 'b1111111-1111-1111-1111-111111111111', 2)
ON CONFLICT DO NOTHING;

-- 2. Couples Date Night: $35.00
-- Consists of: 2x Adult Tickets, 1x Large Popcorn (d1111111-1111-1111-1111-111111111111), 2x Large Drink (d2222222-2222-2222-2222-222222222222)
INSERT INTO catalog.combos (combo_id, name, price)
VALUES ('c2222222-2222-2222-2222-222222222222', 'Couples Date Night', 35.00)
ON CONFLICT DO NOTHING;

-- 2 Adult Tickets
INSERT INTO catalog.combo_items (combo_id, item_type, target_id, quantity)
VALUES ('c2222222-2222-2222-2222-222222222222', 'ticket', 'a1111111-1111-1111-1111-111111111111', 2)
ON CONFLICT DO NOTHING;
-- 1 Large Popcorn
INSERT INTO catalog.combo_items (combo_id, item_type, target_id, quantity)
VALUES ('c2222222-2222-2222-2222-222222222222', 'product', 'd1111111-1111-1111-1111-111111111111', 1)
ON CONFLICT DO NOTHING;
-- 2 Large Soft Drinks
INSERT INTO catalog.combo_items (combo_id, item_type, target_id, quantity)
VALUES ('c2222222-2222-2222-2222-222222222222', 'product', 'd2222222-2222-2222-2222-222222222222', 2)
ON CONFLICT DO NOTHING;
