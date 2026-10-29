-- Shared: borrow/return requests, active loans and permanent history.
-- Books and students are referenced by id; names/titles are looked up with
-- JOINs so they are never duplicated here.

CREATE TABLE IF NOT EXISTS book_requests (
    id           VARCHAR(40)  NOT NULL,
    type         ENUM('Borrow','Return') NOT NULL,
    book_id      INT          NOT NULL,
    student_id   VARCHAR(20)  NOT NULL,
    request_date DATETIME     NOT NULL,
    status       ENUM('Pending','Approved','Rejected') NOT NULL DEFAULT 'Pending',
    created_at   TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (id),
    KEY idx_requests_status (status),
    KEY idx_requests_student (student_id),
    KEY idx_requests_book (book_id),
    CONSTRAINT fk_requests_book    FOREIGN KEY (book_id)    REFERENCES books (id)          ON DELETE RESTRICT ON UPDATE CASCADE,
    CONSTRAINT fk_requests_student FOREIGN KEY (student_id) REFERENCES students (student_id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS loans (
    id            INT          NOT NULL AUTO_INCREMENT,
    book_id       INT          NOT NULL,
    student_id    VARCHAR(20)  NOT NULL,
    borrowed_date DATETIME     NOT NULL,
    due_date      DATETIME     NOT NULL,
    status        ENUM('PENDING','RETURN','OVERDUE') NOT NULL DEFAULT 'PENDING',
    created_at    TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (id),
    KEY idx_loans_student_status (student_id, status),
    KEY idx_loans_book (book_id),
    CONSTRAINT fk_loans_book    FOREIGN KEY (book_id)    REFERENCES books (id)          ON DELETE RESTRICT ON UPDATE CASCADE,
    CONSTRAINT fk_loans_student FOREIGN KEY (student_id) REFERENCES students (student_id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS history (
    id            INT          NOT NULL AUTO_INCREMENT,
    book_id       INT          NOT NULL,
    student_id    VARCHAR(20)  NOT NULL,
    borrowed_date DATETIME     NOT NULL,
    returned_date DATETIME     NOT NULL,
    remark        VARCHAR(20)  NOT NULL DEFAULT 'On time',
    created_at    TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (id),
    KEY idx_history_student (student_id),
    KEY idx_history_book (book_id),
    CONSTRAINT fk_history_book    FOREIGN KEY (book_id)    REFERENCES books (id)          ON DELETE RESTRICT ON UPDATE CASCADE,
    CONSTRAINT fk_history_student FOREIGN KEY (student_id) REFERENCES students (student_id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
