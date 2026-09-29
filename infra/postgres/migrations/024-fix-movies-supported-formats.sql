-- =========================================================================
-- 024-fix-movies-supported-formats.sql
-- Fix missing supported_formats column that Dapper expects
-- =========================================================================

ALTER TABLE catalog.movies
    ADD COLUMN IF NOT EXISTS supported_formats TEXT[] DEFAULT ARRAY['STANDARD'];
