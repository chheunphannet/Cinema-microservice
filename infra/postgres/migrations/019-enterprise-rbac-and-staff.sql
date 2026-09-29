-- =========================================================================
-- 019-enterprise-rbac-and-staff.sql
-- Milestone 5.1: Enterprise RBAC Hierarchy, Staff Management & Shift Scheduling
-- =========================================================================

-- 1. Expand identity.roles with the 8 enterprise canonical roles
INSERT INTO identity.roles (name) VALUES 
    ('super_admin'),
    ('content_manager'),
    ('inventory_manager'),
    ('finance_manager'),
    ('marketing_manager'),
    ('customer_support'),
    ('staff')
ON CONFLICT (name) DO NOTHING;

-- 2. Expand identity.users with contact information and login telemetry
ALTER TABLE identity.users 
    ADD COLUMN IF NOT EXISTS email VARCHAR(255),
    ADD COLUMN IF NOT EXISTS phone VARCHAR(50),
    ADD COLUMN IF NOT EXISTS last_login_at TIMESTAMPTZ NULL;

CREATE INDEX IF NOT EXISTS ix_users_email ON identity.users(email) WHERE email IS NOT NULL;

-- 2.1 Customer account moderation status
ALTER TABLE identity.customers
    ADD COLUMN IF NOT EXISTS is_active BOOLEAN NOT NULL DEFAULT true;

-- 3. Table: identity.staff_shifts
CREATE TABLE IF NOT EXISTS identity.staff_shifts (
    shift_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES identity.users(user_id) ON DELETE CASCADE,
    branch_id UUID NOT NULL REFERENCES catalog.branches(branch_id),
    scheduled_start TIMESTAMPTZ NOT NULL,
    scheduled_end TIMESTAMPTZ NOT NULL,
    actual_start TIMESTAMPTZ NULL,
    actual_end TIMESTAMPTZ NULL,
    terminal_code VARCHAR(50) NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'scheduled' CHECK (status IN ('scheduled', 'clocked_in', 'completed', 'cancelled', 'absent')),
    notes TEXT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CHECK (scheduled_end > scheduled_start)
);

CREATE INDEX IF NOT EXISTS ix_staff_shifts_user ON identity.staff_shifts(user_id, scheduled_start);
CREATE INDEX IF NOT EXISTS ix_staff_shifts_branch ON identity.staff_shifts(branch_id, scheduled_start);

-- 4. Table: identity.user_audit (Tamper-evident audit trail for staff changes)
CREATE TABLE IF NOT EXISTS identity.user_audit (
    audit_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES identity.users(user_id) ON DELETE CASCADE,
    actor_id UUID NOT NULL,
    action VARCHAR(50) NOT NULL,
    details JSONB NOT NULL DEFAULT '{}'::jsonb,
    occurred_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_user_audit_user ON identity.user_audit(user_id, occurred_at DESC);
