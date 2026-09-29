-- 013-customer-accounts.sql
CREATE TABLE IF NOT EXISTS identity.customers (
    customer_id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    email VARCHAR(255) NOT NULL UNIQUE,
    password_hash VARCHAR(255) NOT NULL,
    first_name VARCHAR(100) NOT NULL,
    last_name VARCHAR(100) NOT NULL,
    phone VARCHAR(50),
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

ALTER TABLE pos.orders 
ADD COLUMN IF NOT EXISTS customer_id UUID NULL;

-- Create an index to quickly lookup a customer's order history
CREATE INDEX IF NOT EXISTS idx_pos_orders_customer_id ON pos.orders (customer_id);
CREATE INDEX IF NOT EXISTS idx_reservations_customer_id ON reservations.reservations (customer_id);
