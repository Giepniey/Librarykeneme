namespace LibraryInventory;

public class AdminDashboardPage : PageBase
{
    readonly Shell shell;

    DataGridView recentGrid;

    public AdminDashboardPage(Shell shell) : base("Admin Dashboard", "Overview of library activities")
    {
        this.shell = shell;

        // recent requests (fills the remaining space)
        var recent = new RoundedPanel { Dock = DockStyle.Fill, Padding = new Padding(10, 30, 10, 10), Fill = Color.FromArgb(0xF3, 0xEC, 0xDD) };
        var rt = Theme.Lbl("Recent Requests", 11.5f, FontStyle.Bold);
        rt.Location = new Point(10, 6);
        recentGrid = Tables.Create(30, true);
        recentGrid.Dock = DockStyle.Fill;
        recentGrid.Columns.Add(Tables.Text("title", "Title", 110));
        recentGrid.Columns.Add(Tables.Text("author", "Author", 80));
        recentGrid.Columns.Add(Tables.Text("student", "Student", 90));
        recentGrid.Columns.Add(Tables.Text("date", "Date", 70));
        // View-only status label: Approved (green) / Declined (red) / Pending (neutral).
        // Approving and declining is done on the Requests page.
        recentGrid.Columns.Add(Tables.Chip("action", "Action", 60));

        LoadRecentRequests();

        Tables.Fit(recentGrid);
        Tables.Done(recentGrid);
        recent.Controls.Add(recentGrid);
        recent.Controls.Add(rt);
        rt.BringToFront();

        var stats = Ui.StatRow(
            new StatCard("📖", "Total Books", AppData.Books.Sum(b => b.Qty).ToString("N0"), Theme.Ink),
            new StatCard("👥", "Registered Students", AppData.Students.Count.ToString("N0"), Theme.Ink),
            new StatCard("📋", "Books Borrowed", AppData.Books.Sum(b => b.Out).ToString("N0"), Theme.Ink),
            new StatCard("🕘", "Pending Requests", AppData.PendingCount.ToString("N0"), Theme.Ink));

        var search = new SearchBox("Search books by title, author, ....");

        Body.Controls.Add(recent);
        Body.Controls.Add(stats);
        Body.Controls.Add(Ui.SearchWrap(search));
    }

    void LoadRecentRequests()
    {
        recentGrid.Rows.Clear();
        foreach (var r in AppData.Requests.Take(4))
        {
            int i = recentGrid.Rows.Add(r.Book.Title, r.Book.Author, r.Student.Name, r.Date.ToString("MM/dd/yyyy"), r.Status);
            recentGrid.Rows[i].Tag = r;
        }
    }
}

public class ManageBooksPage : PageBase
{
    readonly DataGridView g = Tables.Create(54);
    readonly SearchBox search = new SearchBox("Search books by title, author, ....");
    readonly ComboBox cat = Ui.Combo("All categories", "CIR", "FIC", "FIL", "REF");
    readonly ComboBox status = Ui.Combo("All statuses", "Available", "Borrowed");
    readonly Label title = Theme.Lbl("Books", 11.5f, FontStyle.Bold);
    readonly Label info = Theme.Lbl("", 8.5f);
    readonly StatCard sTotal = new StatCard("📖", "Total Books", "0", Theme.Ink);
    readonly StatCard sAvail = new StatCard("✔", "Available books", "0", Color.FromArgb(0x2E, 0x7D, 0x3A));
    readonly StatCard sOut = new StatCard("⟳", "Borrowed Books", "0", Theme.Ink);
    readonly StatCard sOver = new StatCard("⚠", "Overdue", "0", Theme.Ink);

    public ManageBooksPage(Shell shell) : base("Manage Books", "Add, edit, and manage books in the library")
    {
        var add = Theme.Btn("+ Add New Book", Theme.Brown, Color.White, 150, 36, 10);
        Header.Controls.Add(add);
        Header.Resize += (s, e) => add.Location = new Point(Header.Width - add.Width, 6);
        add.Click += (s, e) => AddBook();

        var imgCol = new DataGridViewImageColumn { Name = "cover", HeaderText = "Cover", FillWeight = 40, ImageLayout = DataGridViewImageCellLayout.Zoom, SortMode = DataGridViewColumnSortMode.NotSortable };
        g.Columns.Add(imgCol);
        g.Columns.Add(Tables.Text("title", "Title", 120));
        g.Columns.Add(Tables.Text("author", "Author", 90));
        g.Columns.Add(Tables.Text("isbn", "ISBN", 90));
        g.Columns.Add(Tables.Chip("status", "Status", 80));
        g.Columns.Add(Tables.Text("qty", "Qtty", 40));
        g.Columns.Add(Tables.Btn("edit", "Action", "✎", Theme.Card, Theme.Ink, 30, 10));
        g.Columns.Add(Tables.Btn("del", "", "🗑", Theme.Card, Theme.Ink, 30, 10));
        g.CellContentClick += (s, e) =>
        {
            if (e.RowIndex < 0) return;
            var b = g.Rows[e.RowIndex].Tag as Book;
            string col = g.Columns[e.ColumnIndex].Name;
            if (b == null) return;
            if (col == "edit") EditBook(b);
            else if (col == "del" && MessageBox.Show($"Delete \"{b.Title}\"?", "Delete book", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                AppData.Books.Remove(b);
                Reload();
            }
        };

        var card = new RoundedPanel { Dock = DockStyle.Fill, Padding = new Padding(10), Fill = Color.FromArgb(0xF3, 0xEC, 0xDD) };
        title.Dock = DockStyle.Top;
        title.AutoSize = false;
        title.Height = 28;
        g.Dock = DockStyle.Fill;
        card.Controls.Add(g);
        card.Controls.Add(Ui.Pager(info));
        card.Controls.Add(title);

        search.Box.TextChanged += (s, e) => Reload();
        cat.SelectedIndexChanged += (s, e) => Reload();
        status.SelectedIndexChanged += (s, e) => Reload();

        Body.Controls.Add(card);
        Body.Controls.Add(Ui.StatRow(sTotal, sAvail, sOut, sOver));
        Body.Controls.Add(Ui.FilterRow(search, cat, status));
        Reload();
    }

    void Reload()
    {
        g.Rows.Clear();
        string q = search.Box.Text.Trim().ToLowerInvariant();
        var shown = AppData.Books.Where(b =>
            (cat.SelectedIndex == 0 || b.Category == (string)cat.SelectedItem) &&
            (status.SelectedIndex == 0 || (status.SelectedIndex == 1) == b.Available) &&
            (q.Length == 0 || (b.Title + " " + b.Author + " " + b.Isbn).ToLowerInvariant().Contains(q))).ToList();
        foreach (var b in shown)
        {
            int i = g.Rows.Add(CoverFactory.Make(b, 32, 44), b.Title, b.Author, b.Isbn, b.Available ? $"Available ({b.Copies})" : "Borrowed", b.Qty);
            g.Rows[i].Tag = b;
        }
        Tables.Done(g);
        title.Text = $"Books ({AppData.Books.Count:N0})";
        info.Text = $"Showing 1-{shown.Count} from {AppData.Books.Count:N0} records";
        sTotal.Value = AppData.Books.Sum(b => b.Qty).ToString("N0");
        sAvail.Value = AppData.Books.Sum(b => b.Copies).ToString("N0");
        sOut.Value = AppData.Books.Sum(b => b.Out).ToString("N0");
        sOver.Value = AppData.Loans.Count(l => l.Status == "OVERDUE").ToString();
    }

    static readonly string[] Cats = { "CIR", "FIC", "FIL", "REF" };

    void AddBook()
    {
        var v = PromptDialog.Show(FindForm(), "Add New Book", out var zone, "",
            new Field("Title", ""), new Field("Author", ""), new Field("ISBN", ""), new Field("Category", "FIC", Cats), new Field("Quantity", "1"));
        if (v == null) return;
        if (string.IsNullOrWhiteSpace(v[0]) || !int.TryParse(v[4], out int qty) || qty < 1) { MessageBox.Show("Please enter a title and a valid quantity.", "Add book"); return; }
        var b = new Book { Title = v[0].Trim(), Author = v[1].Trim(), Isbn = v[2].Trim(), Category = v[3], Qty = qty, CoverColor = Color.FromArgb(0x8A, 0x6E, 0x50) };
        if (zone?.CoverImage != null)
            b.CoverPath = Database.SaveCover(b, zone.CoverImage);
        AppData.Books.Add(b);
        Reload();
    }

    void EditBook(Book b)
    {
        var v = PromptDialog.Show(FindForm(), "Edit Book", out var zone, b.CoverPath,
            new Field("Title", b.Title), new Field("Author", b.Author), new Field("ISBN", b.Isbn), new Field("Category", b.Category, Cats), new Field("Quantity", b.Qty.ToString()));
        if (v == null) return;
        if (string.IsNullOrWhiteSpace(v[0]) || !int.TryParse(v[4], out int qty) || qty < b.Out) { MessageBox.Show("Please enter a title and a quantity that covers the borrowed copies.", "Edit book"); return; }
        b.Title = v[0].Trim(); b.Author = v[1].Trim(); b.Isbn = v[2].Trim(); b.Category = v[3]; b.Qty = qty;
        if (zone?.Changed == true && zone.CoverImage != null)
            b.CoverPath = Database.SaveCover(b, zone.CoverImage);
        Reload();
    }
}

public class ManageStudentsPage : PageBase
{
    readonly DataGridView g = Tables.Create(34);
    readonly SearchBox search = new SearchBox("Search names");
    readonly ComboBox course = Ui.Combo("All Courses", "BS Computer Science", "BS Information Technology", "BS Psychology");
    readonly ComboBox year = Ui.Combo("All Year Levels", "1st Year", "2nd Year", "3rd Year", "4th Year");
    readonly ComboBox status = Ui.Combo("All Statuses", "Active", "Inactive");
    readonly Label info = Theme.Lbl("", 8.5f);
    readonly StatCard sTotal = new StatCard("👥", "Total Students", "0", Theme.Ink);
    readonly StatCard sActive = new StatCard("✔", "Active Students", "0", Color.FromArgb(0x2E, 0x9E, 0x3A));
    readonly StatCard sInactive = new StatCard("✖", "Inactive Students", "0", Color.FromArgb(0xD0, 0x20, 0x20));
    readonly StatCard sNew = new StatCard("＋", "New Registrations", "0", Color.FromArgb(0xE0, 0x8A, 0x00));

    public ManageStudentsPage() : base("Manage Student", "View, add, edit, and manage accounts")
    {
        var add = Theme.Btn("+ Add New Student", Theme.Brown, Color.White, 170, 36, 10);
        Header.Controls.Add(add);
        Header.Resize += (s, e) => add.Location = new Point(Header.Width - add.Width, 6);
        add.Click += (s, e) => Edit(null);

        g.Columns.Add(Tables.Text("id", "Student ID", 70));
        g.Columns.Add(Tables.Text("name", "Full Name", 100));
        g.Columns.Add(Tables.Text("email", "Email Address", 110));
        g.Columns.Add(Tables.Text("course", "Course", 100));
        g.Columns.Add(Tables.Text("year", "Year Level", 60));
        g.Columns.Add(Tables.Chip("status", "Status", 60));
        g.Columns.Add(Tables.Btn("view", "Actions", "👁", Theme.Card, Theme.Ink, 30, 10));
        g.Columns.Add(Tables.Btn("edit", "", "✎", Theme.Card, Theme.Ink, 30, 10));
        g.Columns.Add(Tables.Btn("del", "", "🗑", Theme.Card, Theme.Ink, 30, 10));
        g.CellContentClick += (s, e) =>
        {
            if (e.RowIndex < 0) return;
            var st = g.Rows[e.RowIndex].Tag as Student;
            if (st == null) return;
            switch (g.Columns[e.ColumnIndex].Name)
            {
                case "view":
                    MessageBox.Show($"{st.Name}\nID: {st.Id}\nEmail: {st.Email}\n{st.Course} - {st.Year}\nStatus: {(st.Active ? "Active" : "Inactive")}", "Student");
                    break;
                case "edit": Edit(st); break;
                case "del":
                    if (st == AppData.Me) { MessageBox.Show("The signed-in sample student cannot be deleted.", "Delete student"); break; }
                    if (MessageBox.Show($"Delete {st.Name}?", "Delete student", MessageBoxButtons.YesNo) == DialogResult.Yes) { AppData.Students.Remove(st); Reload(); }
                    break;
            }
        };

        var card = new RoundedPanel { Dock = DockStyle.Fill, Padding = new Padding(10), Fill = Color.FromArgb(0xF3, 0xEC, 0xDD) };
        var ct = Theme.Lbl("📄  Student List", 11.5f, FontStyle.Bold);
        ct.Dock = DockStyle.Top;
        ct.AutoSize = false;
        ct.Height = 30;
        g.Dock = DockStyle.Fill;
        card.Controls.Add(g);
        card.Controls.Add(Ui.Pager(info));
        card.Controls.Add(ct);

        search.Box.TextChanged += (s, e) => Reload();
        course.SelectedIndexChanged += (s, e) => Reload();
        year.SelectedIndexChanged += (s, e) => Reload();
        status.SelectedIndexChanged += (s, e) => Reload();

        Body.Controls.Add(card);
        Body.Controls.Add(Ui.StatRow(sTotal, sActive, sInactive, sNew));
        Body.Controls.Add(Ui.FilterRow(search, course, year, status));
        Reload();
    }

    void Reload()
    {
        g.Rows.Clear();
        string q = search.Box.Text.Trim().ToLowerInvariant();
        foreach (var s in AppData.Students)
        {
            if (course.SelectedIndex > 0 && s.Course != (string)course.SelectedItem) continue;
            if (year.SelectedIndex > 0 && s.Year != (string)year.SelectedItem) continue;
            if (status.SelectedIndex > 0 && s.Active != (status.SelectedIndex == 1)) continue;
            if (q.Length > 0 && !(s.Name + " " + s.Id + " " + s.Email).ToLowerInvariant().Contains(q)) continue;
            int i = g.Rows.Add(s.Id, s.Name, s.Email, s.Course, s.Year, s.Active ? "Active" : "Inactive");
            g.Rows[i].Tag = s;
        }
        Tables.Done(g);
        info.Text = $"Showing {g.Rows.Count} of {AppData.Students.Count} students";
        sTotal.Value = AppData.Students.Count.ToString();
        sActive.Value = AppData.Students.Count(x => x.Active).ToString();
        sInactive.Value = AppData.Students.Count(x => !x.Active).ToString();
        sNew.Value = AppData.Students.Count(x => x.Joined.Year == DateTime.Today.Year && x.Joined.Month == DateTime.Today.Month).ToString();
    }

    void Edit(Student st)
    {
        bool isNew = st == null;
        var v = PromptDialog.Show(FindForm(), isNew ? "Add New Student" : "Edit Student",
            new Field("Student ID", st?.Id ?? ""),
            new Field("Full Name", st?.Name ?? ""),
            new Field("Email Address", st?.Email ?? ""),
            new Field("Course", st?.Course ?? "BS Computer Science", new[] { "BS Computer Science", "BS Information Technology", "BS Psychology", "BS Engineering" }),
            new Field("Year Level", st?.Year ?? "1st Year", new[] { "1st Year", "2nd Year", "3rd Year", "4th Year" }),
            new Field("Status", st == null || st.Active ? "Active" : "Inactive", new[] { "Active", "Inactive" }));
        if (v == null) return;
        if (string.IsNullOrWhiteSpace(v[0]) || string.IsNullOrWhiteSpace(v[1])) { MessageBox.Show("Student ID and full name are required.", "Student"); return; }
        st ??= new Student { Joined = DateTime.Today };
        st.Id = v[0].Trim(); st.Name = v[1].Trim(); st.Email = v[2].Trim(); st.Course = v[3]; st.Year = v[4]; st.Active = v[5] == "Active";
        if (isNew) AppData.Students.Add(st);
        Reload();
    }
}

public class RequestsPage : PageBase
{
    readonly Shell shell;
    readonly DataGridView g = Tables.Create(34);
    readonly SearchBox search = new SearchBox("Search names");
    readonly ComboBox course = Ui.Combo("All Courses", "BS Computer Science", "BS Information Technology", "BS Psychology");
    readonly ComboBox year = Ui.Combo("All Year Levels", "1st Year", "2nd Year", "3rd Year", "4th Year");
    readonly ComboBox status = Ui.Combo("All Statuses", "Pending", "Approved", "Declined");
    readonly Label info = Theme.Lbl("", 8.5f);

    public RequestsPage(Shell shell) : base("Borrow & Return Requests", "Review and manage student borrowing and return request")
    {
        this.shell = shell;
        g.Columns.Add(Tables.Text("sid", "Student ID", 70));
        g.Columns.Add(Tables.Text("name", "Full Name", 90));
        g.Columns.Add(Tables.Text("book", "Book Title", 100));
        g.Columns.Add(Tables.Text("course", "Course", 90));
        g.Columns.Add(Tables.Text("date", "Date", 60));
        g.Columns.Add(Tables.Chip("status", "Status", 60));
        g.Columns.Add(Tables.Btn("approve", "Actions", "Approve", Color.FromArgb(0x1E, 0x7A, 0x2E), Color.White, 50, 8));
        g.Columns.Add(Tables.Btn("decline", "", "Decline", Color.FromArgb(0xE0, 0x1B, 0x1B), Color.White, 50, 8));
        g.CellContentClick += (s, e) =>
        {
            if (e.RowIndex < 0) return;
            var r = g.Rows[e.RowIndex].Tag as BookRequest;
            string col = g.Columns[e.ColumnIndex].Name;
            if (r == null || (col != "approve" && col != "decline")) return;
            if (r.Status != "Pending") { MessageBox.Show("This request was already handled.", "Requests"); return; }
            if (col == "approve")
            {
                if (!AppData.Approve(r)) { MessageBox.Show("No copies available for this book.", "Cannot approve"); return; }
            }
            else AppData.Decline(r);
            Reload();
            shell.RefreshBadge();
        };

        var card = new RoundedPanel { Dock = DockStyle.Fill, Padding = new Padding(10), Fill = Color.FromArgb(0xF3, 0xEC, 0xDD) };
        var ct = Theme.Lbl("📄  Request List", 11.5f, FontStyle.Bold);
        ct.Dock = DockStyle.Top;
        ct.AutoSize = false;
        ct.Height = 30;
        g.Dock = DockStyle.Fill;
        card.Controls.Add(g);
        card.Controls.Add(Ui.Pager(info));
        card.Controls.Add(ct);

        search.Box.TextChanged += (s, e) => Reload();
        course.SelectedIndexChanged += (s, e) => Reload();
        year.SelectedIndexChanged += (s, e) => Reload();
        status.SelectedIndexChanged += (s, e) => Reload();

        Body.Controls.Add(card);
        Body.Controls.Add(Ui.FilterRow(search, course, year, status));
        Reload();
    }

    void Reload()
    {
        g.Rows.Clear();
        string q = search.Box.Text.Trim().ToLowerInvariant();
        foreach (var r in AppData.Requests.OrderByDescending(x => x.Status == "Pending"))
        {
            if (course.SelectedIndex > 0 && r.Student.Course != (string)course.SelectedItem) continue;
            if (year.SelectedIndex > 0 && r.Student.Year != (string)year.SelectedItem) continue;
            if (status.SelectedIndex > 0 && r.Status != (string)status.SelectedItem) continue;
            if (q.Length > 0 && !(r.Student.Name + " " + r.Student.Id + " " + r.Book.Title).ToLowerInvariant().Contains(q)) continue;
            int i = g.Rows.Add(r.Student.Id, r.Student.Name, r.Book.Title, r.Student.Course, r.Date.ToString("MM/dd/yyyy"), r.Status);
            g.Rows[i].Tag = r;
        }
        Tables.Done(g);
        info.Text = $"Showing {g.Rows.Count} of {AppData.Requests.Count} requests";
    }
}
