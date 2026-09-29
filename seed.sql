-- Insert 1000 orders
INSERT INTO pos.orders (order_id, branch_id, idempotency_key)
SELECT 
    gen_random_uuid(), 
    '11111111-1111-1111-1111-111111111111', 
    gen_random_uuid()
FROM generate_series(1, 1000);

-- Insert 50 order lines per order (50,000 total)
INSERT INTO pos.order_lines (order_id, description, quantity, unit_price, line_total)
SELECT 
    order_id,
    'Test Item',
    1,
    10.00,
    10.00
FROM pos.orders
CROSS JOIN generate_series(1, 50);
