using System.Drawing;
using System.Text.Json;
using System.Text.RegularExpressions;
using MySqlConnector;

namespace LibraryInventory;

public static class Database
{
    // ---- password hashing (bcrypt) ----
    public static bool IsHash(string v) => !string.IsNullOrEmpty(v) && v.StartsWith("$2") && v.Length == 60;
    public static string HashPassword(string p) => IsHash(p) ? p : BCrypt.Net.BCrypt.HashPassword(p ?? "");
    public static bool CheckPassword(string typed, string stored)
        => IsHash(stored) ? BCrypt.Net.BCrypt.Verify(typed ?? "", stored) : stored == typed; // plain-text = old accounts


    static readonly string DataRoot;
    static readonly string DbPath;
    static readonly string AvatarDir;
    public static readonly string PhotosDir;
    public static readonly string CoversDir;
    public static readonly string CoversDbPath;
    public static readonly string RequestsDbPath;

    static Database()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var dir = new DirectoryInfo(baseDir);
        while (dir != null && !dir.GetFiles("*.csproj").Any())
            dir = dir.Parent;
        var projectParent = dir?.Parent?.FullName ?? baseDir;
        DataRoot = Path.Combine(projectParent, "data");
        DbPath = Path.Combine(DataRoot, "students.json");
        AvatarDir = Path.Combine(DataRoot, "avatars");
        PhotosDir = Path.Combine(DataRoot, "photos");
        CoversDir = Path.Combine(DataRoot, "covers");
        CoversDbPath = Path.Combine(DataRoot, "covers.json");
        RequestsDbPath = Path.Combine(DataRoot, "requests.json");
        Directory.CreateDirectory(DataRoot);
        Directory.CreateDirectory(AvatarDir);
        Directory.CreateDirectory(PhotosDir);
        Directory.CreateDirectory(CoversDir);
        EnsureSchema();
        EnsureDefaultAdmin();
    }

    static void EnsureSchema()
    {
        try
        {
            string[] creates = new[]
            {
                @"CREATE TABLE IF NOT EXISTS students (
                    student_id VARCHAR(20) NOT NULL,
                    full_name VARCHAR(140) NOT NULL,
                    email VARCHAR(160) NOT NULL,
                    password VARCHAR(120) NOT NULL,
                    program VARCHAR(60) NOT NULL,
                    year VARCHAR(10) NOT NULL,
                    section VARCHAR(8) NOT NULL,
                    contact VARCHAR(20) NOT NULL,
                    status TINYINT(1) NOT NULL DEFAULT 1,
                    joined DATE NOT NULL,
                    avatar_path VARCHAR(260) NOT NULL DEFAULT '',
                    role VARCHAR(20) NOT NULL DEFAULT 'student',
                    is_sa TINYINT(1) NOT NULL DEFAULT 0,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (student_id)
                ) ENGINE=InnoDB",
                @"CREATE TABLE IF NOT EXISTS books (
                    id INT AUTO_INCREMENT PRIMARY KEY,
                    isbn VARCHAR(40) NOT NULL UNIQUE,
                    title VARCHAR(200) NOT NULL,
                    author VARCHAR(160) NOT NULL,
                    category VARCHAR(8) NOT NULL,
                    total_copies INT NOT NULL DEFAULT 1,
                    borrowed_copies INT NOT NULL DEFAULT 0,
                    cover_path VARCHAR(260) NOT NULL DEFAULT '',
                    cover_color VARCHAR(20) NOT NULL DEFAULT '',
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                ) ENGINE=InnoDB",
                @"CREATE TABLE IF NOT EXISTS loans (
                    id INT AUTO_INCREMENT PRIMARY KEY,
                    book_id INT NOT NULL,
                    student_id VARCHAR(20) NOT NULL,
                    borrowed_date DATETIME NOT NULL,
                    due_date DATETIME NOT NULL,
                    status VARCHAR(12) NOT NULL DEFAULT 'PENDING',
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                ) ENGINE=InnoDB",
                @"CREATE TABLE IF NOT EXISTS book_requests (
                    id VARCHAR(40) PRIMARY KEY,
                    type VARCHAR(10) NOT NULL,
                    book_id INT NOT NULL,
                    student_id VARCHAR(20) NOT NULL,
                    student_name VARCHAR(140) NOT NULL DEFAULT '',
                    book_title VARCHAR(200) NOT NULL DEFAULT '',
                    book_author VARCHAR(160) NOT NULL DEFAULT '',
                    book_isbn VARCHAR(40) NOT NULL DEFAULT '',
                    book_cover_path VARCHAR(260) NOT NULL DEFAULT '',
                    request_date DATETIME NOT NULL,
                    status VARCHAR(12) NOT NULL DEFAULT 'Pending',
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                ) ENGINE=InnoDB",
                @"CREATE TABLE IF NOT EXISTS history (
                    id INT AUTO_INCREMENT PRIMARY KEY,
                    book_id INT NOT NULL,
                    student_id VARCHAR(20) NOT NULL,
                    borrowed_date DATETIME NOT NULL,
                    returned_date DATETIME NOT NULL,
                    remark VARCHAR(20) NOT NULL DEFAULT 'On time',
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                ) ENGINE=InnoDB",
                @"CREATE TABLE IF NOT EXISTS admins (
                    id INT AUTO_INCREMENT PRIMARY KEY,
                    username VARCHAR(60) UNIQUE NOT NULL,
                    password VARCHAR(120) NOT NULL,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                ) ENGINE=InnoDB"
            };

            foreach (var sql in creates)
            {
                try
                {
                    using var cmd = Db.Instance.Connection.CreateCommand();
                    cmd.CommandText = sql;
                    cmd.ExecuteNonQuery();
                }
                catch (Exception e)
                {
                    Console.WriteLine("Create table failed: " + e.Message);
                }
            }
 
            // Upgrade tables created by older versions of the app
            foreach (var alter in new[] {
                "ALTER TABLE students ADD COLUMN is_sa TINYINT(1) NOT NULL DEFAULT 0",
                "ALTER TABLE students ADD COLUMN role VARCHAR(20) NOT NULL DEFAULT 'student'",
                "ALTER TABLE students ADD COLUMN avatar_path VARCHAR(260) NOT NULL DEFAULT ''" })
            {
                try { using var a = Db.Instance.Connection.CreateCommand(); a.CommandText = alter; a.ExecuteNonQuery(); }
                catch { /* column already exists */ }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Schema ensure failed: " + e.Message);
        }
    }

    // ================= Students =================

    public static List<Student> LoadStudents()
    {
        var list = new List<Student>();
        using var cmd = Db.Instance.Connection.CreateCommand();
        cmd.CommandText = "SELECT student_id, full_name, email, password, program, year, section, contact, status, joined, avatar_path, is_sa FROM students";
        using var rd = cmd.ExecuteReader();
        while (rd.Read())
        {
            list.Add(new Student
            {
                Id = rd.GetString(0),
                Name = rd.GetString(1),
                Email = rd.GetString(2),
                Password = rd.GetString(3),
                Program = rd.GetString(4),
                Year = rd.GetString(5),
                Section = rd.GetString(6),
                Contact = rd.GetString(7),
                Active = rd.GetByte(8) != 0,
                Joined = rd.GetDateTime(9),
                AvatarPath = rd.GetString(10),
                IsSa = rd.GetByte(11) != 0
            });
        }
        return list;
    }

    public static void SaveStudents(List<Student> students)
    {
        using var cmd = Db.Instance.Connection.CreateCommand();
        cmd.CommandText = "DELETE FROM students";
        cmd.ExecuteNonQuery();
        foreach (var s in students) SaveStudentToDatabase(s);
    }

    public static void SaveStudent(Student student, string previousId = null)
    {
        if (!string.IsNullOrEmpty(previousId) && previousId != student.Id)
        {
            using var del = Db.Instance.Connection.CreateCommand();
            del.CommandText = "DELETE FROM students WHERE student_id = @id";
            del.Parameters.Add(new MySqlParameter("@id", previousId));
            del.ExecuteNonQuery();
        }
        SaveStudentToDatabase(student);
    }

    static void SaveStudentToDatabase(Student s)
    {
        using var cmd = Db.Instance.Connection.CreateCommand();
        cmd.CommandText = @"INSERT INTO students (student_id, full_name, email, password, program, year, section, contact, status, joined, avatar_path, role, is_sa)
                            VALUES (@id, @name, @email, @pass, @program, @year, @section, @contact, @status, @joined, @avatar, @role, @issa)
                            ON DUPLICATE KEY UPDATE full_name = VALUES(full_name), email = VALUES(email), password = VALUES(password),
                                program = VALUES(program), year = VALUES(year), section = VALUES(section), contact = VALUES(contact),
                                status = VALUES(status), joined = VALUES(joined), avatar_path = VALUES(avatar_path), is_sa = VALUES(is_sa);";
        cmd.Parameters.Add(new MySqlParameter("@id", s.Id));
        cmd.Parameters.Add(new MySqlParameter("@name", s.Name ?? ""));
        cmd.Parameters.Add(new MySqlParameter("@email", s.Email ?? ""));
        cmd.Parameters.Add(new MySqlParameter("@pass", HashPassword(s.Password ?? "")));
        cmd.Parameters.Add(new MySqlParameter("@program", s.Program ?? ""));
        cmd.Parameters.Add(new MySqlParameter("@year", s.Year ?? ""));
        cmd.Parameters.Add(new MySqlParameter("@section", s.Section ?? ""));
        cmd.Parameters.Add(new MySqlParameter("@contact", s.Contact ?? ""));
        cmd.Parameters.Add(new MySqlParameter("@status", s.Active ? 1 : 0));
        cmd.Parameters.Add(new MySqlParameter("@joined", s.Joined));
        cmd.Parameters.Add(new MySqlParameter("@avatar", s.AvatarPath ?? ""));
        cmd.Parameters.Add(new MySqlParameter("@role", "student"));
        cmd.Parameters.Add(new MySqlParameter("@issa", s.IsSa ? 1 : 0));
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
        using (var cmd = Db.Instance.Connection.CreateCommand())
        {
            cmd.CommandText = "SELECT id, type, student_id, student_name, book_title, book_author, book_isbn, book_cover_path, request_date, status FROM book_requests";
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                list.Add(new RequestDbRecord
                {
                    Id = rd.GetString(0),
                    Type = rd.GetString(1),
                    StudentId = rd.GetString(2),
                    StudentName = rd.GetString(3),
                    BookTitle = rd.GetString(4),
                    BookAuthor = rd.GetString(5),
                    BookIsbn = rd.GetString(6),
                    BookCoverPath = rd.GetString(7),
                    Date = rd.GetDateTime(8),
                    Status = rd.GetString(9)
                });
            }
        }
        // An empty table stays empty — requests are only created by students
        // through the cart; no dummy data is seeded.
        return list;
    }

    public static void SaveRequests(IEnumerable<RequestDbRecord> records)
    {
        using var cmd = Db.Instance.Connection.CreateCommand();
        cmd.CommandText = "DELETE FROM book_requests";
        cmd.ExecuteNonQuery();
        foreach (var r in records)
        {
            int bookId = EnsureBook(r.BookIsbn, r.BookTitle, r.BookAuthor, r.BookCoverPath);
            using var ins = Db.Instance.Connection.CreateCommand();
            ins.CommandText = "INSERT INTO book_requests (id, type, book_id, student_id, student_name, book_title, book_author, book_isbn, book_cover_path, request_date, status) VALUES (@id, @type, @bookId, @studentId, @studentName, @bookTitle, @bookAuthor, @bookIsbn, @bookCoverPath, @date, @status)";
            ins.Parameters.Add(new MySqlParameter("@id", r.Id));
            ins.Parameters.Add(new MySqlParameter("@type", r.Type));
            ins.Parameters.Add(new MySqlParameter("@bookId", bookId));
            ins.Parameters.Add(new MySqlParameter("@studentId", r.StudentId));
            ins.Parameters.Add(new MySqlParameter("@studentName", r.StudentName ?? ""));
            ins.Parameters.Add(new MySqlParameter("@bookTitle", r.BookTitle ?? ""));
            ins.Parameters.Add(new MySqlParameter("@bookAuthor", r.BookAuthor ?? ""));
            ins.Parameters.Add(new MySqlParameter("@bookIsbn", r.BookIsbn ?? ""));
            ins.Parameters.Add(new MySqlParameter("@bookCoverPath", r.BookCoverPath ?? ""));
            ins.Parameters.Add(new MySqlParameter("@date", r.Date));
            ins.Parameters.Add(new MySqlParameter("@status", r.Status));
            ins.ExecuteNonQuery();
        }
    }

    // ================= Covers =================

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

    public static Dictionary<string, string> LoadCoverMap()
    {
        var map = new Dictionary<string, string>();
        using var cmd = Db.Instance.Connection.CreateCommand();
        cmd.CommandText = "SELECT isbn, cover_path FROM books WHERE cover_path <> ''";
        using var rd = cmd.ExecuteReader();
        while (rd.Read()) map[rd.GetString(0)] = rd.GetString(1);
        return map;
    }

    // ================= Images =================

    public static Image? LoadAvatar(string avatarPath) => LoadImage(avatarPath);

    public static Image? LoadImage(string imagePath)
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
        cmd.CommandText = "SELECT isbn, title, author, category, total_copies, borrowed_copies, cover_path, cover_color FROM books";
        using var rd = cmd.ExecuteReader();
        while (rd.Read())
        {
            list.Add(new Book
            {
                Isbn = rd.GetString(0),
                Title = rd.GetString(1),
                Author = rd.GetString(2),
                Category = rd.GetString(3),
                Qty = rd.GetInt32(4),
                Out = rd.GetInt32(5),
                CoverPath = rd.GetString(6),
                CoverColor = rd.GetString(7).Length > 0 ? Color.FromArgb(Convert.ToInt32(rd.GetString(7))) : Color.SteelBlue
            });
        }
        return list;
    }

    public static void SaveBooks(List<Book> books)
    {
        using var cmd = Db.Instance.Connection.CreateCommand();
        cmd.CommandText = "DELETE FROM books";
        cmd.ExecuteNonQuery();
        foreach (var b in books)
        {
            using var ins = Db.Instance.Connection.CreateCommand();
            ins.CommandText = "INSERT INTO books (isbn, title, author, category, total_copies, borrowed_copies, cover_path, cover_color) VALUES (@isbn, @title, @author, @cat, @tot, @out, @cp, @cc)";
            ins.Parameters.Add(new MySqlParameter("@isbn", b.Isbn ?? "book"));
            ins.Parameters.Add(new MySqlParameter("@title", b.Title ?? ""));
            ins.Parameters.Add(new MySqlParameter("@author", b.Author ?? ""));
            ins.Parameters.Add(new MySqlParameter("@cat", b.Category ?? "FIC"));
            ins.Parameters.Add(new MySqlParameter("@tot", b.Qty));
            ins.Parameters.Add(new MySqlParameter("@out", b.Out));
            ins.Parameters.Add(new MySqlParameter("@cp", b.CoverPath ?? ""));
            ins.Parameters.Add(new MySqlParameter("@cc", b.CoverColor.ToArgb().ToString()));
            ins.ExecuteNonQuery();
        }
    }

    // ================= Loans =================

    /// <summary>Loads active loans, joining to books for the ISBN used as the app-side key.</summary>
    public static List<Loan> LoadLoans(IReadOnlyDictionary<string, Book> booksByIsbn)
    {
        var list = new List<Loan>();
        try
        {
            using var cmd = Db.Instance.Connection.CreateCommand();
            cmd.CommandText = @"SELECT b.isbn, l.student_id, l.borrowed_date, l.due_date, l.status
                                FROM loans l JOIN books b ON l.book_id = b.id";
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                var isbn = rd.GetString(0);
                if (string.IsNullOrEmpty(isbn) || !booksByIsbn.TryGetValue(isbn, out var book)) continue;
                list.Add(new Loan
                {
                    Book = book,
                    StudentId = rd.GetString(1),
                    Borrowed = rd.GetDateTime(2),
                    Due = rd.GetDateTime(3),
                    Status = rd.GetString(4)
                });
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Load loans failed: " + e.Message);
        }
        return list;
    }

    /// <summary>Full rewrite of the loans table from the in-memory list.</summary>
    public static void SaveLoans(IEnumerable<Loan> loans)
    {
        try
        {
            using var del = Db.Instance.Connection.CreateCommand();
            del.CommandText = "DELETE FROM loans";
            del.ExecuteNonQuery();

            foreach (var l in loans.Where(l => l.Book != null))
            {
                int bookId = EnsureBook(l.Book.Isbn, l.Book.Title, l.Book.Author, l.Book.CoverPath);
                using var ins = Db.Instance.Connection.CreateCommand();
                ins.CommandText = "INSERT INTO loans (book_id, student_id, borrowed_date, due_date, status) VALUES (@bookId, @studentId, @borrowed, @due, @status)";
                ins.Parameters.Add(new MySqlParameter("@bookId", bookId));
                ins.Parameters.Add(new MySqlParameter("@studentId", l.StudentId ?? ""));
                ins.Parameters.Add(new MySqlParameter("@borrowed", l.Borrowed));
                ins.Parameters.Add(new MySqlParameter("@due", l.Due));
                ins.Parameters.Add(new MySqlParameter("@status", l.Status ?? ""));
                ins.ExecuteNonQuery();
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Save loans failed: " + e.Message);
        }
    }

    // ================= History =================

    /// <summary>Loads history rows (newest first). Books are re-linked to the shared collection when present.</summary>
    public static List<HistoryEntry> LoadHistory(IReadOnlyDictionary<string, Book> booksByIsbn)
    {
        var list = new List<HistoryEntry>();
        try
        {
            using var cmd = Db.Instance.Connection.CreateCommand();
            cmd.CommandText = @"SELECT b.isbn, b.title, b.author, h.student_id, h.borrowed_date, h.returned_date, h.remark
                                FROM history h JOIN books b ON h.book_id = b.id
                                ORDER BY h.borrowed_date DESC, h.id DESC";
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                var isbn = rd.GetString(0);
                var book = !string.IsNullOrEmpty(isbn) && booksByIsbn.TryGetValue(isbn, out var b)
                    ? b
                    : new Book { Isbn = isbn, Title = rd.GetString(1), Author = rd.GetString(2) };
                list.Add(new HistoryEntry
                {
                    Book = book,
                    StudentId = rd.GetString(3),
                    Borrowed = rd.GetDateTime(4),
                    Returned = rd.GetDateTime(5),
                    Remark = rd.GetString(6)
                });
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Load history failed: " + e.Message);
        }
        return list;
    }

    /// <summary>Full rewrite of the history table from the in-memory list.</summary>
    public static void SaveHistory(IEnumerable<HistoryEntry> history)
    {
        try
        {
            using var del = Db.Instance.Connection.CreateCommand();
            del.CommandText = "DELETE FROM history";
            del.ExecuteNonQuery();

            foreach (var h in history.Where(h => h.Book != null))
            {
                int bookId = EnsureBook(h.Book.Isbn, h.Book.Title, h.Book.Author, h.Book.CoverPath);
                using var ins = Db.Instance.Connection.CreateCommand();
                ins.CommandText = "INSERT INTO history (book_id, student_id, borrowed_date, returned_date, remark) VALUES (@bookId, @studentId, @borrowed, @returned, @remark)";
                ins.Parameters.Add(new MySqlParameter("@bookId", bookId));
                ins.Parameters.Add(new MySqlParameter("@studentId", h.StudentId ?? ""));
                ins.Parameters.Add(new MySqlParameter("@borrowed", h.Borrowed));
                ins.Parameters.Add(new MySqlParameter("@returned", h.Returned));
                ins.Parameters.Add(new MySqlParameter("@remark", h.Remark ?? ""));
                ins.ExecuteNonQuery();
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Save history failed: " + e.Message);
        }
    }

    // ================= Admins =================

    public static List<Admin> LoadAdmins()
    {
        var list = new List<Admin>();
        using var cmd = Db.Instance.Connection.CreateCommand();
        cmd.CommandText = "SELECT id, username, password FROM admins";
        using var rd = cmd.ExecuteReader();
        while (rd.Read())
        {
            list.Add(new Admin { Id = rd.GetInt32(0), Username = rd.GetString(1), Password = rd.GetString(2) });
        }
        return list;
    }

    public static Admin GetAdmin(string username, string password)
    {
        try
        {
            using var cmd = Db.Instance.Connection.CreateCommand();
            cmd.CommandText = "SELECT id, username, password FROM admins WHERE username = @u";
            cmd.Parameters.Add(new MySqlParameter("@u", username ?? ""));
            Admin found = null;
            using (var rd = cmd.ExecuteReader())
                if (rd.Read()) found = new Admin { Id = rd.GetInt32(0), Username = rd.GetString(1), Password = rd.GetString(2) };
            if (found == null || !CheckPassword(password, found.Password)) return null;
            if (!IsHash(found.Password))   // upgrade old plain-text password
            {
                using var up = Db.Instance.Connection.CreateCommand();
                up.CommandText = "UPDATE admins SET password = @h WHERE id = @id";
                up.Parameters.Add(new MySqlParameter("@h", HashPassword(password)));
                up.Parameters.Add(new MySqlParameter("@id", found.Id));
                up.ExecuteNonQuery();
            }
            return found;
        }
        catch (Exception e)
        {
            Console.WriteLine("Admin lookup failed: " + e.Message);
            return null;
        }
    }

    public static void DeleteStudent(string id)
    {
        using var cmd = Db.Instance.Connection.CreateCommand();
        cmd.CommandText = "DELETE FROM students WHERE student_id = @id";
        cmd.Parameters.Add(new MySqlParameter("@id", id));
        cmd.ExecuteNonQuery();
    }

    public static bool StudentExists(string id)
    {
        using var cmd = Db.Instance.Connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM students WHERE student_id = @id";
        cmd.Parameters.Add(new MySqlParameter("@id", id));
        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0) > 0;
    }

    public static Student GetStudent(string id, string password)
    {
        try
        {
            using var cmd = Db.Instance.Connection.CreateCommand();
            cmd.CommandText = "SELECT student_id, full_name, email, password, program, year, section, contact, status, joined, avatar_path, is_sa FROM students WHERE student_id = @id";
            cmd.Parameters.Add(new MySqlParameter("@id", id));
            using var rd = cmd.ExecuteReader();
            if (rd.Read() && CheckPassword(password, rd.GetString(3)))
            {
                var st = new Student
                {
                    Id = rd.GetString(0),
                    Name = rd.GetString(1),
                    Email = rd.GetString(2),
                    Password = rd.GetString(3),
                    Program = rd.GetString(4),
                    Year = rd.GetString(5),
                    Section = rd.GetString(6),
                    Contact = rd.GetString(7),
                    Active = rd.GetByte(8) != 0,
                    Joined = rd.GetDateTime(9),
                    AvatarPath = rd.GetString(10),
                    IsSa = rd.GetByte(11) != 0
                };
                rd.Close();
                if (!IsHash(st.Password)) { st.Password = HashPassword(st.Password); SaveStudentToDatabase(st); }
                return st;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Student lookup failed: " + e.Message);
        }
        return null;
    }

    public static void SaveAdmin(Admin admin)
    {
        using var cmd = Db.Instance.Connection.CreateCommand();
        cmd.CommandText = "INSERT INTO admins (username, password) VALUES (@u, @p) ON DUPLICATE KEY UPDATE password = VALUES(password);";
        cmd.Parameters.Add(new MySqlParameter("@u", admin.Username));
        cmd.Parameters.Add(new MySqlParameter("@p", HashPassword(admin.Password)));
        cmd.ExecuteNonQuery();
    }

    // ================= Bootstrap =================

    /// <summary>Seeds a default admin account if the admins table is empty (fresh install).</summary>
    static void EnsureDefaultAdmin()
    {
        try
        {
            using var admCmd = Db.Instance.Connection.CreateCommand();
            admCmd.CommandText = "SELECT COUNT(*) FROM admins";
            var adminCount = Convert.ToInt32(admCmd.ExecuteScalar() ?? 0);
            if (adminCount == 0)
            {
                using var insAdm = Db.Instance.Connection.CreateCommand();
                insAdm.CommandText = "INSERT INTO admins (username, password) VALUES ('admin', '" + HashPassword("admin123") + "')";
                insAdm.ExecuteNonQuery();
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Admin bootstrap failed: " + e.Message);
        }
    }

    static int EnsureBook(string isbn, string title, string author, string coverPath = "")
    {
        using var cmd = Db.Instance.Connection.CreateCommand();
        cmd.CommandText = @"INSERT INTO books (isbn, title, author, category, total_copies, borrowed_copies, cover_path, cover_color)
                            VALUES (@isbn, @title, @author, 'FIC', 1, 0, @cover, '')
                            ON DUPLICATE KEY UPDATE title = VALUES(title), author = VALUES(author), cover_path = VALUES(cover_path);";
        cmd.Parameters.Add(new MySqlParameter("@isbn", isbn ?? "book"));
        cmd.Parameters.Add(new MySqlParameter("@title", title ?? "Unknown"));
        cmd.Parameters.Add(new MySqlParameter("@author", author ?? "Unknown"));
        cmd.Parameters.Add(new MySqlParameter("@cover", coverPath ?? ""));
        cmd.ExecuteNonQuery();

        using var sel = Db.Instance.Connection.CreateCommand();
        sel.CommandText = "SELECT id FROM books WHERE isbn = @isbn";
        sel.Parameters.Add(new MySqlParameter("@isbn", isbn ?? "book"));
        var id = sel.ExecuteScalar();
        return id == null ? 0 : Convert.ToInt32(id);
    }
}

/// <summary>Validation for student profile fields.</summary>
public static class Validator
{
    public static (bool valid, string error) ValidateFullName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return (false, "Full Name is required");
        var words = name.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 2) return (false, "Full Name must have at least 2 words");
        if (!System.Text.RegularExpressions.Regex.IsMatch(name.Trim(), @"^[\p{L} ]+$"))
            return (false, "Full Name can only contain letters and spaces");
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
        if (string.IsNullOrWhiteSpace(program) || program.Trim() == "-") return (false, "Program is required");
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

public class RequestDbRecord
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "Borrow";
    public string StudentId { get; set; } = "";
    public string StudentName { get; set; } = "";
    public string BookTitle { get; set; } = "";
    public string BookAuthor { get; set; } = "";
    public string BookIsbn { get; set; } = "";
    public string BookCoverPath { get; set; } = "";
    public DateTime Date { get; set; } = DateTime.Now;
    public string Status { get; set; } = "Pending";
}
