namespace LibraryInventory;

public class Book
{
    public int Id { get; set; }
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
    /// <summary>Plain text only in memory (never persisted); empty after a bulk load.</summary>
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

    // Legacy compatibility
    public string Course { get => Program; set => Program = value; }
}

public class Loan
{
    public int Id { get; set; }
    public Book Book { get; set; }
    public string StudentId { get; set; } = "";
    public DateTime Borrowed { get; set; }
    public DateTime Due { get; set; }
    public string Status { get; set; } = "PENDING";   // RETURN, PENDING, OVERDUE
}

public class HistoryEntry
{
    public int Id { get; set; }
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

/// <summary>In-memory view of the database plus the borrowing business rules.</summary>
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

    static bool _initialized;

    /// <summary>
    /// The exception that stopped <see cref="Initialize"/> from loading the data,
    /// or null when initialisation succeeded.
    /// </summary>
    public static Exception InitError { get; private set; }

    /// <summary>
    /// Loads all database-backed state exactly once. Returns false and stores the
    /// failure in <see cref="InitError"/> instead of throwing from a static
    /// constructor (which surfaces as a useless TypeInitializationException).
    /// Calling it again after a failure retries the whole load.
    /// </summary>
    public static bool Initialize()
    {
        if (_initialized) return true;

        try
        {
            InitializeCore();
            _initialized = true;
            InitError = null;
            return true;
        }
        catch (Exception ex)
        {
            InitError = ex;
            return false;
        }
    }

    static void InitializeCore()
    {
        // Start clean so a retry after a failure never duplicates rows.
        Books.Clear();
        Students.Clear();
        Loans.Clear();
        History.Clear();
        Requests.Clear();
        Cart.Clear();
        Me = null;

        // Connects and verifies that database/00_install.sql has been run.
        Database.Initialize();

        Books.AddRange(Database.LoadBooks());
        Students.AddRange(Database.LoadStudents());

        var byIsbn = Books.Where(b => !string.IsNullOrEmpty(b.Isbn))
                          .GroupBy(b => b.Isbn).ToDictionary(g => g.Key, g => g.First());

        foreach (var rec in Database.LoadRequests())
        {
            var student = Students.FirstOrDefault(s => s.Id == rec.StudentId);
            if (student == null) continue;
            if (!byIsbn.TryGetValue(rec.BookIsbn, out var book)) continue;

            Requests.Add(new BookRequest
            {
                Id = rec.Id,
                Type = string.IsNullOrEmpty(rec.Type) ? "Borrow" : rec.Type,
                Student = student,
                Book = book,
                Date = rec.Date,
                Status = rec.Status
            });
        }

        Loans.AddRange(Database.LoadLoans(byIsbn));
        History.AddRange(Database.LoadHistory(byIsbn));

        // Point pending requests at the loan created when they were submitted.
        foreach (var r in Requests.Where(r => r.Type == "Borrow" && r.Status == "Pending"))
            r.Loan = Loans.FirstOrDefault(l => l.Book == r.Book &&
                                               l.StudentId == (r.Student?.Id ?? "") &&
                                               l.Status == "PENDING");

        Me = Students.FirstOrDefault();
        RefreshUserFlags();
    }

    /// <summary>Switches the signed-in student and restores their persisted cart.</summary>
    public static void SignIn(Student student)
    {
        Me = student;
        if (student != null)
        {
            int index = Students.FindIndex(s => s.Id == student.Id);
            if (index >= 0) Students[index] = student;
            else Students.Add(student);

            foreach (var r in Requests)
                if (r.Student?.Id == student.Id) r.Student = student;
        }
        RefreshUserFlags();
        LoadCart();
    }

    public static void SignOut()
    {
        Me = null;
        UserName = "user";
        Suspended = false;
        Cart.Clear();
        RaiseCartChanged();
    }

    static void RefreshUserFlags()
    {
        if (Me == null)
        {
            UserName = "user";
            Suspended = false;
            return;
        }

        UserName = string.IsNullOrWhiteSpace(Me.Name) ? Me.Id : Me.Name.Split(' ')[0];
        Suspended = !Me.Active;
    }

    /// <summary>Reloads the cart for the current student from cart_items.</summary>
    public static void LoadCart()
    {
        Cart.Clear();
        if (Me != null)
        {
            foreach (var isbn in Database.LoadCartIsbns(Me.Id))
            {
                var book = Books.FirstOrDefault(b => b.Isbn == isbn);
                if (book != null && !Cart.Contains(book)) Cart.Add(book);
            }
        }
        RaiseCartChanged();
    }

    public static void AddToCart(Book book)
    {
        if (book == null || Me == null) return;
        Database.AddToCart(Me.Id, book);
        if (!Cart.Contains(book)) Cart.Add(book);
        RaiseCartChanged();
    }

    public static void RemoveFromCart(Book book)
    {
        if (book == null) return;
        if (Me != null) Database.RemoveFromCart(Me.Id, book);
        Cart.Remove(book);
        RaiseCartChanged();
    }

    public static bool Approve(BookRequest r)
    {
        if (r.Book != null && !r.Book.Available && r.Type == "Borrow") return false;
        r.Status = "Approved";
        if (r.Type == "Borrow" && r.Book != null)
        {
            // Reuse the pending loan created when the student submitted the request.
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

    /// <summary>Persists the pieces that the borrow/return flow touches, in dependency order.</summary>
    public static void SyncState()
    {
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
            BookIsbn = r.Book?.Isbn ?? "",
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
            (h.Book.Isbn == l.Book.Isbn || h.Book.Title == l.Book.Title));
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
        if (l.Book != null) l.Book.Out = Math.Max(0, l.Book.Out - 1);
        SyncState();
    }

    public static void SubmitCart(IEnumerable<Book> books)
    {
        if (Me == null) return;

        foreach (var b in books.ToList())
        {
            var loan = new Loan { Book = b, StudentId = Me.Id, Status = "PENDING", Borrowed = DateTime.Today, Due = DateTime.Today.AddDays(7) };
            Loans.Add(loan);
            Requests.Add(new BookRequest
            {
                Id = NextRequestId(),
                Type = "Borrow",
                Book = b,
                Student = Me,
                Date = DateTime.Now,
                Status = "Pending",
                Loan = loan
            });
            Cart.Remove(b);
            Database.RemoveFromCart(Me.Id, b);
        }
        SyncState();
        RaiseCartChanged();
    }

    static string NextRequestId()
    {
        int year = DateTime.Today.Year;
        int next = 1;
        foreach (var r in Requests)
        {
            var parts = (r.Id ?? "").Split('-');
            if (parts.Length >= 3 && parts[1] == year.ToString() && int.TryParse(parts[2], out int n))
                next = Math.Max(next, n + 1);
        }
        return $"BR-{year}-{next:D4}";
    }
}

/// <summary>Library administrator account.</summary>
public class Admin
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    /// <summary>Never populated: passwords live only as BCrypt hashes in the database.</summary>
    public string Password { get; set; } = "";
}
