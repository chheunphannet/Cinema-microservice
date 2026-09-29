-- Phase 1 canonical PostgreSQL schema. PostgreSQL primary is the write authority.
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- =========================================================================
-- Phase 1 Technical Architecture Specification Module 1.1 Canonical Tables
-- =========================================================================
CREATE TABLE IF NOT EXISTS Branches (
    branch_id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(255) NOT NULL,
    location_details TEXT,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS Movies (
    movie_id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    title VARCHAR(500) NOT NULL,
    duration_minutes INTEGER NOT NULL,
    genre VARCHAR(100),
    rating NUMERIC(3, 1)
);

CREATE TABLE IF NOT EXISTS AuditoriumLayouts (
    auditorium_id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    branch_id UUID REFERENCES Branches(branch_id),
    seat_map JSONB NOT NULL
);

CREATE TABLE IF NOT EXISTS Seats (
    seat_id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    branch_id UUID REFERENCES Branches(branch_id),
    row_identifier CHAR(2) NOT NULL,
    seat_number INTEGER NOT NULL,
    seat_type VARCHAR(50) DEFAULT 'Standard',
    UNIQUE(branch_id, row_identifier, seat_number)
);

CREATE TABLE IF NOT EXISTS Transactions (
    transaction_id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    idempotency_key UUID UNIQUE NOT NULL,
    user_id UUID NOT NULL,
    seat_id UUID REFERENCES Seats(seat_id),
    amount NUMERIC(12, 2) NOT NULL,
    status VARCHAR(50) DEFAULT 'Pending',
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- =========================================================================
-- Service-Partitioned Schemas (Shared-Nothing Architecture)
-- =========================================================================
CREATE SCHEMA IF NOT EXISTS identity;
CREATE SCHEMA IF NOT EXISTS catalog;
CREATE SCHEMA IF NOT EXISTS reservations;
CREATE SCHEMA IF NOT EXISTS pos;
CREATE SCHEMA IF NOT EXISTS tickets;

DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'reservation_status') THEN CREATE TYPE reservation_status AS ENUM ('hold','confirmed','cancelled','expired'); END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'order_status') THEN CREATE TYPE order_status AS ENUM ('draft','pending_payment','paid','cancelled','refunded'); END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'payment_method') THEN CREATE TYPE payment_method AS ENUM ('cash','card','qr','voucher','other'); END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'ticket_status') THEN CREATE TYPE ticket_status AS ENUM ('active','void','used'); END IF; END $$;

CREATE TABLE identity.roles (role_id uuid PRIMARY KEY DEFAULT gen_random_uuid(), name varchar(50) UNIQUE NOT NULL, created_at timestamptz NOT NULL DEFAULT now());
CREATE TABLE identity.users (user_id uuid PRIMARY KEY DEFAULT gen_random_uuid(), branch_id uuid, username varchar(100) UNIQUE NOT NULL, display_name varchar(200) NOT NULL, pin_hash text, is_active boolean NOT NULL DEFAULT true, created_at timestamptz NOT NULL DEFAULT now());
CREATE TABLE identity.user_roles (user_id uuid NOT NULL REFERENCES identity.users ON DELETE CASCADE, role_id uuid NOT NULL REFERENCES identity.roles ON DELETE CASCADE, PRIMARY KEY (user_id, role_id));

CREATE TABLE catalog.branches (branch_id uuid PRIMARY KEY DEFAULT gen_random_uuid(), code varchar(20) UNIQUE NOT NULL, name varchar(255) NOT NULL, address text, timezone varchar(64) NOT NULL DEFAULT 'Asia/Phnom_Penh', is_active boolean NOT NULL DEFAULT true, created_at timestamptz NOT NULL DEFAULT now());
ALTER TABLE identity.users ADD CONSTRAINT fk_user_branch FOREIGN KEY (branch_id) REFERENCES catalog.branches(branch_id);
CREATE TABLE catalog.auditoriums (auditorium_id uuid PRIMARY KEY DEFAULT gen_random_uuid(), branch_id uuid NOT NULL REFERENCES catalog.branches, name varchar(100) NOT NULL, capacity integer NOT NULL CHECK (capacity > 0), UNIQUE(branch_id, name));
CREATE TABLE catalog.seats (seat_id uuid PRIMARY KEY DEFAULT gen_random_uuid(), auditorium_id uuid NOT NULL REFERENCES catalog.auditoriums ON DELETE CASCADE, row_label varchar(4) NOT NULL, seat_number integer NOT NULL CHECK (seat_number > 0), seat_type varchar(30) NOT NULL DEFAULT 'standard', is_accessible boolean NOT NULL DEFAULT false, is_active boolean NOT NULL DEFAULT true, UNIQUE(auditorium_id, row_label, seat_number));
CREATE TABLE catalog.auditorium_layouts (auditorium_id uuid PRIMARY KEY REFERENCES catalog.auditoriums(auditorium_id) ON DELETE CASCADE, branch_id uuid NOT NULL REFERENCES catalog.branches(branch_id), seat_map jsonb NOT NULL);
CREATE TABLE catalog.movies (movie_id uuid PRIMARY KEY DEFAULT gen_random_uuid(), title varchar(500) NOT NULL, duration_minutes integer NOT NULL CHECK (duration_minutes > 0), genre varchar(100), classification varchar(20), poster_url text, is_active boolean NOT NULL DEFAULT true, created_at timestamptz NOT NULL DEFAULT now());
CREATE TABLE catalog.showtimes (showtime_id uuid PRIMARY KEY DEFAULT gen_random_uuid(), movie_id uuid NOT NULL REFERENCES catalog.movies, auditorium_id uuid NOT NULL REFERENCES catalog.auditoriums, starts_at timestamptz NOT NULL, ends_at timestamptz NOT NULL, base_price numeric(12,2) NOT NULL CHECK (base_price >= 0), status varchar(20) NOT NULL DEFAULT 'scheduled' CHECK (status IN ('scheduled','cancelled','completed')), created_at timestamptz NOT NULL DEFAULT now(), CHECK (ends_at > starts_at), UNIQUE(auditorium_id, starts_at));
CREATE INDEX ix_showtimes_auditorium_start ON catalog.showtimes(auditorium_id, starts_at);

CREATE TABLE reservations.reservations (reservation_id uuid PRIMARY KEY DEFAULT gen_random_uuid(), showtime_id uuid NOT NULL, customer_id uuid, status reservation_status NOT NULL DEFAULT 'hold', hold_expires_at timestamptz, idempotency_key uuid NOT NULL UNIQUE, fencing_token bigint NOT NULL DEFAULT 0, created_at timestamptz NOT NULL DEFAULT now(), confirmed_at timestamptz, CHECK ((status = 'hold' AND hold_expires_at IS NOT NULL) OR status <> 'hold'));
CREATE TABLE reservations.reservation_seats (reservation_id uuid NOT NULL REFERENCES reservations.reservations ON DELETE CASCADE, seat_id uuid NOT NULL, price numeric(12,2) NOT NULL CHECK (price >= 0), PRIMARY KEY(reservation_id, seat_id));
-- Final ACID double-booking guard. A reservation cannot be confirmed twice for a seat/showtime.
CREATE TABLE reservations.confirmed_seats (showtime_id uuid NOT NULL, seat_id uuid NOT NULL, reservation_id uuid NOT NULL REFERENCES reservations.reservations, booked_at timestamptz NOT NULL DEFAULT now(), PRIMARY KEY(showtime_id, seat_id));
CREATE INDEX ix_reservations_showtime_status ON reservations.reservations(showtime_id, status);

CREATE TABLE pos.products (product_id uuid PRIMARY KEY DEFAULT gen_random_uuid(), branch_id uuid NOT NULL REFERENCES catalog.branches, sku varchar(64) NOT NULL, name varchar(200) NOT NULL, category varchar(50) NOT NULL, unit_price numeric(12,2) NOT NULL CHECK(unit_price >= 0), is_active boolean NOT NULL DEFAULT true, UNIQUE(branch_id, sku));
CREATE TABLE pos.orders (order_id uuid PRIMARY KEY DEFAULT gen_random_uuid(), branch_id uuid NOT NULL REFERENCES catalog.branches, cashier_id uuid, reservation_id uuid UNIQUE, status order_status NOT NULL DEFAULT 'draft', subtotal numeric(12,2) NOT NULL DEFAULT 0 CHECK(subtotal >= 0), discount_amount numeric(12,2) NOT NULL DEFAULT 0 CHECK(discount_amount >= 0), total_amount numeric(12,2) NOT NULL DEFAULT 0 CHECK(total_amount >= 0), idempotency_key uuid NOT NULL UNIQUE, created_at timestamptz NOT NULL DEFAULT now(), completed_at timestamptz);
CREATE TABLE pos.order_lines (order_line_id uuid PRIMARY KEY DEFAULT gen_random_uuid(), order_id uuid NOT NULL REFERENCES pos.orders ON DELETE CASCADE, product_id uuid REFERENCES pos.products, description varchar(300) NOT NULL, quantity integer NOT NULL CHECK(quantity > 0), unit_price numeric(12,2) NOT NULL CHECK(unit_price >= 0), line_total numeric(12,2) NOT NULL CHECK(line_total >= 0));
CREATE TABLE pos.payments (payment_id uuid PRIMARY KEY DEFAULT gen_random_uuid(), order_id uuid NOT NULL REFERENCES pos.orders, method payment_method NOT NULL, amount numeric(12,2) NOT NULL CHECK(amount > 0), provider_reference varchar(200), status varchar(30) NOT NULL DEFAULT 'captured', paid_at timestamptz NOT NULL DEFAULT now());
CREATE TABLE pos.till_shifts (shift_id uuid PRIMARY KEY DEFAULT gen_random_uuid(), branch_id uuid NOT NULL REFERENCES catalog.branches, cashier_id uuid, terminal_code varchar(50) NOT NULL, opened_at timestamptz NOT NULL DEFAULT now(), closed_at timestamptz, opening_float numeric(12,2) NOT NULL DEFAULT 0, closing_cash numeric(12,2), status varchar(20) NOT NULL DEFAULT 'open' CHECK(status IN ('open','closed')), UNIQUE(branch_id, terminal_code, opened_at));

-- Phase 3 Step 3: Performance Indexes
CREATE INDEX ix_orders_status_created ON pos.orders(status, created_at);
CREATE INDEX ix_order_lines_order_id ON pos.order_lines(order_id);
CREATE INDEX ix_payments_order_id ON pos.payments(order_id);

CREATE TABLE tickets.tickets (ticket_id uuid PRIMARY KEY DEFAULT gen_random_uuid(), reservation_id uuid NOT NULL, showtime_id uuid NOT NULL, seat_id uuid NOT NULL, qr_token_hash char(64) NOT NULL UNIQUE, status ticket_status NOT NULL DEFAULT 'active', is_printed boolean NOT NULL DEFAULT false, printed_at timestamptz, redeemed_at timestamptz, created_at timestamptz NOT NULL DEFAULT now(), UNIQUE(reservation_id, seat_id));
CREATE INDEX ix_tickets_showtime_status ON tickets.tickets(showtime_id, status);
CREATE TABLE tickets.redemption_audit (audit_id uuid PRIMARY KEY DEFAULT gen_random_uuid(), ticket_id uuid NOT NULL REFERENCES tickets.tickets, actor_id uuid, action varchar(40) NOT NULL, terminal_code varchar(50), occurred_at timestamptz NOT NULL DEFAULT now(), metadata jsonb NOT NULL DEFAULT '{}'::jsonb);

CREATE TABLE public.audit_log (audit_id uuid PRIMARY KEY DEFAULT gen_random_uuid(), occurred_at timestamptz NOT NULL DEFAULT now(), service_name varchar(80) NOT NULL, actor_id uuid, action varchar(100) NOT NULL, entity_type varchar(80) NOT NULL, entity_id uuid, correlation_id uuid, before_data jsonb, after_data jsonb);
CREATE INDEX ix_audit_log_entity ON public.audit_log(entity_type, entity_id, occurred_at DESC);

INSERT INTO identity.roles(name) VALUES ('cashier'),('supervisor'),('branch_manager'),('system_admin') ON CONFLICT DO NOTHING;

