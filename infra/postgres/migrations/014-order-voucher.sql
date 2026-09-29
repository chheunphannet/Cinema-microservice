-- Migration 014: Add voucher_code to pos.orders
ALTER TABLE pos.orders ADD COLUMN IF NOT EXISTS voucher_code VARCHAR(50);
