-- User: student accounts. student_id is also the login username.
CREATE TABLE IF NOT EXISTS students (
    student_id     VARCHAR(20)     NOT NULL,
    password_hash  VARCHAR(255)    NOT NULL,
    full_name      VARCHAR(140)    NOT NULL,
    email          VARCHAR(160)    NULL,
    program        VARCHAR(60)     NOT NULL DEFAULT '',
    year           VARCHAR(10)     NOT NULL DEFAULT '',
    section        VARCHAR(8)      NOT NULL DEFAULT '',
    contact        VARCHAR(20)     NOT NULL DEFAULT '',
    status         ENUM('active','suspended') NOT NULL DEFAULT 'active',
    is_sa          TINYINT(1)      NOT NULL DEFAULT 0,
    avatar_path    VARCHAR(260)    NOT NULL DEFAULT '',
    joined         DATE            NOT NULL,
    created_at     TIMESTAMP       NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (student_id),
    UNIQUE KEY uq_students_email (email)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
