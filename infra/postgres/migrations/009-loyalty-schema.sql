-- Phase 2 Step 4: Loyalty, CRM & Vouchers Schema

CREATE SCHEMA IF NOT EXISTS loyalty;

-- 1. Loyalty Tiers (Dynamic rules for points accrual)
CREATE TABLE loyalty.tiers (
    tier_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(50) NOT NULL UNIQUE,
    points_multiplier NUMERIC(5,2) NOT NULL DEFAULT 1.00, -- e.g. 1.0x, 1.5x
    points_per_dollar NUMERIC(12,2) NOT NULL DEFAULT 10.00, -- Base points per unit of currency
    minimum_spend_to_qualify NUMERIC(12,2) NOT NULL DEFAULT 0.00,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Seed initial tiers
INSERT INTO loyalty.tiers (name, points_multiplier, points_per_dollar, minimum_spend_to_qualify)
VALUES 
('Bronze', 1.0, 10.00, 0),
('Silver', 1.2, 10.00, 500),
('Gold', 1.5, 10.00, 2000);

-- 2. Members (CRM Profiles)
CREATE TABLE loyalty.members (
    member_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    first_name VARCHAR(100) NOT NULL,
    last_name VARCHAR(100) NOT NULL,
    email VARCHAR(255) NOT NULL UNIQUE,
    phone VARCHAR(20),
    tier_id UUID NOT NULL REFERENCES loyalty.tiers(tier_id),
    total_points_balance INT NOT NULL DEFAULT 0 CHECK (total_points_balance >= 0),
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Seed a test member (assuming Bronze tier ID is needed, we'll fetch it dynamically during seed)
DO $$
DECLARE
    bronze_id UUID;
    gold_id UUID;
BEGIN
    SELECT tier_id INTO bronze_id FROM loyalty.tiers WHERE name = 'Bronze';
    SELECT tier_id INTO gold_id FROM loyalty.tiers WHERE name = 'Gold';

    INSERT INTO loyalty.members (first_name, last_name, email, phone, tier_id, total_points_balance)
    VALUES 
    ('John', 'Doe', 'john.doe@example.com', '+15550100', bronze_id, 500),
    ('Jane', 'Smith', 'jane.smith@example.com', '+15550200', gold_id, 12500);
END $$;

-- 3. Points Ledger (Strict append-only audit trail)
CREATE TYPE loyalty.ledger_transaction_type AS ENUM ('earn_purchase', 'redeem_purchase', 'adjustment', 'refund');

CREATE TABLE loyalty.points_ledger (
    ledger_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    member_id UUID NOT NULL REFERENCES loyalty.members(member_id),
    transaction_type loyalty.ledger_transaction_type NOT NULL,
    points_delta INT NOT NULL, -- positive for earn, negative for redeem
    reference_order_id UUID, -- Links to pos.orders
    description VARCHAR(255) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- 4. Vouchers (Corporate Passes, BOGO, Free Tickets)
CREATE TYPE loyalty.voucher_type AS ENUM ('free_ticket', 'percentage_discount', 'fixed_discount', 'bogo');

CREATE TABLE loyalty.vouchers (
    voucher_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code VARCHAR(50) NOT NULL UNIQUE,
    voucher_type loyalty.voucher_type NOT NULL,
    target_item_type VARCHAR(50) NOT NULL, -- e.g., 'ticket', 'popcorn', 'order_total'
    discount_value NUMERIC(12,2) NOT NULL, -- e.g., 100 for 100% off, or 5 for $5.00 off
    is_redeemed BOOLEAN NOT NULL DEFAULT false,
    redeemed_at TIMESTAMPTZ,
    redeemed_order_id UUID,
    expires_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Seed some vouchers
INSERT INTO loyalty.vouchers (code, voucher_type, target_item_type, discount_value, expires_at)
VALUES 
('CORP-FREE-TK-001', 'free_ticket', 'ticket', 100.00, now() + interval '1 year'),
('PROMO-BOGO-POP', 'bogo', 'large_popcorn', 100.00, now() + interval '30 days'),
('WELCOME-5OFF', 'fixed_discount', 'order_total', 5.00, now() + interval '6 months');

-- Add a trigger to update member total points on ledger insert
CREATE OR REPLACE FUNCTION loyalty.update_member_points_balance()
RETURNS TRIGGER AS $$
BEGIN
    UPDATE loyalty.members
    SET total_points_balance = total_points_balance + NEW.points_delta,
        updated_at = now()
    WHERE member_id = NEW.member_id;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_update_points_balance
AFTER INSERT ON loyalty.points_ledger
FOR EACH ROW
EXECUTE FUNCTION loyalty.update_member_points_balance();
