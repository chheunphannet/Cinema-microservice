-- Phase 3 Module 4: Concessions / F&B Pickup & Fulfillment Lifecycle Tracking
ALTER TABLE pos.order_lines ADD COLUMN IF NOT EXISTS fulfillment_status VARCHAR(20) NOT NULL DEFAULT 'pending';
ALTER TABLE pos.order_lines ADD COLUMN IF NOT EXISTS fulfilled_at TIMESTAMPTZ NULL;
ALTER TABLE pos.order_lines ADD COLUMN IF NOT EXISTS fulfillment_staff_id UUID NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'chk_order_lines_fulfillment_status'
    ) THEN
        ALTER TABLE pos.order_lines 
        ADD CONSTRAINT chk_order_lines_fulfillment_status 
        CHECK (fulfillment_status IN ('pending', 'preparing', 'ready', 'collected', 'cancelled'));
    END IF;
END $$;
