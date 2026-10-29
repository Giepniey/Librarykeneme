-- User: the persisted book cart (maximum of 3 books is enforced in C#).
CREATE TABLE IF NOT EXISTS cart_items (
    student_id VARCHAR(20) NOT NULL,
    book_id    INT         NOT NULL,
    added_at   TIMESTAMP   NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (student_id, book_id),
    KEY idx_cart_book (book_id),
    CONSTRAINT fk_cart_student FOREIGN KEY (student_id) REFERENCES students (student_id) ON DELETE CASCADE ON UPDATE CASCADE,
    CONSTRAINT fk_cart_book    FOREIGN KEY (book_id)    REFERENCES books (id)            ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
