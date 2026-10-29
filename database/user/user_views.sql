-- User: browse catalogue with computed availability.
CREATE OR REPLACE VIEW v_browse_books AS
SELECT
    b.title,
    b.author,
    b.isbn,
    b.category,
    (b.total_copies - b.borrowed_copies) AS available_copies,
    CASE
        WHEN b.total_copies - b.borrowed_copies > 0 THEN 'Available'
        ELSE 'Borrowed'
    END AS availability
FROM books b;

-- User: borrowing history joined to book details.
CREATE OR REPLACE VIEW v_user_history AS
SELECT
    h.student_id,
    b.title,
    b.author,
    b.isbn,
    h.borrowed_date,
    h.returned_date,
    h.remark
FROM history h
JOIN books b ON h.book_id = b.id
ORDER BY h.borrowed_date DESC, h.id DESC;
