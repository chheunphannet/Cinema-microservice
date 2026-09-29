-- Migration 018: Customer Social Auth (Google Sign-In) & Refresh Tokens
-- Adds social auth columns to identity.customers and ensures refresh token storage table exists.

-- 1. Extend identity.customers for Google Social Auth
ALTER TABLE identity.customers 
    ADD COLUMN IF NOT EXISTS google_id VARCHAR(255) UNIQUE,
    ADD COLUMN IF NOT EXISTS avatar_url TEXT,
    ADD COLUMN IF NOT EXISTS auth_provider VARCHAR(50) NOT NULL DEFAULT 'local',
    ADD COLUMN IF NOT EXISTS email_verified BOOLEAN NOT NULL DEFAULT false;

-- Allow password_hash to be NULL for customers registering via external OAuth providers
ALTER TABLE identity.customers 
    ALTER COLUMN password_hash DROP NOT NULL;

-- Create index on google_id for fast lookup during social authentication
CREATE INDEX IF NOT EXISTS ix_customers_google_id ON identity.customers (google_id);

-- 2. Ensure identity.refresh_tokens table exists for refresh token management
CREATE TABLE IF NOT EXISTS identity.refresh_tokens (
    token_hash VARCHAR(64) PRIMARY KEY,
    user_id UUID NOT NULL,
    username VARCHAR(255) NOT NULL,
    role VARCHAR(50) NOT NULL,
    branch_id UUID NULL,
    expires_at TIMESTAMPTZ NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    is_revoked BOOLEAN NOT NULL DEFAULT false,
    revoked_at TIMESTAMPTZ NULL
);

CREATE INDEX IF NOT EXISTS idx_refresh_tokens_user_id ON identity.refresh_tokens (user_id);
