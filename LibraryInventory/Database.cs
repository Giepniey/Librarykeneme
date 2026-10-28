using System.Text.Json;
using System.Text.RegularExpressions;

namespace LibraryInventory;

/// <summary>Simple JSON file-based database for student records.
/// Data is stored in a dedicated "data" folder next to the project directory,
/// keeping it separate from the UI/source code.</summary>
public static class Database
{
    /// <summary>Root data folder lives beside the project source directory (e.g. LibraryInventory/data/).</summary>
    static readonly string DataRoot;
    static readonly string DbPath;
    static readonly string AvatarDir;
    /// <summary>Storage folder for profile photos (to be used for photo features).</summary>
    public static readonly string PhotosDir;
    /// <summary>Storage folder for uploaded book covers (sibling of the profile photos folder).</summary>
    public static readonly string CoversDir;
    /// <summary>Database file mapping ISBN → cover file path (data/covers.json).</summary>
    public static readonly string CoversDbPath;
    /// <summary>Database file storing student book requests (data/requests.json).</summary>
    public static readonly string RequestsDbPath;

    static Database()
    {
        // Walk up from the output directory to find the project root (the folder that contains the .csproj),
        // then place the "data" folder as a sibling so it's separate from the UI code.
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var dir = new DirectoryInfo(baseDir);
        while (dir != null && !dir.GetFiles("*.csproj").Any())
            dir = dir.Parent;
        // dir is now the project folder (e.g. …/LibraryInventory/LibraryInventory).
        // Place data one level up so it sits beside the project folder.
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
    }

    public static List<Student> LoadStudents()
    {
        if (!File.Exists(DbPath))
            return new List<Student>();

        try
        {
            var json = File.ReadAllText(DbPath);
            return JsonSerializer.Deserialize<List<Student>>(json) ?? new List<Student>();
        }
        catch
        {
            return new List<Student>();
        }
    }

    public static void SaveStudents(List<Student> students)
    {
        var json = JsonSerializer.Serialize(students, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(DbPath, json);
    }

    /// <summary>Saves a student record. If the student's Id was changed during an edit,
    /// pass the old Id as <paramref name="previousId"/> so the record stored under it is replaced.</summary>
    public static void SaveStudent(Student student, string previousId = null)
    {
        var students = LoadStudents();
        if (!string.IsNullOrEmpty(previousId) && previousId != student.Id)
            students.RemoveAll(s => s.Id == previousId);
        var existing = students.FirstOrDefault(s => s.Id == student.Id);
        if (existing != null)
        {
            var index = students.IndexOf(existing);
            students[index] = student;
        }
        else
        {
            students.Add(student);
        }
        SaveStudents(students);
    }

    public static string SaveAvatar(string studentId, Image image)
    {
        var fileName = $"{studentId}.png";
        var filePath = Path.Combine(AvatarDir, fileName);
        image.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
        return filePath;
    }

    public static List<RequestDbRecord> LoadRequests()
    {
        if (!File.Exists(RequestsDbPath))
            return SeedRequests();

        try
        {
            var json = File.ReadAllText(RequestsDbPath);
            var list = JsonSerializer.Deserialize<List<RequestDbRecord>>(json);
            if (list == null || list.Count == 0)
                return SeedRequests();
            return list;
        }
        catch
        {
            return SeedRequests();
        }
    }

    public static void SaveRequests(IEnumerable<RequestDbRecord> records)
    {
        var json = JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(RequestsDbPath, json);
    }

    static List<RequestDbRecord> SeedRequests()
    {
        var list = new List<RequestDbRecord>
        {
            new RequestDbRecord
            {
                Id = "BR-2025-0048",
                Type = "Borrow",
                StudentId = "2023-0142",
                StudentName = "Maria Santos",
                BookTitle = "The Alchemist",
                BookAuthor = "Paulo Coelho",
                BookIsbn = "9780062315007",
                Date = new DateTime(2025, 5, 16, 9, 30, 0),
                Status = "Pending"
            },
            new RequestDbRecord
            {
                Id = "BR-2025-0049",
                Type = "Borrow",
                StudentId = "2024-0089",
                StudentName = "Juan Dela Cruz",
                BookTitle = "To Kill a Mockingbird",
                BookAuthor = "Harper Lee",
                BookIsbn = "9780060935467",
                Date = new DateTime(2025, 5, 16, 10, 15, 0),
                Status = "Pending"
            },
            new RequestDbRecord
            {
                Id = "BR-2025-0050",
                Type = "Borrow",
                StudentId = "2022-0315",
                StudentName = "Angela Reyes",
                BookTitle = "1984",
                BookAuthor = "George Orwell",
                BookIsbn = "9780451524935",
                Date = new DateTime(2025, 5, 17, 13, 20, 0),
                Status = "Pending"
            },
            new RequestDbRecord
            {
                Id = "BR-2025-0051",
                Type = "Borrow",
                StudentId = "2023-0511",
                StudentName = "Mark Bautista",
                BookTitle = "The Great Gatsby",
                BookAuthor = "F. Scott Fitzgerald",
                BookIsbn = "9780743273565",
                Date = new DateTime(2025, 5, 17, 14, 45, 0),
                Status = "Pending"
            },
            new RequestDbRecord
            {
                Id = "BR-2025-0052",
                Type = "Borrow",
                StudentId = "2025-0104",
                StudentName = "Chloe Garcia",
                BookTitle = "Clean Code",
                BookAuthor = "Robert C. Martin",
                BookIsbn = "9780132350884",
                Date = new DateTime(2025, 5, 18, 11, 0, 0),
                Status = "Pending"
            },
            new RequestDbRecord
            {
                Id = "RR-2025-0012",
                Type = "Return",
                StudentId = "2023-0210",
                StudentName = "Carlos Mendoza",
                BookTitle = "Pride and Prejudice",
                BookAuthor = "Jane Austen",
                BookIsbn = "9780141439518",
                Date = new DateTime(2025, 5, 18, 8, 30, 0),
                Status = "Pending"
            },
            new RequestDbRecord
            {
                Id = "RR-2025-0013",
                Type = "Return",
                StudentId = "2024-0418",
                StudentName = "Liza Soberano",
                BookTitle = "Sapiens",
                BookAuthor = "Yuval Noah Harari",
                BookIsbn = "9780062316097",
                Date = new DateTime(2025, 5, 18, 9, 15, 0),
                Status = "Pending"
            }
        };

        SaveRequests(list);
        return list;
    }

    /// <summary>Saves a book cover image to the data/covers folder and returns its file path.
    /// Each save gets a UNIQUE file name (ISBN + timestamp) so a cached or locked old image
    /// is never reused, and the table always picks up the new file. The previous cover file
    /// is deleted so the folder doesn't fill up. The ISBN → path mapping is persisted in
    /// data/covers.json so covers survive restarts.</summary>
    public static string SaveCover(Book book, Image image)
    {
        var safeIsbn = string.Concat((string.IsNullOrWhiteSpace(book.Isbn) ? "book" : book.Isbn)
            .Split(Path.GetInvalidFileNameChars()));
        var fileName = $"cover_{safeIsbn}_{DateTime.Now:yyyyMMddHHmmssfff}.png";
        var filePath = Path.Combine(CoversDir, fileName);

        // Delete the previous cover file
        var oldPath = book.CoverPath;
        if (!string.IsNullOrEmpty(oldPath) && File.Exists(oldPath))
        {
            try { File.Delete(oldPath); }
            catch { /* locked or in use — leave it; the mapping now points to the new file */ }
        }

        image.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);

        // Persist the ISBN → path mapping in the database (covers.json)
        var map = LoadCoverMap();
        // If the ISBN itself was edited, drop the entry that pointed at the old path
        var oldKey = map.FirstOrDefault(kvp => string.Equals(kvp.Value, oldPath, StringComparison.OrdinalIgnoreCase)).Key;
        if (oldKey != null) map.Remove(oldKey);
        map[book.Isbn] = filePath;
        File.WriteAllText(CoversDbPath, JsonSerializer.Serialize(map, new JsonSerializerOptions { WriteIndented = true }));

        return filePath;
    }

    /// <summary>Removes a book's cover: deletes the image file and its database entry.
    /// Called on Save after the user deletes the cover in the Edit Book dialog.</summary>
    public static void DeleteCover(Book book)
    {
        var oldPath = book.CoverPath;
        if (!string.IsNullOrEmpty(oldPath) && File.Exists(oldPath))
        {
            try { File.Delete(oldPath); }
            catch { /* in use — the mapping is removed regardless */ }
        }
        var map = LoadCoverMap();
        if (map.Remove(book.Isbn))
            File.WriteAllText(CoversDbPath, JsonSerializer.Serialize(map, new JsonSerializerOptions { WriteIndented = true }));
        book.CoverPath = "";
    }

    /// <summary>Loads the persisted ISBN → cover-path mappings from data/covers.json.</summary>
    public static Dictionary<string, string> LoadCoverMap()
    {
        try
        {
            if (File.Exists(CoversDbPath))
                return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(CoversDbPath))
                       ?? new Dictionary<string, string>();
        }
        catch { /* corrupted file — start fresh */ }
        return new Dictionary<string, string>();
    }

    public static Image? LoadAvatar(string avatarPath) => LoadImage(avatarPath);

    public static Image? LoadImage(string imagePath)
    {
        if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
            return null;

        try
        {
            // Copy into memory first so the file handle is released immediately —
            // otherwise the image keeps the file locked and it can't be replaced or deleted.
            var bytes = File.ReadAllBytes(imagePath);
            using var ms = new MemoryStream(bytes);
            return Image.FromStream(ms);
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>Validation for student profile fields.</summary>
public static class Validator
{
    public static (bool valid, string error) ValidateFullName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return (false, "Full Name is required");

        var words = name.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 2)
            return (false, "Full Name must have at least 2 words");

        return (true, "");
    }

    public static (bool valid, string error) ValidateStudentId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return (false, "Student ID is required");

        var pattern = @"^\d{8}-[CNScns]$";
        if (!Regex.IsMatch(id, pattern))
            return (false, "Format: 8 digits-letter (e.g., 20251362-C). Letter must be C, N, or S");

        return (true, "");
    }

    public static string NormalizeStudentId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return id;
        var parts = id.Split('-');
        if (parts.Length == 2)
            return $"{parts[0]}-{parts[1].ToUpper()}";
        return id;
    }

    public static (bool valid, string error) ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return (false, "Email is required");

        var pattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
        if (!Regex.IsMatch(email, pattern))
            return (false, "Invalid email format (e.g., email@sample.com)");

        return (true, "");
    }

    public static (bool valid, string error) ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return (false, "Password is required");

        if (password.Length < 4)
            return (false, "Password must be at least 4 characters");

        return (true, "");
    }

    public static (bool valid, string error) ValidateProgram(string program)
    {
        if (string.IsNullOrWhiteSpace(program))
            return (false, "Program is required");

        if (Regex.IsMatch(program, @"\d"))
            return (false, "Program cannot contain numbers");

        return (true, "");
    }

    public static (bool valid, string error) ValidateSection(string section)
    {
        if (string.IsNullOrWhiteSpace(section))
            return (false, "Section is required");

        if (!Regex.IsMatch(section, @"^[A-F]$"))
            return (false, "Section must be A, B, C, D, E, or F");

        return (true, "");
    }

    public static (bool valid, string error) ValidateYear(string year)
    {
        if (string.IsNullOrWhiteSpace(year))
            return (false, "Year is required");

        var digit = year.Trim().Length > 0 && char.IsDigit(year.Trim()[0]) ? year.Trim()[0].ToString() : year.Trim();
        if (!new[] { "1", "2", "3", "4" }.Contains(digit))
            return (false, "Year must be 1, 2, 3, or 4");

        return (true, "");
    }

    public static (bool valid, string error) ValidateContactNumber(string contact)
    {
        if (string.IsNullOrWhiteSpace(contact))
            return (false, "Contact Number is required");

        if (!Regex.IsMatch(contact, @"^09\d{9}$"))
            return (false, "Must be exactly 11 digits starting with 09 (e.g., 09123456789)");

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
