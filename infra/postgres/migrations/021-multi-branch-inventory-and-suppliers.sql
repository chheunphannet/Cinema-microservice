-- =========================================================================
-- 021-multi-branch-inventory-and-suppliers.sql
-- Milestone 5.3: Ticket Pricing Cards, Surcharges & F&B Multi-Branch Inventory
-- =========================================================================

-- 1. Dynamic Pricing Rules & Surcharges in catalog schema
CREATE TABLE IF NOT EXISTS catalog.pricing_rules (
    rule_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(100) NOT NULL,
    rule_type VARCHAR(50) NOT NULL CHECK (rule_type IN ('day_of_week', 'matinee', 'weekend_surge', 'format_surcharge')),
    day_of_week INT NULL CHECK (day_of_week BETWEEN 0 AND 6),
    start_time TIME NULL,
    end_time TIME NULL,
    screen_type_code VARCHAR(30) NULL,
    adjustment_type VARCHAR(20) NOT NULL DEFAULT 'fixed_amount' CHECK (adjustment_type IN ('fixed_amount', 'percentage')),
    adjustment_value NUMERIC(12,2) NOT NULL DEFAULT 0.00,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Seed canonical dynamic pricing rules and surcharges
INSERT INTO catalog.pricing_rules (rule_id, name, rule_type, day_of_week, start_time, end_time, screen_type_code, adjustment_type, adjustment_value, is_active)
VALUES
    ('c1111111-1111-1111-1111-111111111111', 'Cinema Wednesday Discount', 'day_of_week', 3, NULL, NULL, NULL, 'fixed_amount', -2.00, true),
    ('c2222222-2222-2222-2222-222222222222', 'Matinee Morning Discount', 'matinee', NULL, '00:00:00', '12:00:00', NULL, 'fixed_amount', -1.50, true),
    ('c3333333-3333-3333-3333-333333333333', 'Weekend Prime Surge', 'weekend_surge', NULL, NULL, NULL, NULL, 'fixed_amount', 1.50, true),
    ('c4444444-4444-4444-4444-444444444444', '3D Format Surcharge', 'format_surcharge', NULL, NULL, NULL, '3D', 'fixed_amount', 2.00, true),
    ('c5555555-5555-5555-5555-555555555555', 'IMAX Laser Surcharge', 'format_surcharge', NULL, NULL, NULL, 'IMAX', 'fixed_amount', 4.00, true)
ON CONFLICT (rule_id) DO NOTHING;

-- 2. Multi-Branch Stock Tracking in pos schema
CREATE TABLE IF NOT EXISTS pos.branch_inventory (
    inventory_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    branch_id UUID NOT NULL REFERENCES catalog.branches(branch_id),
    product_id UUID NOT NULL REFERENCES pos.products(product_id) ON DELETE CASCADE,
    stock_quantity INT NOT NULL DEFAULT 0,
    reorder_threshold INT NOT NULL DEFAULT 20,
    is_out_of_stock BOOLEAN NOT NULL DEFAULT false,
    last_restocked_at TIMESTAMPTZ NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_branch_inventory_branch_product UNIQUE(branch_id, product_id)
);

CREATE INDEX IF NOT EXISTS ix_branch_inventory_branch ON pos.branch_inventory(branch_id);
CREATE INDEX IF NOT EXISTS ix_branch_inventory_reorder ON pos.branch_inventory(branch_id, is_out_of_stock, stock_quantity);

-- Seed initial branch inventory for all existing products and branches
INSERT INTO pos.branch_inventory (branch_id, product_id, stock_quantity, reorder_threshold, is_out_of_stock, last_restocked_at)
SELECT b.branch_id, p.product_id, COALESCE(p.stock_quantity, 100), 20, false, now()
FROM catalog.branches b
CROSS JOIN pos.products p
ON CONFLICT (branch_id, product_id) DO NOTHING;

-- 3. Spoilage / Wastage Log Table
CREATE TABLE IF NOT EXISTS pos.inventory_wastage (
    wastage_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    branch_id UUID NOT NULL REFERENCES catalog.branches(branch_id),
    product_id UUID NOT NULL REFERENCES pos.products(product_id),
    quantity INT NOT NULL CHECK (quantity > 0),
    reason VARCHAR(100) NOT NULL CHECK (reason IN ('damaged', 'expired', 'dropped', 'spoilage', 'theft', 'other')),
    cost_loss NUMERIC(12,2) NOT NULL DEFAULT 0 CHECK (cost_loss >= 0),
    logged_by UUID NOT NULL,
    logged_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    notes TEXT NULL
);

CREATE INDEX IF NOT EXISTS ix_inventory_wastage_branch ON pos.inventory_wastage(branch_id, logged_at);

-- 4. Suppliers & Purchase Orders
CREATE TABLE IF NOT EXISTS pos.suppliers (
    supplier_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(150) NOT NULL,
    contact_name VARCHAR(100) NULL,
    email VARCHAR(150) NULL,
    phone VARCHAR(50) NULL,
    address TEXT NULL,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Seed default concession supplier
INSERT INTO pos.suppliers (supplier_id, name, contact_name, email, phone, address, is_active)
VALUES (
    'e1111111-1111-1111-1111-111111111111',
    'Global Cinema Concession Supplies Ltd',
    'Sarah Jenkins',
    'supplies@cinemaglobal.com',
    '+1-800-555-0199',
    '100 Industrial Parkway, Suite 400',
    true
)
ON CONFLICT (supplier_id) DO NOTHING;

CREATE TABLE IF NOT EXISTS pos.purchase_orders (
    po_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    po_number VARCHAR(50) UNIQUE NOT NULL,
    supplier_id UUID REFERENCES pos.suppliers(supplier_id),
    branch_id UUID NOT NULL REFERENCES catalog.branches(branch_id),
    status VARCHAR(30) NOT NULL DEFAULT 'draft' CHECK (status IN ('draft', 'ordered', 'received', 'cancelled')),
    total_cost NUMERIC(12,2) NOT NULL DEFAULT 0 CHECK (total_cost >= 0),
    ordered_at TIMESTAMPTZ NULL,
    received_at TIMESTAMPTZ NULL,
    notes TEXT NULL,
    created_by UUID NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS pos.purchase_order_lines (
    po_line_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    po_id UUID NOT NULL REFERENCES pos.purchase_orders(po_id) ON DELETE CASCADE,
    product_id UUID NOT NULL REFERENCES pos.products(product_id),
    quantity INT NOT NULL CHECK (quantity > 0),
    unit_cost NUMERIC(12,2) NOT NULL DEFAULT 0 CHECK (unit_cost >= 0),
    line_total NUMERIC(12,2) NOT NULL DEFAULT 0 CHECK (line_total >= 0)
);

CREATE INDEX IF NOT EXISTS ix_pos_purchase_orders_branch ON pos.purchase_orders(branch_id, status);
