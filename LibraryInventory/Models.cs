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
    /// <summary>Student assistant — gets the admin workspace. Normal students are false.</summary>
    public bool IsSa { get; set; } = false;
    public DateTime Joined { get; set; } = DateTime.Today;

    /// <summary>True once the student has filled in program, year, section and contact (sign-up leaves them as "-").</summary>
    public bool ProfileComplete =>
        new[] { Program, Year, Section, Contact }.All(v => !string.IsNullOrWhiteSpace(v) && v.Trim() != "-");

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

    public static event Action ProfileChanged;
    public static void RaiseProfileChanged() => ProfileChanged?.Invoke();

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
            // Reuse the pending loan created when the student submitted the request
            // (it survives reloads now that loans are persisted).
            r.Loan ??= Loans.FirstOrDefault(l => l.Book == r.Book && l.StudentId == (r.Student?.Id ?? "") && l.Status != "RETURN");
            r.Loan ??= new Loan { Book = r.Book, StudentId = r.Student?.Id ?? "" };
            r.Loan.Status = "RETURN";   // approved, currently borrowed
            r.Loan.Borrowed = DateTime.Today;
            r.Loan.Due = DateTime.Today.AddDays(7);
            if (!Loans.Contains(r.Loan)) Loans.Add(r.Loan);
            r.Book.Out++;

            // Approval is visible in history for both the admin and the requesting student.
            History.Insert(0, new HistoryEntry
            {
                Book = r.Book,
                StudentId = r.Student?.Id ?? "",
                Borrowed = DateTime.Today,
                Returned = DateTime.Today,
                Remark = "Approved"
            });
        }
        else if (r.Type == "Return" && r.Book != null)
        {
            var loan = r.Loan ?? Loans.FirstOrDefault(l => l.Book == r.Book && l.StudentId == r.Student?.Id);
            if (loan != null) Return(loan);
        }
        SyncState();
        return true;
    }

    public static void Decline(BookRequest r)
    {
        r.Status = "Rejected";
        if (r.Type == "Borrow")
        {
            // The loan link may be missing after a reload — find the pending loan instead.
            var loan = r.Loan ?? Loans.FirstOrDefault(l => l.Book == r.Book && l.StudentId == (r.Student?.Id ?? "") && l.Status == "PENDING");
            if (loan != null) Loans.Remove(loan);
        }
        SyncState();
    }

    /// <summary>Persists every state that the borrow/return flow touches, in dependency order.</summary>
    public static void SyncState()
    {
        // Books first: rewriting the books table changes book ids, and
        // requests/loans/history rows reference books by id.
        Database.SaveBooks(Books);
        SaveRequestsToDb();
        Database.SaveLoans(Loans);
        Database.SaveHistory(History);
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
        // Update the pending "Approved" history row (if any) instead of adding a duplicate.
        var open = History.FirstOrDefault(h =>
            h.Remark == "Approved" && h.StudentId == l.StudentId &&
            h.Book != null && l.Book != null &&
            (h.Book.Isbn == l.Book.Isbn || h.Book.Title == l.Book?.Title));
        if (open != null)
        {
            open.Returned = DateTime.Today;
            open.Remark = late ? "Late" : "On time";
        }
        else
        {
            History.Insert(0, new HistoryEntry
            {
                Book = l.Book, StudentId = l.StudentId, Borrowed = l.Borrowed,
                Returned = DateTime.Today, Remark = late ? "Late" : "On time"
            });
        }
        Loans.Remove(l);
        l.Book.Out = Math.Max(0, l.Book.Out - 1);
        SyncState();
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
        SyncState();
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

        // Load books from database
        Books.Clear();
        Books.AddRange(Database.LoadBooks());

        // Re-link request book references to the freshly loaded book objects
        // (they pointed at temporary objects from before/books that were replaced above).
        var byIsbn = Books.Where(b => !string.IsNullOrEmpty(b.Isbn))
                          .GroupBy(b => b.Isbn).ToDictionary(g => g.Key, g => g.First());
        foreach (var r in Requests)
        {
            if (r.Book == null) continue;
            var match = !string.IsNullOrEmpty(r.Book.Isbn) && byIsbn.TryGetValue(r.Book.Isbn, out var m1) ? m1
                      : Books.FirstOrDefault(b => b.Title.Equals(r.Book.Title, StringComparison.OrdinalIgnoreCase));
            if (match != null) r.Book = match;
        }

        // Restore active loans (so approved requests show in the student's Manage tab) and history.
        foreach (var l in Database.LoadLoans(byIsbn))
            if (!Loans.Any(x => x.Book == l.Book && x.StudentId == l.StudentId && x.Status == l.Status))
                Loans.Add(l);
        History.AddRange(Database.LoadHistory(byIsbn));

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

/// <summary>Library administrator account.</summary>
public class Admin
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}
