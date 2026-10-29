using System.Drawing;
using System.Text.Json;
using MySqlConnector;

namespace LibraryInventory;

/// <summary>
/// MySQL access layer. The schema itself lives in <c>database/</c> and is
/// installed with <c>database/00_install.sql</c>; nothing here creates tables.
/// </summary>
public static class Database
{
    static readonly string DataRoot;
    static readonly string AvatarDir;
    public static readonly string PhotosDir;
    public static readonly string CoversDir;

    static readonly string[] RequiredTables =
    {
        "books", "students", "book_requests", "loans", "history", "admins", "cart_items"
    };

    static Database()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var dir = new DirectoryInfo(baseDir);
        while (dir != null && !dir.GetFiles("*.csproj").Any())
            dir = dir.Parent;
        var projectParent = dir?.Parent?.FullName ?? baseDir;
        DataRoot = Path.Combine(projectParent, "data");
        AvatarDir = Path.Combine(DataRoot, "avatars");
        PhotosDir = Path.Combine(DataRoot, "photos");
        CoversDir = Path.Combine(DataRoot, "covers");
    }

    /// <summary>
    /// Opens the database, prepares the data folders, verifies that
    /// <c>database/00_install.sql</c> has been run and seeds/migrates accounts.
    /// Deliberately NOT done in a static constructor: failures must be readable.
    /// </summary>
    public static void Initialize()
    {
        Directory.CreateDirectory(DataRoot);
        Directory.CreateDirectory(AvatarDir);
        Directory.CreateDirectory(PhotosDir);
        Directory.CreateDirectory(CoversDir);

        try
        {
            EnsureSchemaPresent();
            EnsureDefaultAdmin();
            MigrateLegacyPasswords();
        }
        catch (Exception e)
        {
            if (e is InvalidOperationException && e.Message.Contains("00_install.sql"))
                throw;

            throw new InvalidOperationException(
                "The library database is not ready. Run database/00_install.sql " +
                "(from the repository root) to create the LIS database and tables. " +
                "Details: " + e.Message, e);
        }
    }

    /// <summary>Fails with a clear message when the installed schema is incomplete.</summary>
    static void EnsureSchemaPresent()
    {
        var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = Db.Instance.Connection.CreateCommand())
        {
            cmd.CommandText = @"SELECT table_name FROM information_schema.tables
                                WHERE table_schema = DATABASE()";
            using var rd = cmd.ExecuteReader();
            while (rd.Read()) present.Add(rd.GetString(0));
        }

        var missing = RequiredTables.Where(t => !present.Contains(t)).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException(
                "Run database/00_install.sql to create the missing tables: " + string.Join(", ", missing));
    }

    // ================= Passwords =================

    public static string HashPassword(string plain) =>
        BCrypt.Net.BCrypt.HashPassword(plain ?? "");

    static bool IsBcryptHash(string hash) =>
        !string.IsNullOrEmpty(hash) && (hash.StartsWith("$2a$") || hash.StartsWith("$2b$") || hash.StartsWith("$2y$"));

    /// <summary>Verifies a BCrypt hash, or a legacy plain-text value.</summary>
    static bool VerifyPassword(string plain, string stored)
    {
        if (string.IsNullOrEmpty(stored)) return false;
        if (IsBcryptHash(stored))
        {
            try { return BCrypt.Net.BCrypt.Verify(plain ?? "", stored); }
            catch { return false; }
        }
        return string.Equals(stored, plain ?? "", StringComparison.Ordinal);
    }

    /// <summary>Verifies and upgrades a legacy plain-text password to BCrypt.</summary>
    static bool VerifyAndUpgrade(string table, string keyColumn, object keyValue, string plain, string stored)
    {
        if (!VerifyPassword(plain, stored)) return false;
        if (!IsBcryptHash(stored))
        {
            using var fix = Db.Instance.Connection.CreateCommand();
            fix.CommandText = $"UPDATE `{table}` SET password_hash = @hash WHERE `{keyColumn}` = @key";
            fix.Parameters.Add(new MySqlParameter("@hash", HashPassword(plain)));
            fix.Parameters.Add(new MySqlParameter("@key", keyValue));
            fix.ExecuteNonQuery();
        }
        return true;
    }

    /// <summary>Hashes any plain-text password rows left by older versions (runs once at startup).</summary>
    static void MigrateLegacyPasswords()
    {
        MigratePasswordColumn("admins", "id");
        MigratePasswordColumn("students", "student_id");
    }

    static void MigratePasswordColumn(string table, string keyColumn)
    {
        var rows = new List<(object Key, string Hash)>();
        using (var cmd = Db.Instance.Connection.CreateCommand())
        {
            cmd.CommandText = $"SELECT `{keyColumn}`, password_hash FROM `{table}`";
            using var rd = cmd.ExecuteReader();
            while (rd.Read()) rows.Add((rd.GetValue(0), rd.IsDBNull(1) ? "" : rd.GetString(1)));
        }

        foreach (var (key, hash) in rows)
        {
            if (IsBcryptHash(hash) || string.IsNullOrEmpty(hash)) continue;
            using var fix = Db.Instance.Connection.CreateCommand();
            fix.CommandText = $"UPDATE `{table}` SET password_hash = @hash WHERE `{keyColumn}` = @key";
            fix.Parameters.Add(new MySqlParameter("@hash", HashPassword(hash)));
            fix.Parameters.Add(new MySqlParameter("@key", key));
            fix.ExecuteNonQuery();
        }
    }

    // ================= Students =================

    public static List<Student> LoadStudents()
    {
        var list = new List<Student>();
        using var cmd = Db.Instance.Connection.CreateCommand();
        cmd.CommandText = @"SELECT student_id, full_name, email, program, year, section, contact,
                                   status, joined, avatar_path, is_sa
                            FROM students";
        using var rd = cmd.ExecuteReader();
        while (rd.Read())
        {
            list.Add(new Student
            {
                Id = rd.GetString(0),
                Name = rd.GetString(1),
                Email = rd.IsDBNull(2) ? "" : rd.GetString(2),
                Program = rd.GetString(3),
                Year = rd.GetString(4),
                Section = rd.GetString(5),
                Contact = rd.GetString(6),
                Active = string.Equals(rd.GetString(7), "active", StringComparison.OrdinalIgnoreCase),
                Joined = rd.GetDateTime(8),
                AvatarPath = rd.GetString(9),
                IsSa = rd.GetByte(10) != 0
            });
        }
        return list;
    }

    public static bool StudentExists(string studentId)
    {
        using var cmd = Db.Instance.Connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM students WHERE student_id = @id";
        cmd.Parameters.Add(new MySqlParameter("@id", studentId ?? ""));
        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0) > 0;
    }

    /// <summary>
    /// Inserts or updates one student. A non-empty <see cref="Student.Password"/>
    /// is hashed; an empty one keeps the stored hash (so admin edits do not wipe it).
    /// </summary>
    public static void SaveStudent(Student student, string previousId = null)
    {
        if (!string.IsNullOrEmpty(previousId) && previousId != student.Id)
        {
            // Rename in place; the foreign keys use ON UPDATE CASCADE so loans,
            // requests and history follow the new student id.
            using var rename = Db.Instance.Connection.CreateCommand();
            rename.CommandText = "UPDATE students SET student_id = @new WHERE student_id = @old";
            rename.Parameters.Add(new MySqlParameter("@new", student.Id));
            rename.Parameters.Add(new MySqlParameter("@old", previousId));
            rename.ExecuteNonQuery();
        }

        string existingHash = null;
        using (var sel = Db.Instance.Connection.CreateCommand())
        {
            sel.CommandText = "SELECT password_hash FROM students WHERE student_id = @id";
            sel.Parameters.Add(new MySqlParameter("@id", student.Id));
            var value = sel.ExecuteScalar();
            if (value != null && value != DBNull.Value) existingHash = Convert.ToString(value);
        }

        string hash = !string.IsNullOrEmpty(student.Password)
            ? HashPassword(student.Password)
            : (!string.IsNullOrEmpty(existingHash) ? existingHash : HashPassword("password123"));

        using var cmd = Db.Instance.Connection.CreateCommand();
        cmd.CommandText = @"INSERT INTO students
                                (student_id, password_hash, full_name, email, program, year, section,
                                 contact, status, is_sa, avatar_path, joined)
                            VALUES
                                (@id, @hash, @name, @email, @program, @year, @section,
                                 @contact, @status, @issa, @avatar, @joined)
                            ON DUPLICATE KEY UPDATE
                                password_hash = VALUES(password_hash), full_name = VALUES(full_name),
                                email = VALUES(email), program = VALUES(program), year = VALUES(year),
                                section = VALUES(section), contact = VALUES(contact),
                                status = VALUES(status), is_sa = VALUES(is_sa),
                                avatar_path = VALUES(avatar_path), joined = VALUES(joined)";
        cmd.Parameters.Add(new MySqlParameter("@id", student.Id));
        cmd.Parameters.Add(new MySqlParameter("@hash", hash));
        cmd.Parameters.Add(new MySqlParameter("@name", student.Name ?? ""));
        cmd.Parameters.Add(new MySqlParameter("@email",
            string.IsNullOrWhiteSpace(student.Email) ? (object)DBNull.Value : student.Email.Trim()));
        cmd.Parameters.Add(new MySqlParameter("@program", student.Program ?? ""));
        cmd.Parameters.Add(new MySqlParameter("@year", student.Year ?? ""));
        cmd.Parameters.Add(new MySqlParameter("@section", student.Section ?? ""));
        cmd.Parameters.Add(new MySqlParameter("@contact", student.Contact ?? ""));
        cmd.Parameters.Add(new MySqlParameter("@status", student.Active ? "active" : "suspended"));
        cmd.Parameters.Add(new MySqlParameter("@issa", student.IsSa ? 1 : 0));
        cmd.Parameters.Add(new MySqlParameter("@avatar", student.AvatarPath ?? ""));
        cmd.Parameters.Add(new MySqlParameter("@joined", student.Joined));
        cmd.ExecuteNonQuery();
    }

    public static void DeleteStudent(Student student)
    {
        using var cmd = Db.Instance.Connection.CreateCommand();
        cmd.CommandText = "DELETE FROM students WHERE student_id = @id";
        cmd.Parameters.Add(new MySqlParameter("@id", student.Id));
        cmd.ExecuteNonQuery();
    }

    public static string SaveAvatar(string studentId, Image image)
    {
        var fileName = $"{studentId}.png";
        var filePath = Path.Combine(AvatarDir, fileName);
        image.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
        using var cmd = Db.Instance.Connection.CreateCommand();
        cmd.CommandText = "UPDATE students SET avatar_path = @p WHERE student_id = @id";
        cmd.Parameters.Add(new MySqlParameter("@p", filePath));
        cmd.Parameters.Add(new MySqlParameter("@id", studentId));
        cmd.ExecuteNonQuery();
        return filePath;
    }

    // ================= Requests =================

    public static List<RequestDbRecord> LoadRequests()
    {
        var list = new List<RequestDbRecord>();
        using var cmd = Db.Instance.Connection.CreateCommand();
        cmd.CommandText = @"SELECT r.id, r.type, r.student_id, b.isbn, r.request_date, r.status
                            FROM book_requests r
                            JOIN books b ON r.book_id = b.id
                            ORDER BY r.request_date, r.id";
        using var rd = cmd.ExecuteReader();
        while (rd.Read())
        {
            list.Add(new RequestDbRecord
            {
                Id = rd.GetString(0),
                Type = rd.GetString(1),
                StudentId = rd.GetString(2),
                BookIsbn = rd.GetString(3),
                Date = rd.GetDateTime(4),
                Status = rd.GetString(5)
            });
        }
        return list;
    }

    /// <summary>Upserts requests; rows are never deleted because they are an audit trail.</summary>
    public static void SaveRequests(IEnumerable<RequestDbRecord> records)
    {
        foreach (var r in records)
        {
            var bookId = GetBookId(r.BookIsbn);
            if (bookId == null) continue;

            using var cmd = Db.Instance.Connection.CreateCommand();
            cmd.CommandText = @"INSERT INTO book_requests
                                    (id, type, book_id, student_id, request_date, status)
                                VALUES
                                    (@id, @type, @bookId, @studentId, @date, @status)
                                ON DUPLICATE KEY UPDATE
                                    type = VALUES(type), book_id = VALUES(book_id),
                                    student_id = VALUES(student_id), request_date = VALUES(request_date),
                                    status = VALUES(status)";
            cmd.Parameters.Add(new MySqlParameter("@id", r.Id));
            cmd.Parameters.Add(new MySqlParameter("@type", r.Type));
            cmd.Parameters.Add(new MySqlParameter("@bookId", bookId.Value));
            cmd.Parameters.Add(new MySqlParameter("@studentId", r.StudentId));
            cmd.Parameters.Add(new MySqlParameter("@date", r.Date));
            cmd.Parameters.Add(new MySqlParameter("@status", r.Status));
            cmd.ExecuteNonQuery();
        }
    }

    // ================= Covers / images =================

    public static string SaveCover(Book book, Image image)
    {
        var safeIsbn = string.Concat((string.IsNullOrWhiteSpace(book.Isbn) ? "book" : book.Isbn).Split(Path.GetInvalidFileNameChars()));
        var fileName = $"cover_{safeIsbn}_{DateTime.Now:yyyyMMddHHmmssfff}.png";
        var filePath = Path.Combine(CoversDir, fileName);

        var oldPath = book.CoverPath;
        if (!string.IsNullOrEmpty(oldPath) && File.Exists(oldPath))
        {
            try { File.Delete(oldPath); } catch { }
        }

        image.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);

        using var cmd = Db.Instance.Connection.CreateCommand();
        cmd.CommandText = "UPDATE books SET cover_path = @p WHERE isbn = @isbn";
        cmd.Parameters.Add(new MySqlParameter("@p", filePath));
        cmd.Parameters.Add(new MySqlParameter("@isbn", book.Isbn));
        cmd.ExecuteNonQuery();

        return filePath;
    }

    public static void DeleteCover(Book book)
    {
        var oldPath = book.CoverPath;
        if (!string.IsNullOrEmpty(oldPath) && File.Exists(oldPath))
        {
            try { File.Delete(oldPath); } catch { }
        }
        using var cmd = Db.Instance.Connection.CreateCommand();
        cmd.CommandText = "UPDATE books SET cover_path = '' WHERE isbn = @isbn";
        cmd.Parameters.Add(new MySqlParameter("@isbn", book.Isbn));
        cmd.ExecuteNonQuery();
    }

    public static Image LoadAvatar(string avatarPath) => LoadImage(avatarPath);

    public static Image LoadImage(string imagePath)
    {
        if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
            return null;

        try
        {
            var bytes = File.ReadAllBytes(imagePath);
            using var ms = new MemoryStream(bytes);
            return Image.FromStream(ms);
        }
        catch
        {
            return null;
        }
    }

    // ================= Books =================

    public static List<Book> LoadBooks()
    {
        var list = new List<Book>();
        using var cmd = Db.Instance.Connection.CreateCommand();
        cmd.CommandText = @"SELECT id, isbn, title, author, category, total_copies,
                                   borrowed_copies, cover_path, cover_color
                            FROM books";
        using var rd = cmd.ExecuteReader();
        while (rd.Read())
        {
            list.Add(new Book
            {
                Id = rd.GetInt32(0),
                Isbn = rd.GetString(1),
                Title = rd.GetString(2),
                Author = rd.GetString(3),
                Category = rd.GetString(4),
                Qty = rd.GetInt32(5),
                Out = rd.GetInt32(6),
                CoverPath = rd.GetString(7),
                CoverColor = rd.GetString(8).Length > 0 ? Color.FromArgb(Convert.ToInt32(rd.GetString(8))) : Color.SteelBlue
            });
        }
        return list;
    }

    /// <summary>Upserts every book, keeping primary keys stable (no delete-and-reinsert).</summary>
    public static void SaveBooks(IEnumerable<Book> books)
    {
        foreach (var b in books) SaveBookCore(b);
    }

    public static void SaveBook(Book book, string previousIsbn = null)
    {
        if (!string.IsNullOrEmpty(previousIsbn) && previousIsbn != book.Isbn)
        {
            using var rename = Db.Instance.Connection.CreateCommand();
            rename.CommandText = "UPDATE books SET isbn = @new WHERE isbn = @old";
            rename.Parameters.Add(new MySqlParameter("@new", book.Isbn ?? ""));
            rename.Parameters.Add(new MySqlParameter("@old", previousIsbn));
            rename.ExecuteNonQuery();
        }
        SaveBookCore(book);
    }

    static void SaveBookCore(Book b)
    {
        using var cmd = Db.Instance.Connection.CreateCommand();
        cmd.CommandText = @"INSERT INTO books
                                (isbn, title, author, category, total_copies, borrowed_copies,
                                 cover_path, cover_color)
                            VALUES
                                (@isbn, @title, @author, @cat, @tot, @out, @cp, @cc)
                            ON DUPLICATE KEY UPDATE
                                title = VALUES(title), author = VALUES(author), category = VALUES(category),
                                total_copies = VALUES(total_copies), borrowed_copies = VALUES(borrowed_copies),
                                cover_path = VALUES(cover_path), cover_color = VALUES(cover_color)";
        cmd.Parameters.Add(new MySqlParameter("@isbn", b.Isbn ?? "book"));
        cmd.Parameters.Add(new MySqlParameter("@title", b.Title ?? ""));
        cmd.Parameters.Add(new MySqlParameter("@author", b.Author ?? ""));
        cmd.Parameters.Add(new MySqlParameter("@cat", b.Category ?? "FIC"));
        cmd.Parameters.Add(new MySqlParameter("@tot", b.Qty));
        cmd.Parameters.Add(new MySqlParameter("@out", b.Out));
        cmd.Parameters.Add(new MySqlParameter("@cp", b.CoverPath ?? ""));
        cmd.Parameters.Add(new MySqlParameter("@cc", b.CoverColor.ToArgb().ToString()));
        cmd.ExecuteNonQuery();

        b.Id = GetBookId(b.Isbn) ?? b.Id;
    }

    public static void DeleteBook(Book book)
    {
        // Cart rows are ephemeral; remove them first so only real history can
        // block the delete. The transaction keeps the cart intact on failure.
        using var tx = Db.Instance.Connection.BeginTransaction();
        try
        {
            using (var cart = Db.Instance.Connection.CreateCommand())
            {
                cart.Transaction = tx;
                cart.CommandText = @"DELETE c FROM cart_items c
                                     JOIN books b ON c.book_id = b.id
                                     WHERE b.isbn = @isbn";
                cart.Parameters.Add(new MySqlParameter("@isbn", book.Isbn ?? ""));
                cart.ExecuteNonQuery();
            }

            using (var cmd = Db.Instance.Connection.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "DELETE FROM books WHERE isbn = @isbn";
                cmd.Parameters.Add(new MySqlParameter("@isbn", book.Isbn ?? ""));
                cmd.ExecuteNonQuery();
            }

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public static int? GetBookId(string isbn)
    {
        using var cmd = Db.Instance.Connection.CreateCommand();
        cmd.CommandText = "SELECT id FROM books WHERE isbn = @isbn";
        cmd.Parameters.Add(new MySqlParameter("@isbn", isbn ?? ""));
        var value = cmd.ExecuteScalar();
        return value == null || value == DBNull.Value ? null : Convert.ToInt32(value);
    }

    static int EnsureBook(string isbn, string title, string author, string coverPath = "")
    {
        using var cmd = Db.Instance.Connection.CreateCommand();
        cmd.CommandText = @"INSERT INTO books (isbn, title, author, category, total_copies, borrowed_copies, cover_path, cover_color)
                            VALUES (@isbn, @title, @author, 'FIC', 1, 0, @cover, '')
                            ON DUPLICATE KEY UPDATE isbn = VALUES(isbn)";
        cmd.Parameters.Add(new MySqlParameter("@isbn", isbn ?? "book"));
        cmd.Parameters.Add(new MySqlParameter("@title", title ?? "Unknown"));
        cmd.Parameters.Add(new MySqlParameter("@author", author ?? "Unknown"));
        cmd.Parameters.Add(new MySqlParameter("@cover", coverPath ?? ""));
        cmd.ExecuteNonQuery();
        return GetBookId(isbn) ?? 0;
    }

    // ================= Loans =================

    public static List<Loan> LoadLoans(IReadOnlyDictionary<string, Book> booksByIsbn)
    {
        var list = new List<Loan>();
        using var cmd = Db.Instance.Connection.CreateCommand();
        cmd.CommandText = @"SELECT l.id, b.isbn, l.student_id, l.borrowed_date, l.due_date, l.status
                            FROM loans l
                            JOIN books b ON l.book_id = b.id";
        using var rd = cmd.ExecuteReader();
        while (rd.Read())
        {
            var isbn = rd.GetString(1);
            if (string.IsNullOrEmpty(isbn) || !booksByIsbn.TryGetValue(isbn, out var book)) continue;
            list.Add(new Loan
            {
                Id = rd.GetInt32(0),
                Book = book,
                StudentId = rd.GetString(2),
                Borrowed = rd.GetDateTime(3),
                Due = rd.GetDateTime(4),
                Status = rd.GetString(5)
            });
        }
        return list;
    }

    /// <summary>Upserts the current loans and removes rows that are no longer active.</summary>
    public static void SaveLoans(IEnumerable<Loan> loans)
    {
        var list = loans.Where(l => l.Book != null && !string.IsNullOrEmpty(l.Book.Isbn)).ToList();

        foreach (var l in list)
        {
            int bookId = GetBookId(l.Book.Isbn) ?? EnsureBook(l.Book.Isbn, l.Book.Title, l.Book.Author, l.Book.CoverPath);

            if (l.Id > 0)
            {
                using var upd = Db.Instance.Connection.CreateCommand();
                upd.CommandText = @"UPDATE loans SET book_id = @bookId, student_id = @studentId,
                                        borrowed_date = @borrowed, due_date = @due, status = @status
                                    WHERE id = @id";
                AddLoanParameters(upd, l, bookId);
                upd.Parameters.Add(new MySqlParameter("@id", l.Id));
                if (upd.ExecuteNonQuery() > 0) continue;
            }

            using var ins = Db.Instance.Connection.CreateCommand();
            ins.CommandText = @"INSERT INTO loans (book_id, student_id, borrowed_date, due_date, status)
                                VALUES (@bookId, @studentId, @borrowed, @due, @status)";
            AddLoanParameters(ins, l, bookId);
            ins.ExecuteNonQuery();
            l.Id = (int)ins.LastInsertedId;
        }

        if (list.Count == 0)
        {
            using var del = Db.Instance.Connection.CreateCommand();
            del.CommandText = "DELETE FROM loans";
            del.ExecuteNonQuery();
            return;
        }

        using var stale = Db.Instance.Connection.CreateCommand();
        var names = new List<string>();
        for (int i = 0; i < list.Count; i++)
        {
            var name = "@id" + i;
            names.Add(name);
            stale.Parameters.Add(new MySqlParameter(name, list[i].Id));
        }
        stale.CommandText = "DELETE FROM loans WHERE id NOT IN (" + string.Join(", ", names) + ")";
        stale.ExecuteNonQuery();
    }

    static void AddLoanParameters(MySqlCommand cmd, Loan l, int bookId)
    {
        cmd.Parameters.Add(new MySqlParameter("@bookId", bookId));
        cmd.Parameters.Add(new MySqlParameter("@studentId", l.StudentId ?? ""));
        cmd.Parameters.Add(new MySqlParameter("@borrowed", l.Borrowed));
        cmd.Parameters.Add(new MySqlParameter("@due", l.Due));
        cmd.Parameters.Add(new MySqlParameter("@status", l.Status ?? "PENDING"));
    }

    // ================= History =================

    public static List<HistoryEntry> LoadHistory(IReadOnlyDictionary<string, Book> booksByIsbn)
    {
        var list = new List<HistoryEntry>();
        using var cmd = Db.Instance.Connection.CreateCommand();
        cmd.CommandText = @"SELECT h.id, b.isbn, b.title, b.author, h.student_id,
                                   h.borrowed_date, h.returned_date, h.remark
                            FROM history h
                            JOIN books b ON h.book_id = b.id
                            ORDER BY h.borrowed_date DESC, h.id DESC";
        using var rd = cmd.ExecuteReader();
        while (rd.Read())
        {
            var isbn = rd.GetString(1);
            var book = !string.IsNullOrEmpty(isbn) && booksByIsbn.TryGetValue(isbn, out var b)
                ? b
                : new Book { Isbn = isbn, Title = rd.GetString(2), Author = rd.GetString(3) };
            list.Add(new HistoryEntry
            {
                Id = rd.GetInt32(0),
                Book = book,
                StudentId = rd.GetString(4),
                Borrowed = rd.GetDateTime(5),
                Returned = rd.GetDateTime(6),
                Remark = rd.GetString(7)
            });
        }
        return list;
    }

    /// <summary>Upserts history rows; completed history is never deleted.</summary>
    public static void SaveHistory(IEnumerable<HistoryEntry> history)
    {
        foreach (var h in history.Where(h => h.Book != null && !string.IsNullOrEmpty(h.Book.Isbn)))
        {
            int bookId = GetBookId(h.Book.Isbn) ?? EnsureBook(h.Book.Isbn, h.Book.Title, h.Book.Author, h.Book.CoverPath);

            if (h.Id > 0)
            {
                using var upd = Db.Instance.Connection.CreateCommand();
                upd.CommandText = @"UPDATE history SET book_id = @bookId, student_id = @studentId,
                                        borrowed_date = @borrowed, returned_date = @returned, remark = @remark
                                    WHERE id = @id";
                AddHistoryParameters(upd, h, bookId);
                upd.Parameters.Add(new MySqlParameter("@id", h.Id));
                if (upd.ExecuteNonQuery() > 0) continue;
            }

            using var ins = Db.Instance.Connection.CreateCommand();
            ins.CommandText = @"INSERT INTO history (book_id, student_id, borrowed_date, returned_date, remark)
                                VALUES (@bookId, @studentId, @borrowed, @returned, @remark)";
            AddHistoryParameters(ins, h, bookId);
            ins.ExecuteNonQuery();
            h.Id = (int)ins.LastInsertedId;
        }
    }

    static void AddHistoryParameters(MySqlCommand cmd, HistoryEntry h, int bookId)
    {
        cmd.Parameters.Add(new MySqlParameter("@bookId", bookId));
        cmd.Parameters.Add(new MySqlParameter("@studentId", h.StudentId ?? ""));
        cmd.Parameters.Add(new MySqlParameter("@borrowed", h.Borrowed));
        cmd.Parameters.Add(new MySqlParameter("@returned", h.Returned));
        cmd.Parameters.Add(new MySqlParameter("@remark", h.Remark ?? ""));
    }

    // ================= Admins =================

    public static List<Admin> LoadAdmins()
    {
        var list = new List<Admin>();
        using var cmd = Db.Instance.Connection.CreateCommand();
        cmd.CommandText = "SELECT id, username FROM admins";
        using var rd = cmd.ExecuteReader();
        while (rd.Read())
            list.Add(new Admin { Id = rd.GetInt32(0), Username = rd.GetString(1) });
        return list;
    }

    /// <summary>Looks the admin up by username, then verifies the BCrypt hash.</summary>
    public static Admin GetAdmin(string username, string password)
    {
        int id;
        string name, hash;
        using (var cmd = Db.Instance.Connection.CreateCommand())
        {
            cmd.CommandText = "SELECT id, username, password_hash FROM admins WHERE username = @u";
            cmd.Parameters.Add(new MySqlParameter("@u", username ?? ""));
            using var rd = cmd.ExecuteReader();
            if (!rd.Read()) return null;
            id = rd.GetInt32(0);
            name = rd.GetString(1);
            hash = rd.GetString(2);
        }

        if (!VerifyAndUpgrade("admins", "id", id, password, hash)) return null;
        return new Admin { Id = id, Username = name };
    }

    /// <summary>Looks the student up by id, then verifies the BCrypt hash.</summary>
    public static Student GetStudent(string id, string password)
    {
        Student student;
        string hash;
        using (var cmd = Db.Instance.Connection.CreateCommand())
        {
            cmd.CommandText = @"SELECT student_id, full_name, email, program, year, section, contact,
                                       status, joined, avatar_path, is_sa, password_hash
                                FROM students WHERE student_id = @id";
            cmd.Parameters.Add(new MySqlParameter("@id", id ?? ""));
            using var rd = cmd.ExecuteReader();
            if (!rd.Read()) return null;
            student = new Student
            {
                Id = rd.GetString(0),
                Name = rd.GetString(1),
                Email = rd.IsDBNull(2) ? "" : rd.GetString(2),
                Program = rd.GetString(3),
                Year = rd.GetString(4),
                Section = rd.GetString(5),
                Contact = rd.GetString(6),
                Active = string.Equals(rd.GetString(7), "active", StringComparison.OrdinalIgnoreCase),
                Joined = rd.GetDateTime(8),
                AvatarPath = rd.GetString(9),
                IsSa = rd.GetByte(10) != 0
            };
            hash = rd.GetString(11);
        }

        if (!VerifyAndUpgrade("students", "student_id", id, password, hash)) return null;
        // Keep the plain text in memory only so the Account page can display it.
        student.Password = password ?? "";
        return student;
    }

    public static void SaveAdmin(Admin admin, string plainPassword)
    {
        using var cmd = Db.Instance.Connection.CreateCommand();
        cmd.CommandText = @"INSERT INTO admins (username, password_hash)
                            VALUES (@u, @hash)
                            ON DUPLICATE KEY UPDATE password_hash = VALUES(password_hash)";
        cmd.Parameters.Add(new MySqlParameter("@u", admin.Username));
        cmd.Parameters.Add(new MySqlParameter("@hash", HashPassword(plainPassword)));
        cmd.ExecuteNonQuery();
        admin.Id = Convert.ToInt32(cmd.LastInsertedId);
    }

    static void EnsureDefaultAdmin()
    {
        using var count = Db.Instance.Connection.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM admins";
        if (Convert.ToInt32(count.ExecuteScalar() ?? 0) > 0) return;

        using var insert = Db.Instance.Connection.CreateCommand();
        insert.CommandText = "INSERT INTO admins (username, password_hash) VALUES ('admin', @hash)";
        insert.Parameters.Add(new MySqlParameter("@hash", HashPassword("admin123")));
        insert.ExecuteNonQuery();
    }

    // ================= Cart =================

    public static List<string> LoadCartIsbns(string studentId)
    {
        var isbns = new List<string>();
        if (string.IsNullOrEmpty(studentId)) return isbns;

        using var cmd = Db.Instance.Connection.CreateCommand();
        cmd.CommandText = @"SELECT b.isbn
                            FROM cart_items c
                            JOIN books b ON c.book_id = b.id
                            WHERE c.student_id = @id
                            ORDER BY c.added_at";
        cmd.Parameters.Add(new MySqlParameter("@id", studentId));
        using var rd = cmd.ExecuteReader();
        while (rd.Read()) isbns.Add(rd.GetString(0));
        return isbns;
    }

    public static void AddToCart(string studentId, Book book)
    {
        if (string.IsNullOrEmpty(studentId) || book == null) return;

        using var cmd = Db.Instance.Connection.CreateCommand();
        cmd.CommandText = @"INSERT IGNORE INTO cart_items (student_id, book_id)
                            SELECT @studentId, id FROM books WHERE isbn = @isbn";
        cmd.Parameters.Add(new MySqlParameter("@studentId", studentId));
        cmd.Parameters.Add(new MySqlParameter("@isbn", book.Isbn ?? ""));
        cmd.ExecuteNonQuery();
    }

    public static void RemoveFromCart(string studentId, Book book)
    {
        if (string.IsNullOrEmpty(studentId) || book == null) return;

        using var cmd = Db.Instance.Connection.CreateCommand();
        cmd.CommandText = @"DELETE c FROM cart_items c
                            JOIN books b ON c.book_id = b.id
                            WHERE c.student_id = @studentId AND b.isbn = @isbn";
        cmd.Parameters.Add(new MySqlParameter("@studentId", studentId));
        cmd.Parameters.Add(new MySqlParameter("@isbn", book.Isbn ?? ""));
        cmd.ExecuteNonQuery();
    }

    public static void ClearCart(string studentId)
    {
        if (string.IsNullOrEmpty(studentId)) return;

        using var cmd = Db.Instance.Connection.CreateCommand();
        cmd.CommandText = "DELETE FROM cart_items WHERE student_id = @id";
        cmd.Parameters.Add(new MySqlParameter("@id", studentId));
        cmd.ExecuteNonQuery();
    }

    /// <summary>True when MySQL refused a delete because another table still references the row.</summary>
    public static bool IsReferenceBlocked(Exception ex) =>
        ex is MySqlException mysql && mysql.Number == 1451;
}

/// <summary>Validation for student profile fields.</summary>
public static class Validator
{
    public static (bool valid, string error) ValidateFullName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return (false, "Full Name is required");
        var words = name.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 2) return (false, "Full Name must have at least 2 words");
        return (true, "");
    }

    public static (bool valid, string error) ValidateStudentId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return (false, "Student ID is required");
        var pattern = @"^\d{8}-[CNScns]$";
        if (!System.Text.RegularExpressions.Regex.IsMatch(id, pattern)) return (false, "Format: 8 digits-letter (e.g., 20251362-C). Letter must be C, N, or S");
        return (true, "");
    }

    public static string NormalizeStudentId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return id;
        var parts = id.Split('-');
        if (parts.Length == 2) return $"{parts[0]}-{parts[1].ToUpper()}";
        return id;
    }

    public static (bool valid, string error) ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return (false, "Email is required");
        var pattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
        if (!System.Text.RegularExpressions.Regex.IsMatch(email, pattern)) return (false, "Invalid email format (e.g., email@sample.com)");
        return (true, "");
    }

    public static (bool valid, string error) ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password)) return (false, "Password is required");
        if (password.Length < 4) return (false, "Password must be at least 4 characters");
        return (true, "");
    }

    public static (bool valid, string error) ValidateProgram(string program)
    {
        if (string.IsNullOrWhiteSpace(program)) return (false, "Program is required");
        if (System.Text.RegularExpressions.Regex.IsMatch(program, @"\d")) return (false, "Program cannot contain numbers");
        return (true, "");
    }

    public static (bool valid, string error) ValidateSection(string section)
    {
        if (string.IsNullOrWhiteSpace(section)) return (false, "Section is required");
        if (!System.Text.RegularExpressions.Regex.IsMatch(section, @"^[A-F]$")) return (false, "Section must be A, B, C, D, E, or F");
        return (true, "");
    }

    public static (bool valid, string error) ValidateYear(string year)
    {
        if (string.IsNullOrWhiteSpace(year)) return (false, "Year is required");
        var digit = year.Trim().Length > 0 && char.IsDigit(year.Trim()[0]) ? year.Trim()[0].ToString() : year.Trim();
        if (!new[] { "1", "2", "3", "4" }.Contains(digit)) return (false, "Year must be 1, 2, 3, or 4");
        return (true, "");
    }

    public static (bool valid, string error) ValidateContactNumber(string contact)
    {
        if (string.IsNullOrWhiteSpace(contact)) return (false, "Contact Number is required");
        if (!System.Text.RegularExpressions.Regex.IsMatch(contact, @"^09\d{9}$")) return (false, "Must be exactly 11 digits starting with 09 (e.g., 09123456789)");
        return (true, "");
    }
}

/// <summary>One row of <c>book_requests</c> joined to the book ISBN.</summary>
public class RequestDbRecord
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "Borrow";
    public string StudentId { get; set; } = "";
    public string BookIsbn { get; set; } = "";
    public DateTime Date { get; set; } = DateTime.Now;
    public string Status { get; set; } = "Pending";
}
