-- Seed additional auditoriums for Branch 3 (Olympia) and Branch 4 (Midtown)
INSERT INTO catalog.auditoriums (auditorium_id, branch_id, name, capacity, hall_type, cleaning_buffer_minutes)
VALUES
    ('a3000000-0000-0000-0000-000000000001', 'b1000000-0000-0000-0000-000000000003', 'Hall 1 (ScreenX 270°)', 150, 'ScreenX', 15),
    ('a3000000-0000-0000-0000-000000000002', 'b1000000-0000-0000-0000-000000000003', 'Hall 2 (Dolby Atmos)', 120, 'Atmos', 15),
    ('a4000000-0000-0000-0000-000000000001', 'b1000000-0000-0000-0000-000000000004', 'Hall 1 (Standard 2D/3D)', 100, 'Standard', 15),
    ('a4000000-0000-0000-0000-000000000002', 'b1000000-0000-0000-0000-000000000004', 'Hall 2 (VIP Diamond Class)', 60, 'VIP', 20)
ON CONFLICT (auditorium_id) DO NOTHING;

-- Seed Seats for auditoriums
DO $$
DECLARE
    aud RECORD;
    r TEXT;
    s INT;
BEGIN
    FOR aud IN SELECT auditorium_id FROM catalog.auditoriums LOOP
        FOR r IN SELECT unnest(ARRAY['A','B','C','D','E','F']) LOOP
            FOR s IN 1..14 LOOP
                INSERT INTO catalog.seats (seat_id, auditorium_id, row_label, seat_number, seat_type, is_accessible, is_active)
                VALUES (
                    gen_random_uuid(),
                    aud.auditorium_id,
                    r,
                    s,
                    CASE WHEN r IN ('E','F') THEN 'vip' ELSE 'standard' END,
                    (r = 'A' AND s IN (1, 2)),
                    true
                )
                ON CONFLICT (auditorium_id, row_label, seat_number) DO NOTHING;
            END LOOP;
        END LOOP;
    END LOOP;
END $$;

-- Seed additional showtimes across Olympia and Midtown
INSERT INTO catalog.showtimes (showtime_id, movie_id, auditorium_id, starts_at, ends_at, base_price, status)
VALUES
    ('c1000000-0000-0000-0000-000000000001', '2a0c7d76-3c2d-48ac-8074-818f2293cb19', 'a3000000-0000-0000-0000-000000000002', NOW() + INTERVAL '2 hours', NOW() + INTERVAL '5 hours 3 mins', 9.00, 'scheduled'),
    ('c1000000-0000-0000-0000-000000000002', '821c426a-d6ab-40cf-8705-7f3548a5fb16', 'a3000000-0000-0000-0000-000000000001', NOW() + INTERVAL '3 hours', NOW() + INTERVAL '5 hours 23 mins', 10.00, 'scheduled'),
    ('c1000000-0000-0000-0000-000000000003', '02f2b6ea-2887-4c51-af00-084f66a249e0', 'a4000000-0000-0000-0000-000000000001', NOW() + INTERVAL '1 hour', NOW() + INTERVAL '4 hours 15 mins', 7.50, 'scheduled'),
    ('c1000000-0000-0000-0000-000000000004', 'c8f49c0a-27b7-4092-b476-3634303ced84', 'a4000000-0000-0000-0000-000000000002', NOW() + INTERVAL '4 hours', NOW() + INTERVAL '7 hours 15 mins', 12.00, 'scheduled')
ON CONFLICT (showtime_id) DO NOTHING;

-- Seed F&B Products in pos.products for each of the 4 branches
DO $$
DECLARE
    br RECORD;
    p_car_l UUID;
    p_swt_m UUID;
    p_chs_l UUID;
    p_cok_l UUID;
    p_ckz_l UUID;
    p_spr_l UUID;
    p_wtr   UUID;
    p_cpl   UUID;
    p_hdog  UUID;
    p_nch   UUID;
BEGIN
    FOR br IN SELECT branch_id FROM catalog.branches LOOP
        -- Caramel Popcorn
        INSERT INTO pos.products (branch_id, sku, name, category, unit_price, is_active, description, stock_quantity)
        VALUES (br.branch_id, 'POP-CAR-L', 'Caramel Popcorn (Large)', 'Popcorn', 4.50, true, 'Freshly popped golden corn coated with rich butter caramel', 150)
        ON CONFLICT (branch_id, sku) DO UPDATE SET unit_price = EXCLUDED.unit_price
        RETURNING product_id INTO p_car_l;

        -- Sweet Popcorn
        INSERT INTO pos.products (branch_id, sku, name, category, unit_price, is_active, description, stock_quantity)
        VALUES (br.branch_id, 'POP-SWT-M', 'Sweet Popcorn (Medium)', 'Popcorn', 3.50, true, 'Classic sweet crunchy cinema popcorn', 120)
        ON CONFLICT (branch_id, sku) DO UPDATE SET unit_price = EXCLUDED.unit_price
        RETURNING product_id INTO p_swt_m;

        -- Cheese Popcorn
        INSERT INTO pos.products (branch_id, sku, name, category, unit_price, is_active, description, stock_quantity)
        VALUES (br.branch_id, 'POP-CHS-L', 'Cheese Popcorn (Large)', 'Popcorn', 4.50, true, 'Savory cheddar cheese coated warm popcorn', 90)
        ON CONFLICT (branch_id, sku) DO UPDATE SET unit_price = EXCLUDED.unit_price
        RETURNING product_id INTO p_chs_l;

        -- Coca-Cola
        INSERT INTO pos.products (branch_id, sku, name, category, unit_price, is_active, description, stock_quantity)
        VALUES (br.branch_id, 'BEV-COK-L', 'Coca-Cola 32oz', 'Beverages', 2.50, true, 'Chilled refreshing fountain Coca-Cola', 200)
        ON CONFLICT (branch_id, sku) DO UPDATE SET unit_price = EXCLUDED.unit_price
        RETURNING product_id INTO p_cok_l;

        -- Coca-Cola Zero
        INSERT INTO pos.products (branch_id, sku, name, category, unit_price, is_active, description, stock_quantity)
        VALUES (br.branch_id, 'BEV-CKZ-L', 'Coca-Cola Zero Sugar 32oz', 'Beverages', 2.50, true, 'Zero sugar refreshing ice cold Coca-Cola', 180)
        ON CONFLICT (branch_id, sku) DO UPDATE SET unit_price = EXCLUDED.unit_price
        RETURNING product_id INTO p_ckz_l;

        -- Sprite
        INSERT INTO pos.products (branch_id, sku, name, category, unit_price, is_active, description, stock_quantity)
        VALUES (br.branch_id, 'BEV-SPR-L', 'Sprite 32oz', 'Beverages', 2.50, true, 'Crisp lemon-lime fountain soda', 160)
        ON CONFLICT (branch_id, sku) DO UPDATE SET unit_price = EXCLUDED.unit_price
        RETURNING product_id INTO p_spr_l;

        -- Mineral Water
        INSERT INTO pos.products (branch_id, sku, name, category, unit_price, is_active, description, stock_quantity)
        VALUES (br.branch_id, 'BEV-WTR-500', 'Dasani Mineral Water 500ml', 'Beverages', 1.20, true, 'Pure bottled mineral water', 15)
        ON CONFLICT (branch_id, sku) DO UPDATE SET unit_price = EXCLUDED.unit_price
        RETURNING product_id INTO p_wtr;

        -- Couple Combo
        INSERT INTO pos.products (branch_id, sku, name, category, unit_price, is_active, description, stock_quantity)
        VALUES (br.branch_id, 'CMB-CPL', 'Legend Couple Combo', 'Combos', 7.50, true, '1 Large Popcorn + 2 Large Fountain Drinks', 80)
        ON CONFLICT (branch_id, sku) DO UPDATE SET unit_price = EXCLUDED.unit_price
        RETURNING product_id INTO p_cpl;

        -- Hotdog Combo
        INSERT INTO pos.products (branch_id, sku, name, category, unit_price, is_active, description, stock_quantity)
        VALUES (br.branch_id, 'CMB-HDOG', 'Jumbo Hotdog Combo', 'Combos', 6.50, true, '1 Grilled Jumbo Hotdog + 1 Medium Popcorn + 1 Drink', 50)
        ON CONFLICT (branch_id, sku) DO UPDATE SET unit_price = EXCLUDED.unit_price
        RETURNING product_id INTO p_hdog;

        -- Nachos
        INSERT INTO pos.products (branch_id, sku, name, category, unit_price, is_active, description, stock_quantity)
        VALUES (br.branch_id, 'SNK-NCH', 'Warm Cheese Nachos', 'Snacks', 4.00, true, 'Tortilla chips served with hot cheddar cheese dip and jalapeños', 18)
        ON CONFLICT (branch_id, sku) DO UPDATE SET unit_price = EXCLUDED.unit_price
        RETURNING product_id INTO p_nch;

        -- Insert into pos.branch_inventory for each product
        INSERT INTO pos.branch_inventory (branch_id, product_id, stock_quantity, reorder_threshold, is_out_of_stock)
        VALUES
            (br.branch_id, p_car_l, 145, 25, false),
            (br.branch_id, p_swt_m, 110, 20, false),
            (br.branch_id, p_chs_l, 88, 20, false),
            (br.branch_id, p_cok_l, 200, 30, false),
            (br.branch_id, p_ckz_l, 175, 30, false),
            (br.branch_id, p_spr_l, 160, 30, false),
            (br.branch_id, p_wtr, 15, 20, false),
            (br.branch_id, p_cpl, 80, 15, false),
            (br.branch_id, p_hdog, 48, 10, false),
            (br.branch_id, p_nch, 18, 20, false)
        ON CONFLICT (branch_id, product_id) DO UPDATE 
        SET stock_quantity = EXCLUDED.stock_quantity, 
            reorder_threshold = EXCLUDED.reorder_threshold, 
            updated_at = NOW();
    END LOOP;
END $$;

-- Seed Sample Reservations & Bookings
DO $$
DECLARE
    st1 UUID; aud1 UUID; br1 UUID;
    st2 UUID; aud2 UUID; br2 UUID;
    st3 UUID; aud3 UUID; br3 UUID;
    st4 UUID; aud4 UUID; br4 UUID;
    
    seat1 UUID; seat2 UUID;
    seat3 UUID; seat4 UUID;
    seat5 UUID; seat6 UUID; seat7 UUID;

    res1 UUID := 'd1000000-0000-0000-0000-000000000001';
    res2 UUID := 'd1000000-0000-0000-0000-000000000002';
    res3 UUID := 'd1000000-0000-0000-0000-000000000003';
    res4 UUID := 'd1000000-0000-0000-0000-000000000004';
    
    ord1 UUID := 'e1000000-0000-0000-0000-000000000001';
    ord2 UUID := 'e1000000-0000-0000-0000-000000000002';
    ord3 UUID := 'e1000000-0000-0000-0000-000000000003';
    ord4 UUID := 'e1000000-0000-0000-0000-000000000004';
BEGIN
    -- Dynamically select 4 valid showtimes and their auditoriums
    SELECT s.showtime_id, s.auditorium_id, a.branch_id INTO st1, aud1, br1 
    FROM catalog.showtimes s JOIN catalog.auditoriums a ON s.auditorium_id = a.auditorium_id ORDER BY s.starts_at LIMIT 1 OFFSET 0;
    
    SELECT s.showtime_id, s.auditorium_id, a.branch_id INTO st2, aud2, br2 
    FROM catalog.showtimes s JOIN catalog.auditoriums a ON s.auditorium_id = a.auditorium_id ORDER BY s.starts_at LIMIT 1 OFFSET 1;
    
    SELECT s.showtime_id, s.auditorium_id, a.branch_id INTO st3, aud3, br3 
    FROM catalog.showtimes s JOIN catalog.auditoriums a ON s.auditorium_id = a.auditorium_id ORDER BY s.starts_at LIMIT 1 OFFSET 2;
    
    SELECT s.showtime_id, s.auditorium_id, a.branch_id INTO st4, aud4, br4 
    FROM catalog.showtimes s JOIN catalog.auditoriums a ON s.auditorium_id = a.auditorium_id ORDER BY s.starts_at LIMIT 1 OFFSET 3;

    -- Pick available seats
    SELECT seat_id INTO seat1 FROM catalog.seats WHERE auditorium_id = aud1 AND row_label = 'D' AND seat_number = 5 LIMIT 1;
    SELECT seat_id INTO seat2 FROM catalog.seats WHERE auditorium_id = aud1 AND row_label = 'D' AND seat_number = 6 LIMIT 1;
    
    SELECT seat_id INTO seat3 FROM catalog.seats WHERE auditorium_id = aud2 AND row_label = 'E' AND seat_number = 7 LIMIT 1;
    SELECT seat_id INTO seat4 FROM catalog.seats WHERE auditorium_id = aud2 AND row_label = 'E' AND seat_number = 8 LIMIT 1;
    
    SELECT seat_id INTO seat5 FROM catalog.seats WHERE auditorium_id = aud3 AND row_label = 'C' AND seat_number = 4 LIMIT 1;
    SELECT seat_id INTO seat6 FROM catalog.seats WHERE auditorium_id = aud4 AND row_label = 'B' AND seat_number = 9 LIMIT 1;
    SELECT seat_id INTO seat7 FROM catalog.seats WHERE auditorium_id = aud4 AND row_label = 'B' AND seat_number = 10 LIMIT 1;

    -- Reservation 1
    INSERT INTO reservations.reservations (reservation_id, showtime_id, status, idempotency_key, guest_name, guest_email, guest_phone, is_guest, confirmed_at)
    VALUES (res1, st1, 'confirmed', gen_random_uuid(), 'Sokha Chan', 'sokha.chan@gmail.com', '+855 12 345 678', true, NOW())
    ON CONFLICT (reservation_id) DO NOTHING;

    IF seat1 IS NOT NULL THEN
        INSERT INTO reservations.confirmed_seats (showtime_id, seat_id, reservation_id) VALUES (st1, seat1, res1) ON CONFLICT DO NOTHING;
    END IF;
    IF seat2 IS NOT NULL THEN
        INSERT INTO reservations.confirmed_seats (showtime_id, seat_id, reservation_id) VALUES (st1, seat2, res1) ON CONFLICT DO NOTHING;
    END IF;

    INSERT INTO pos.orders (order_id, branch_id, reservation_id, status, subtotal, discount_amount, total_amount, idempotency_key, channel, customer_email, customer_phone, booking_reference)
    VALUES (ord1, br1, res1, 'paid', 17.00, 0, 17.00, gen_random_uuid(), 'web', 'sokha.chan@gmail.com', '+855 12 345 678', 'LEG-BKG-8801')
    ON CONFLICT (order_id) DO NOTHING;

    INSERT INTO pos.payments (order_id, method, amount, status)
    VALUES (ord1, 'card', 17.00, 'captured')
    ON CONFLICT DO NOTHING;

    IF seat1 IS NOT NULL THEN
        INSERT INTO tickets.tickets (reservation_id, showtime_id, seat_id, qr_token_hash, status, delivery_channel, email_recipient)
        VALUES (res1, st1, seat1, encode(sha256('TICKET-1-LEG'::bytea), 'hex'), 'active', 'email', 'sokha.chan@gmail.com')
        ON CONFLICT DO NOTHING;
    END IF;
    IF seat2 IS NOT NULL THEN
        INSERT INTO tickets.tickets (reservation_id, showtime_id, seat_id, qr_token_hash, status, delivery_channel, email_recipient)
        VALUES (res1, st1, seat2, encode(sha256('TICKET-2-LEG'::bytea), 'hex'), 'active', 'email', 'sokha.chan@gmail.com')
        ON CONFLICT DO NOTHING;
    END IF;

    -- Reservation 2
    INSERT INTO reservations.reservations (reservation_id, showtime_id, status, idempotency_key, guest_name, guest_email, guest_phone, is_guest, confirmed_at)
    VALUES (res2, st2, 'confirmed', gen_random_uuid(), 'Dara Sam', 'dara.sam@outlook.com', '+855 98 765 432', true, NOW())
    ON CONFLICT (reservation_id) DO NOTHING;

    IF seat3 IS NOT NULL THEN
        INSERT INTO reservations.confirmed_seats (showtime_id, seat_id, reservation_id) VALUES (st2, seat3, res2) ON CONFLICT DO NOTHING;
    END IF;
    IF seat4 IS NOT NULL THEN
        INSERT INTO reservations.confirmed_seats (showtime_id, seat_id, reservation_id) VALUES (st2, seat4, res2) ON CONFLICT DO NOTHING;
    END IF;

    INSERT INTO pos.orders (order_id, branch_id, reservation_id, status, subtotal, discount_amount, total_amount, idempotency_key, channel, customer_email, customer_phone, booking_reference)
    VALUES (ord2, br2, res2, 'paid', 17.00, 0, 17.00, gen_random_uuid(), 'web', 'dara.sam@outlook.com', '+855 98 765 432', 'LEG-BKG-8802')
    ON CONFLICT (order_id) DO NOTHING;

    INSERT INTO pos.payments (order_id, method, amount, status)
    VALUES (ord2, 'qr', 17.00, 'captured')
    ON CONFLICT DO NOTHING;

    IF seat3 IS NOT NULL THEN
        INSERT INTO tickets.tickets (reservation_id, showtime_id, seat_id, qr_token_hash, status, delivery_channel, email_recipient)
        VALUES (res2, st2, seat3, encode(sha256('TICKET-3-LEG'::bytea), 'hex'), 'active', 'email', 'dara.sam@outlook.com')
        ON CONFLICT DO NOTHING;
    END IF;
    IF seat4 IS NOT NULL THEN
        INSERT INTO tickets.tickets (reservation_id, showtime_id, seat_id, qr_token_hash, status, delivery_channel, email_recipient)
        VALUES (res2, st2, seat4, encode(sha256('TICKET-4-LEG'::bytea), 'hex'), 'active', 'email', 'dara.sam@outlook.com')
        ON CONFLICT DO NOTHING;
    END IF;

    -- Reservation 3
    INSERT INTO reservations.reservations (reservation_id, showtime_id, status, idempotency_key, guest_name, guest_email, guest_phone, is_guest, confirmed_at)
    VALUES (res3, st3, 'confirmed', gen_random_uuid(), 'Bopha Pich', 'bopha.p@gmail.com', '+855 10 234 567', true, NOW())
    ON CONFLICT (reservation_id) DO NOTHING;

    IF seat5 IS NOT NULL THEN
        INSERT INTO reservations.confirmed_seats (showtime_id, seat_id, reservation_id) VALUES (st3, seat5, res3) ON CONFLICT DO NOTHING;
    END IF;

    INSERT INTO pos.orders (order_id, branch_id, reservation_id, status, subtotal, discount_amount, total_amount, idempotency_key, channel, customer_email, customer_phone, booking_reference)
    VALUES (ord3, br3, res3, 'paid', 8.50, 0, 8.50, gen_random_uuid(), 'web', 'bopha.p@gmail.com', '+855 10 234 567', 'LEG-BKG-8803')
    ON CONFLICT (order_id) DO NOTHING;

    INSERT INTO pos.payments (order_id, method, amount, status)
    VALUES (ord3, 'cash', 8.50, 'captured')
    ON CONFLICT DO NOTHING;

    IF seat5 IS NOT NULL THEN
        INSERT INTO tickets.tickets (reservation_id, showtime_id, seat_id, qr_token_hash, status, delivery_channel, email_recipient)
        VALUES (res3, st3, seat5, encode(sha256('TICKET-5-LEG'::bytea), 'hex'), 'active', 'print', 'bopha.p@gmail.com')
        ON CONFLICT DO NOTHING;
    END IF;

    -- Reservation 4
    INSERT INTO reservations.reservations (reservation_id, showtime_id, status, idempotency_key, guest_name, guest_email, guest_phone, is_guest, confirmed_at)
    VALUES (res4, st4, 'confirmed', gen_random_uuid(), 'Vireak Chea', 'vireak.chea@gmail.com', '+855 77 889 900', true, NOW())
    ON CONFLICT (reservation_id) DO NOTHING;

    IF seat6 IS NOT NULL THEN
        INSERT INTO reservations.confirmed_seats (showtime_id, seat_id, reservation_id) VALUES (st4, seat6, res4) ON CONFLICT DO NOTHING;
    END IF;
    IF seat7 IS NOT NULL THEN
        INSERT INTO reservations.confirmed_seats (showtime_id, seat_id, reservation_id) VALUES (st4, seat7, res4) ON CONFLICT DO NOTHING;
    END IF;

    INSERT INTO pos.orders (order_id, branch_id, reservation_id, status, subtotal, discount_amount, total_amount, idempotency_key, channel, customer_email, customer_phone, booking_reference)
    VALUES (ord4, br4, res4, 'paid', 18.00, 0, 18.00, gen_random_uuid(), 'web', 'vireak.chea@gmail.com', '+855 77 889 900', 'LEG-BKG-8804')
    ON CONFLICT (order_id) DO NOTHING;

    INSERT INTO pos.payments (order_id, method, amount, status)
    VALUES (ord4, 'card', 18.00, 'captured')
    ON CONFLICT DO NOTHING;

    IF seat6 IS NOT NULL THEN
        INSERT INTO tickets.tickets (reservation_id, showtime_id, seat_id, qr_token_hash, status, delivery_channel, email_recipient)
        VALUES (res4, st4, seat6, encode(sha256('TICKET-6-LEG'::bytea), 'hex'), 'active', 'email', 'vireak.chea@gmail.com')
        ON CONFLICT DO NOTHING;
    END IF;
    IF seat7 IS NOT NULL THEN
        INSERT INTO tickets.tickets (reservation_id, showtime_id, seat_id, qr_token_hash, status, delivery_channel, email_recipient)
        VALUES (res4, st4, seat7, encode(sha256('TICKET-7-LEG'::bytea), 'hex'), 'active', 'email', 'vireak.chea@gmail.com')
        ON CONFLICT DO NOTHING;
    END IF;
END $$;
