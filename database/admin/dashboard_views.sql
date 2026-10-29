-- Admin: one-row summary used by the dashboard cards.
CREATE OR REPLACE VIEW v_dashboard_stats AS
SELECT
    (SELECT COUNT(*) FROM books)                                   AS total_titles,
    (SELECT COALESCE(SUM(total_copies), 0) FROM books)             AS total_copies,
    (SELECT COUNT(*) FROM students)                                AS registered_students,
    (SELECT COALESCE(SUM(borrowed_copies), 0) FROM books)          AS books_borrowed,
    (SELECT COUNT(*) FROM book_requests WHERE status = 'Pending')  AS pending_requests;
