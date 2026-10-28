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
    public string Id { get; set; } = "";
    public string Type { get; set; } = "Borrow";   // Borrow or Return
    public Book Book { get; set; }
    public Student Student { get; set; }
    public DateTime Date { get; set; }
    public string Status { get; set; } = "Pending";   // Pending, Approved, Rejected / Declined
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

    public static int PendingBorrowCount => Requests.Count(r => r.Type == "Borrow" && r.Status == "Pending");
    public static int PendingReturnCount => Requests.Count(r => r.Type == "Return" && r.Status == "Pending");
    public static int PendingCount => PendingBorrowCount;

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
        if (r.Book != null && !r.Book.Available && r.Type == "Borrow") return false;
        r.Status = "Approved";
        if (r.Type == "Borrow" && r.Book != null)
        {
            r.Loan ??= new Loan { Book = r.Book, StudentId = r.Student?.Id ?? "" };
            r.Loan.Status = "RETURN";
            r.Loan.Borrowed = DateTime.Today;
            r.Loan.Due = DateTime.Today.AddDays(7);
            if (!Loans.Contains(r.Loan)) Loans.Add(r.Loan);
            r.Book.Out++;
        }
        else if (r.Type == "Return" && r.Book != null)
        {
            var loan = r.Loan ?? Loans.FirstOrDefault(l => l.Book == r.Book && l.StudentId == r.Student?.Id);
            if (loan != null) Return(loan);
        }
        SaveRequestsToDb();
        return true;
    }

    public static void Decline(BookRequest r)
    {
        r.Status = "Rejected";
        if (r.Loan != null && r.Type == "Borrow") Loans.Remove(r.Loan);
        SaveRequestsToDb();
    }

    public static void SaveRequestsToDb()
    {
        var records = Requests.Select(r => new RequestDbRecord
        {
            Id = r.Id,
            Type = r.Type,
            StudentId = r.Student?.Id ?? "",
            StudentName = r.Student?.Name ?? "",
            BookTitle = r.Book?.Title ?? "",
            BookAuthor = r.Book?.Author ?? "",
            BookIsbn = r.Book?.Isbn ?? "",
            BookCoverPath = r.Book?.CoverPath ?? "",
            Date = r.Date,
            Status = r.Status
        }).ToList();
        Database.SaveRequests(records);
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
        int nextNum = Requests.Count + 1;
        foreach (var b in books.ToList())
        {
            var loan = new Loan { Book = b, StudentId = Me.Id, Status = "PENDING", Borrowed = DateTime.Today, Due = DateTime.Today.AddDays(7) };
            Loans.Add(loan);
            Requests.Add(new BookRequest
            {
                Id = $"BR-{DateTime.Today.Year}-{nextNum++:D4}",
                Type = "Borrow",
                Book = b,
                Student = Me,
                Date = DateTime.Now,
                Status = "Pending",
                Loan = loan
            });
            Cart.Remove(b);
        }
        SaveRequestsToDb();
        RaiseCartChanged();
    }

    static AppData()
    {
        // Load students from database
        var dbStudents = Database.LoadStudents();
        Students.AddRange(dbStudents);

        // Load requests from database
        var dbRequests = Database.LoadRequests();
        foreach (var rec in dbRequests)
        {
            var st = Students.FirstOrDefault(s => s.Id == rec.StudentId);
            if (st == null)
            {
                st = new Student
                {
                    Id = rec.StudentId,
                    Name = rec.StudentName,
                    Email = $"{rec.StudentId.ToLowerInvariant().Replace("-", "")}@library.edu",
                    Year = "2nd Year",
                    Course = "BS Computer Science"
                };
                Students.Add(st);
            }

            var bk = Books.FirstOrDefault(b => b.Title.Equals(rec.BookTitle, StringComparison.OrdinalIgnoreCase) ||
                                               (!string.IsNullOrEmpty(rec.BookIsbn) && b.Isbn == rec.BookIsbn));
            if (bk == null)
            {
                bk = new Book
                {
                    Title = rec.BookTitle,
                    Author = rec.BookAuthor,
                    Isbn = rec.BookIsbn,
                    CoverPath = rec.BookCoverPath,
                    CoverColor = Color.FromArgb(0x8A, 0x6E, 0x50),
                    Qty = 8,
                    Out = 2
                };
                Books.Add(bk);
            }

            var req = new BookRequest
            {
                Id = rec.Id,
                Type = string.IsNullOrEmpty(rec.Type) ? "Borrow" : rec.Type,
                Student = st,
                Book = bk,
                Date = rec.Date,
                Status = rec.Status
            };
            Requests.Add(req);
        }

        // Re-attach saved book covers (ISBN → file path, persisted in data/covers.json)
        foreach (var (isbn, path) in Database.LoadCoverMap())
        {
            var b = Books.FirstOrDefault(x => x.Isbn == isbn);
            if (b != null && File.Exists(path)) b.CoverPath = path;
        }

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
