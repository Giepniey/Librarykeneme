namespace LibraryInventory;

public class Book
{
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public string Isbn { get; set; } = "";
    public string Category { get; set; } = "FIC";   // CIR, FIC, FIL, REF
    public int Qty { get; set; } = 10;
    public int Out { get; set; }
    public Color CoverColor { get; set; } = Color.SteelBlue;
    public string CoverPath { get; set; } = "";     // uploaded cover image (data/covers folder)
    public int Copies => Qty - Out;
    public bool Available => Copies > 0;
}

public class Student
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string Program { get; set; } = "";
    public string Section { get; set; } = "";
    public string Year { get; set; } = "";
    public string Contact { get; set; } = "";
    public string AvatarPath { get; set; } = "";
    public bool Active { get; set; } = true;
    public DateTime Joined { get; set; } = DateTime.Today;

    // Legacy compatibility
    public string Course { get => Program; set => Program = value; }
}

public class Loan
{
    public Book Book { get; set; }
    public string StudentId { get; set; } = "";
    public DateTime Borrowed { get; set; }
    public DateTime Due { get; set; }
    public string Status { get; set; } = "PENDING";   // RETURN, PENDING, OVERDUE
}

public class HistoryEntry
{
    public Book Book { get; set; }
    public string StudentId { get; set; } = "";
    public DateTime Borrowed { get; set; }
    public DateTime Returned { get; set; }
    public string Remark { get; set; } = "On time";
}

public class BookRequest
{
    public Book Book { get; set; }
    public Student Student { get; set; }
    public DateTime Date { get; set; }
    public string Status { get; set; } = "Pending";   // Pending, Approved, Declined
    public Loan Loan { get; set; }
}

/// <summary>In-memory sample data and tiny bits of business logic (no database).</summary>
public static class AppData
{
    public static readonly List<Book> Books = new();
    public static readonly List<Student> Students = new();
    public static readonly List<Loan> Loans = new();
    public static readonly List<HistoryEntry> History = new();
    public static readonly List<BookRequest> Requests = new();
    public static readonly List<Book> Cart = new();

    public static Student Me;
    public static string UserName = "user";
    public static bool Suspended;

    public static event Action CartChanged;
    public static void RaiseCartChanged() => CartChanged?.Invoke();

    public static int PendingCount => Requests.Count(r => r.Status == "Pending");

    public static string Initials(string name)
    {
        name = (name ?? "").Trim();
        return name.Length == 0 ? "?" : name.Substring(0, Math.Min(2, name.Length)).ToUpperInvariant();
    }

    public static string CategoryName(string c) => c switch
    {
        "FIC" => "FICTION",
        "CIR" => "CIRCULATION",
        "FIL" => "FILIPINO",
        "REF" => "REFERENCE",
        _ => c
    };

    public static bool Approve(BookRequest r)
    {
        if (!r.Book.Available) return false;
        r.Status = "Approved";
        r.Loan ??= new Loan { Book = r.Book, StudentId = r.Student.Id };
        r.Loan.Status = "RETURN";
        r.Loan.Borrowed = DateTime.Today;
        r.Loan.Due = DateTime.Today.AddDays(7);
        if (!Loans.Contains(r.Loan)) Loans.Add(r.Loan);
        r.Book.Out++;
        return true;
    }

    public static void Decline(BookRequest r)
    {
        r.Status = "Declined";
        if (r.Loan != null) Loans.Remove(r.Loan);
    }

    public static void Return(Loan l)
    {
        bool late = DateTime.Today > l.Due;
        History.Insert(0, new HistoryEntry
        {
            Book = l.Book, StudentId = l.StudentId, Borrowed = l.Borrowed,
            Returned = DateTime.Today, Remark = late ? "Late" : "On time"
        });
        Loans.Remove(l);
        l.Book.Out = Math.Max(0, l.Book.Out - 1);
    }

    public static void SubmitCart(IEnumerable<Book> books)
    {
        foreach (var b in books.ToList())
        {
            var loan = new Loan { Book = b, StudentId = Me.Id, Status = "PENDING", Borrowed = DateTime.Today, Due = DateTime.Today.AddDays(7) };
            Loans.Add(loan);
            Requests.Add(new BookRequest { Book = b, Student = Me, Date = DateTime.Today, Status = "Pending", Loan = loan });
            Cart.Remove(b);
        }
        RaiseCartChanged();
    }

    static AppData()
    {
        // Load students from database
        var dbStudents = Database.LoadStudents();
        Students.AddRange(dbStudents);

        // If no students exist, create a sample one
        if (Students.Count == 0)
        {
            Me = new Student
            {
                Id = "20251362-C",
                Name = "Sample Student",
                Email = "student@library.edu",
                Password = "password123",
                Program = "BS Computer Science",
                Year = "1",
                Section = "A",
                Contact = "09123456789",
                Joined = DateTime.Today
            };
            Students.Add(Me);
            Database.SaveStudent(Me);
        }
        else
        {
            Me = Students[0];
        }
    }
}
