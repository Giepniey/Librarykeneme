-- ============================================================
-- Library Inventory System - master installer
-- Run from the repository root:
--     mysql -u root < database/00_install.sql
-- Safe to run more than once (all objects use IF NOT EXISTS or
-- CREATE OR REPLACE).
-- ============================================================

CREATE DATABASE IF NOT EXISTS LIS
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_unicode_ci;

USE LIS;

-- Shared tables first: other tables reference books.
SOURCE database/shared/books.sql;

-- User accounts.
SOURCE database/user/students.sql;

-- Requests, loans and history (needs books + students).
SOURCE database/shared/borrowing.sql;

-- Administrator accounts.
SOURCE database/admin/admins.sql;

-- Per-student book cart (needs students + books).
SOURCE database/user/book_cart.sql;

-- Views used by the screens.
SOURCE database/admin/dashboard_views.sql;
SOURCE database/user/user_views.sql;
