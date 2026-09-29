-- =========================================================================
-- 023-system-feature-flags.sql
-- Milestone 5.5: Marketing CRM, Campaigns & Technical System Admin
-- =========================================================================

-- 1. System Feature Flags Table (Public Schema)
CREATE TABLE IF NOT EXISTS public.system_feature_flags (
    key VARCHAR(100) PRIMARY KEY,
    description TEXT NOT NULL,
    is_enabled BOOLEAN NOT NULL DEFAULT false,
    environment VARCHAR(30) NOT NULL DEFAULT 'all',
    updated_by UUID NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Seed default feature flags
INSERT INTO public.system_feature_flags (key, description, is_enabled, environment, updated_at)
VALUES
    ('enable_google_login', 'Enables Google social authentication on customer sign-in', true, 'all', now()),
    ('enable_dynamic_surge', 'Enables weekend and peak hour surge pricing multipliers', true, 'all', now()),
    ('enable_kiosk_mode', 'Enables unattended kiosk POS workflow and interfaces', true, 'all', now()),
    ('maintenance_banner', 'Displays global system maintenance banner across customer apps', false, 'all', now()),
    ('require_email_verification', 'Requires email verification before customer ticket purchase', false, 'all', now()),
    ('allow_guest_checkout', 'Allows anonymous checkout with email confirmation', true, 'all', now())
ON CONFLICT (key) DO NOTHING;

-- 2. System-Wide Tamper-Proof Audit Log
CREATE TABLE IF NOT EXISTS public.system_audit_log (
    log_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    actor_id UUID NOT NULL,
    actor_email VARCHAR(255) NULL,
    actor_role VARCHAR(50) NULL,
    service_name VARCHAR(50) NOT NULL,
    action VARCHAR(100) NOT NULL,
    resource_type VARCHAR(50) NOT NULL,
    resource_id VARCHAR(100) NULL,
    details JSONB NOT NULL DEFAULT '{}'::jsonb,
    ip_address VARCHAR(45) NULL,
    occurred_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_audit_actor ON public.system_audit_log(actor_id, occurred_at DESC);
CREATE INDEX IF NOT EXISTS ix_audit_service_action ON public.system_audit_log(service_name, action, occurred_at DESC);
CREATE INDEX IF NOT EXISTS ix_audit_occurred ON public.system_audit_log(occurred_at DESC);

-- 3. Loyalty Schema Enhancements for Marketing Campaigns & Platinum Tier
INSERT INTO loyalty.tiers (name, points_multiplier, points_per_dollar, minimum_spend_to_qualify)
VALUES ('Platinum', 2.0, 10.00, 5000.00)
ON CONFLICT (name) DO NOTHING;

-- Table: loyalty.campaigns
CREATE TABLE IF NOT EXISTS loyalty.campaigns (
    campaign_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(100) NOT NULL,
    description TEXT NULL,
    target_segment VARCHAR(50) NOT NULL DEFAULT 'all',
    status VARCHAR(30) NOT NULL DEFAULT 'draft' CHECK (status IN ('draft', 'active', 'completed', 'cancelled')),
    discount_code VARCHAR(50) NULL,
    discount_value NUMERIC(12,2) NULL,
    starts_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    ends_at TIMESTAMPTZ NULL,
    created_by UUID NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_loyalty_campaigns_status ON loyalty.campaigns(status, starts_at);

-- Expand loyalty.vouchers with campaign linking and multi-use limits
ALTER TABLE loyalty.vouchers
    ADD COLUMN IF NOT EXISTS campaign_id UUID NULL REFERENCES loyalty.campaigns(campaign_id) ON DELETE SET NULL,
    ADD COLUMN IF NOT EXISTS max_uses INT NOT NULL DEFAULT 1,
    ADD COLUMN IF NOT EXISTS used_count INT NOT NULL DEFAULT 0;

CREATE INDEX IF NOT EXISTS ix_vouchers_campaign ON loyalty.vouchers(campaign_id) WHERE campaign_id IS NOT NULL;
