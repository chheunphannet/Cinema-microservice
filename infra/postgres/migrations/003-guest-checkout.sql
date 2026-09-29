-- Phase 3 Schema Migration: Guest Checkout & Pluggable Digital Delivery

-- 1. Dual-Identity Enhancements for Reservations
ALTER TABLE reservations.reservations 
    ADD COLUMN IF NOT EXISTS guest_email VARCHAR(255) NULL,
    ADD COLUMN IF NOT EXISTS guest_phone VARCHAR(50) NULL,
    ADD COLUMN IF NOT EXISTS guest_name VARCHAR(150) NULL,
    ADD COLUMN IF NOT EXISTS is_guest BOOLEAN NOT NULL DEFAULT false;

-- Add index on guest_email for fast lookup
CREATE INDEX IF NOT EXISTS ix_reservations_guest_email 
    ON reservations.reservations(guest_email) 
    WHERE guest_email IS NOT NULL;

-- Ensure either registered customer_id OR guest_email is supplied for guest reservations
DO $$ 
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'chk_reservation_identity') THEN
        ALTER TABLE reservations.reservations 
            ADD CONSTRAINT chk_reservation_identity CHECK (
                (is_guest = false) OR 
                (is_guest = true AND (guest_email IS NOT NULL OR status IN ('hold', 'expired', 'cancelled')))
            );
    END IF;
END $$;

-- 2. POS Order Channel & Guest Attribution
ALTER TABLE pos.orders 
    ADD COLUMN IF NOT EXISTS channel VARCHAR(20) NOT NULL DEFAULT 'pos',
    ADD COLUMN IF NOT EXISTS customer_email VARCHAR(255) NULL,
    ADD COLUMN IF NOT EXISTS customer_phone VARCHAR(50) NULL;

-- Check constraint for channel
DO $$ 
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'chk_order_channel') THEN
        ALTER TABLE pos.orders ADD CONSTRAINT chk_order_channel CHECK (channel IN ('pos', 'web', 'kiosk', 'mobile'));
    END IF;
END $$;

-- For web orders, cashier_id is null; for pos completed orders, cashier_id is required
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

-- 3. Digital Ticket Delivery Tracking
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

-- 4. F&B Concessions Digital Pickup Tracking for Guest & Online Orders
ALTER TABLE pos.orders 
    ADD COLUMN IF NOT EXISTS concession_pickup_code VARCHAR(64) NULL,
    ADD COLUMN IF NOT EXISTS concession_status VARCHAR(30) NOT NULL DEFAULT 'none';

ALTER TABLE pos.order_lines 
    ADD COLUMN IF NOT EXISTS is_fulfilled BOOLEAN NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS fulfilled_at TIMESTAMPTZ NULL;

CREATE INDEX IF NOT EXISTS ix_pos_orders_concession_pickup 
    ON pos.orders(concession_pickup_code) 
    WHERE concession_pickup_code IS NOT NULL;

