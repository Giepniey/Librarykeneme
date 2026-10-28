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

    /// <summary>Saves a book cover image to the data/covers folder and returns its file path.
    /// If the book already had a cover file, the old one is deleted so the folder doesn't fill up.</summary>
    public static string SaveCover(Book book, Image image)
    {
        var fileName = $"cover_{book.Isbn}.png";
        var filePath = Path.Combine(CoversDir, fileName);

        // Delete the previous cover (keep the raw path in a temp var so two books sharing one file is safe)
        var oldPath = book.CoverPath;
        if (!string.IsNullOrEmpty(oldPath) && File.Exists(oldPath) &&
            !string.Equals(oldPath, filePath, StringComparison.OrdinalIgnoreCase))
            File.Delete(oldPath);

        image.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
        return filePath;
    }

    public static Image? LoadAvatar(string avatarPath) => LoadImage(avatarPath);

    public static Image? LoadImage(string imagePath)
    {
        if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
            return null;

        try
        {
            return Image.FromFile(imagePath);
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

        if (!new[] { "1", "2", "3", "4" }.Contains(year))
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
