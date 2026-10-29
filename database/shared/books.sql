-- Shared: the catalogue of books.
CREATE TABLE IF NOT EXISTS books (
    id              INT             NOT NULL AUTO_INCREMENT,
    isbn            VARCHAR(40)     NOT NULL,
    title           VARCHAR(200)    NOT NULL,
    author          VARCHAR(160)    NOT NULL,
    category        ENUM('CIR','FIC','FIL','REF') NOT NULL DEFAULT 'FIC',
    total_copies    INT             NOT NULL DEFAULT 1,
    borrowed_copies INT             NOT NULL DEFAULT 0,
    cover_path      VARCHAR(260)    NOT NULL DEFAULT '',
    cover_color     VARCHAR(20)     NOT NULL DEFAULT '',
    created_at      TIMESTAMP       NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (id),
    UNIQUE KEY uq_books_isbn (isbn),
    KEY idx_books_title (title),
    CONSTRAINT chk_books_copies CHECK (borrowed_copies >= 0 AND borrowed_copies <= total_copies)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
