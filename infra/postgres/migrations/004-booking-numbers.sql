-- Phase 3 Migration: Persistent Sequential Booking Numbers & References
CREATE SEQUENCE IF NOT EXISTS pos.order_booking_number_seq START WITH 31600;

ALTER TABLE pos.orders 
    ADD COLUMN IF NOT EXISTS booking_number INT DEFAULT nextval('pos.order_booking_number_seq'),
    ADD COLUMN IF NOT EXISTS booking_reference VARCHAR(20) NULL;

CREATE INDEX IF NOT EXISTS ix_orders_booking_number ON pos.orders(booking_number);
CREATE INDEX IF NOT EXISTS ix_orders_booking_reference ON pos.orders(booking_reference);
