-- =========================================================================
-- 022-refunds-and-disputes.sql
-- Milestone 5.4: Customer Support, Booking Dispute & Transaction Operations
-- =========================================================================

-- 1. Extend enum types for refund workflows
ALTER TYPE reservation_status ADD VALUE IF NOT EXISTS 'refunded';
ALTER TYPE reservation_status ADD VALUE IF NOT EXISTS 'partially_refunded';
ALTER TYPE order_status ADD VALUE IF NOT EXISTS 'partially_refunded';

-- 2. Extend reservation seats with item status
ALTER TABLE reservations.reservation_seats 
    ADD COLUMN IF NOT EXISTS status VARCHAR(30) NOT NULL DEFAULT 'confirmed';

-- 3. Table: pos.refunds (Atomic Partial / Full Refund Tracking & Auditing)
CREATE TABLE IF NOT EXISTS pos.refunds (
    refund_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    order_id UUID NULL REFERENCES pos.orders(order_id),
    reservation_id UUID NULL REFERENCES reservations.reservations(reservation_id),
    refund_amount NUMERIC(12,2) NOT NULL CHECK (refund_amount > 0),
    reason_code VARCHAR(50) NOT NULL CHECK (reason_code IN ('customer_request', 'cancelled_showtime', 'technical_issue', 'chargeback', 'duplicate_booking', 'other')),
    notes TEXT NULL,
    authorized_by UUID NOT NULL,
    refunded_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_refunds_order ON pos.refunds(order_id);
CREATE INDEX IF NOT EXISTS ix_refunds_reservation ON pos.refunds(reservation_id);
CREATE INDEX IF NOT EXISTS ix_refunds_refunded_at ON pos.refunds(refunded_at);

-- 4. Table: pos.disputes (Payment Disputes & Chargebacks)
CREATE TABLE IF NOT EXISTS pos.disputes (
    dispute_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    order_id UUID NULL REFERENCES pos.orders(order_id),
    reservation_id UUID NULL REFERENCES reservations.reservations(reservation_id),
    provider_dispute_id VARCHAR(100) NULL,
    status VARCHAR(30) NOT NULL DEFAULT 'open' CHECK (status IN ('open', 'under_review', 'won', 'lost', 'accepted')),
    amount NUMERIC(12,2) NOT NULL CHECK (amount >= 0),
    evidence_notes TEXT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    resolved_at TIMESTAMPTZ NULL
);

CREATE INDEX IF NOT EXISTS ix_disputes_order ON pos.disputes(order_id);
CREATE INDEX IF NOT EXISTS ix_disputes_reservation ON pos.disputes(reservation_id);
CREATE INDEX IF NOT EXISTS ix_disputes_status ON pos.disputes(status);
