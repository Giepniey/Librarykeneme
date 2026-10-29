using System.Drawing.Printing;

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
                try
                {
                    Database.DeleteBook(b);
                    AppData.Books.Remove(b);
                    AppData.LoadCart();
                    Reload();
                }
                catch (Exception ex) when (Database.IsReferenceBlocked(ex))
                {
                    MessageBox.Show(
                        $"\"{b.Title}\" cannot be deleted because it is still linked to borrowing history or pending requests.",
                        "Delete book", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
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
        var v = PromptDialog.ShowWithCover(FindForm(), "Add New Book", out var zone, "", showDelete: false,
            new Field("Title", ""), new Field("Author", ""), new Field("ISBN", ""), new Field("Category", "FIC", Cats), new Field("Quantity", "1"));
        if (v == null) return;
        if (string.IsNullOrWhiteSpace(v[0]) || !int.TryParse(v[4], out int qty) || qty < 1) { MessageBox.Show("Please enter a title and a valid quantity.", "Add book"); return; }
        var b = new Book { Title = v[0].Trim(), Author = v[1].Trim(), Isbn = v[2].Trim(), Category = v[3], Qty = qty, CoverColor = Color.FromArgb(0x8A, 0x6E, 0x50) };
        if (AppData.Books.Any(x => x.Isbn.Equals(b.Isbn, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show($"A book with ISBN \"{b.Isbn}\" already exists.", "Add book");
            return;
        }
        if (zone?.CoverImage != null)
            b.CoverPath = Database.SaveCover(b, zone.CoverImage);
        AppData.Books.Add(b);
        Database.SaveBook(b);
        Reload();
    }

    void EditBook(Book b)
    {
        var v = PromptDialog.ShowWithCover(FindForm(), "Edit Book", out var zone, b.CoverPath, showDelete: true,
            new Field("Title", b.Title), new Field("Author", b.Author), new Field("ISBN", b.Isbn), new Field("Category", b.Category, Cats), new Field("Quantity", b.Qty.ToString()));
        if (v == null) return;   // Cancel: leave the old cover untouched in dialog and table
        if (string.IsNullOrWhiteSpace(v[0]) || !int.TryParse(v[4], out int qty) || qty < b.Out) { MessageBox.Show("Please enter a title and a quantity that covers the borrowed copies.", "Edit book"); return; }
        string newIsbn = v[2].Trim();
        if (AppData.Books.Any(x => x != b && x.Isbn.Equals(newIsbn, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show($"A book with ISBN \"{newIsbn}\" already exists.", "Edit book");
            return;
        }
        string previousIsbn = b.Isbn;
        b.Title = v[0].Trim(); b.Author = v[1].Trim(); b.Isbn = newIsbn; b.Category = v[3]; b.Qty = qty;
        // Persist the row first so cover updates below target the (possibly renamed) ISBN.
        Database.SaveBook(b, previousIsbn);
        if (zone?.Cleared == true)
        {
            Database.DeleteCover(b);          // removes file + database entry
            b.CoverPath = "";
        }
        else if (zone?.Changed == true && zone.CoverImage != null)
            b.CoverPath = Database.SaveCover(b, zone.CoverImage);
        Database.SaveBook(b);
        Reload();   // refresh the books table so the new (or removed) cover shows right away
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
    readonly DateFilterStatCard sActive = new DateFilterStatCard("✔", "Active Students", Color.FromArgb(0x2E, 0x9E, 0x3A), s => s.Active);
    readonly DateFilterStatCard sInactive = new DateFilterStatCard("✖", "Inactive Students", Color.FromArgb(0xD0, 0x20, 0x20), s => !s.Active);
    readonly DateFilterStatCard sNew = new DateFilterStatCard("＋", "New Registrations", Color.FromArgb(0xE0, 0x8A, 0x00), s => true);

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
        var actCol = Tables.Btn("view", "Action", "👁", Theme.Card, Theme.Ink, 55, 10);
        actCol.MinimumWidth = 60;
        g.Columns.Add(actCol);
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
                    MessageBox.Show($"{st.Name}\nID: {st.Id}\nEmail: {st.Email}\nFunction: {(st.IsSa ? "Student Assistant" : "Student")}\n{st.Course} - {StudentYearHelper.ToStandardDisplay(st.Year)}\nStatus: {(st.Active ? "Active" : "Inactive")}", "Student");
                    break;
                case "edit": Edit(st); break;
                case "del":
                    if (st == AppData.Me) { MessageBox.Show("The signed-in student cannot be deleted.", "Delete student"); break; }
                    if (MessageBox.Show($"Delete {st.Name}?", "Delete student", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        try
                        {
                            Database.DeleteStudent(st);
                            AppData.Students.Remove(st);
                            Reload();
                        }
                        catch (Exception ex) when (Database.IsReferenceBlocked(ex))
                        {
                            MessageBox.Show(
                                $"{st.Name} cannot be deleted because they still have loans, requests, or borrowing history.",
                                "Delete student", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
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
        Body.Controls.Add(Ui.StatRow(108, sTotal, sActive, sInactive, sNew));
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
            if (year.SelectedIndex > 0 && !StudentYearHelper.Matches(s.Year, (string)year.SelectedItem)) continue;
            if (status.SelectedIndex > 0 && s.Active != (status.SelectedIndex == 1)) continue;
            if (q.Length > 0 && !(s.Name + " " + s.Id + " " + s.Email).ToLowerInvariant().Contains(q)) continue;
            int i = g.Rows.Add(s.Id, s.Name, s.Email, s.Course, StudentYearHelper.ToStandardDisplay(s.Year), s.Active ? "Active" : "Inactive");
            g.Rows[i].Tag = s;
        }
        Tables.Done(g);
        info.Text = $"Showing {g.Rows.Count} of {AppData.Students.Count} students";
        sTotal.Value = AppData.Students.Count.ToString("N0");
        sActive.UpdateCount();
        sInactive.UpdateCount();
        sNew.UpdateCount();
    }

    void Edit(Student st)
    {
        bool isNew = st == null;
        var v = PromptDialog.Show(FindForm(), isNew ? "Add New Student" : "Edit Student",
            new Field("Student ID", st?.Id ?? ""),
            new Field("Full Name", st?.Name ?? ""),
            new Field("Email Address", st?.Email ?? ""),
            new Field("Function", st != null && st.IsSa ? "Student Assistant" : "Student", new[] { "Student", "Student Assistant" }),
            new Field("Course", st?.Course ?? "BS Computer Science", new[] { "BS Computer Science", "BS Information Technology", "BS Psychology", "BS Engineering" }),
            new Field("Year Level", StudentYearHelper.ToStandardDisplay(st?.Year), new[] { "1st Year", "2nd Year", "3rd Year", "4th Year" }),
            new Field("Status", st == null || st.Active ? "Active" : "Inactive", new[] { "Active", "Inactive" }));
        if (v == null) return;
        if (string.IsNullOrWhiteSpace(v[0]) || string.IsNullOrWhiteSpace(v[1])) { MessageBox.Show("Student ID and full name are required.", "Student"); return; }
        string prevId = st?.Id;
        st ??= new Student { Joined = DateTime.Today };
        st.Id = v[0].Trim(); st.Name = v[1].Trim(); st.Email = v[2].Trim(); st.IsSa = v[3] == "Student Assistant"; st.Course = v[4]; st.Year = v[5]; st.Active = v[6] == "Active";
        if (isNew && AppData.Students.Any(s => s.Id.Equals(st.Id, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show($"A student with ID {st.Id} already exists.", "Student");
            return;
        }
        if (isNew) AppData.Students.Add(st);
        Database.SaveStudent(st, prevId);
        Reload();
    }
}

public class RequestsPage : PageBase
{
    readonly Shell shell;

    // Tabs
    readonly RequestTab tabBorrow;
    readonly RequestTab tabReturn;

    // Content containers
    readonly Panel contentPanel;
    readonly RoundedPanel cardBorrow;
    readonly RoundedPanel cardReturn;

    // Borrow Table
    DataGridView gBorrow;
    Label lblBorrowTitle;
    Label lblBorrowInfo;
    bool allBorrowChecked = false;

    // Return Table
    DataGridView gReturn;
    Label lblReturnTitle;
    Label lblReturnInfo;
    bool allReturnChecked = false;

    public RequestsPage(Shell shell) : base("Borrow and Return Requests", "Review and manage student borrowing and return request.")
    {
        this.shell = shell;

        // 1. Two tabs directly under page title
        var tabsRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 44,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 0, 0, 8),
            Margin = new Padding(0)
        };

        tabBorrow = new RequestTab("📖", "Borrow Requests", Color.FromArgb(0xDC, 0x26, 0x26), true);
        tabReturn = new RequestTab("🔄", "Return Requests", Color.FromArgb(0x8D, 0x7B, 0x68), false);
        tabBorrow.Margin = new Padding(0, 0, 10, 0);
        tabReturn.Margin = new Padding(0);

        tabBorrow.Click += (s, e) => SwitchTab(true);
        tabReturn.Click += (s, e) => SwitchTab(false);

        tabsRow.Controls.Add(tabBorrow);
        tabsRow.Controls.Add(tabReturn);

        // 2. Card container
        contentPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

        cardBorrow = BuildBorrowCard();
        cardReturn = BuildReturnCard();
        cardReturn.Visible = false;

        contentPanel.Controls.Add(cardBorrow);
        contentPanel.Controls.Add(cardReturn);

        Body.Controls.Add(contentPanel);
        Body.Controls.Add(tabsRow);

        Reload();
    }

    RoundedPanel BuildBorrowCard()
    {
        var card = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            Fill = Color.FromArgb(0xF3, 0xEC, 0xDD)
        };

        var head = new Panel { Dock = DockStyle.Top, Height = 34, BackColor = Color.Transparent };
        var icon = new Label
        {
            Text = "📖", Font = Theme.E(12f), ForeColor = Theme.Ink,
            AutoSize = true, Location = new Point(4, 4), BackColor = Color.Transparent
        };
        lblBorrowTitle = Theme.Lbl("Pending Borrow Requests (0)", 11.5f, FontStyle.Bold);
        lblBorrowTitle.Location = new Point(28, 4);
        head.Controls.Add(icon);
        head.Controls.Add(lblBorrowTitle);

        gBorrow = Tables.Create(54);
        gBorrow.Dock = DockStyle.Fill;

        var chkCol = new DataGridViewCheckBoxColumn
        {
            Name = "chk", HeaderText = "", Width = 34,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        gBorrow.Columns.Add(chkCol);
        gBorrow.Columns.Add(Tables.Text("id", "Request ID", 90));
        gBorrow.Columns.Add(Tables.Text("student", "Student", 125));
        gBorrow.Columns.Add(Tables.Text("book", "Book Title", 200));
        gBorrow.Columns.Add(Tables.Text("date", "Request Date", 100));
        gBorrow.Columns.Add(Tables.Chip("status", "Status", 75));

        var actCol = new DataGridViewTextBoxColumn
        {
            Name = "actions", HeaderText = "Actions", Width = 175,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        gBorrow.Columns.Add(actCol);

        SetupGridPainting(gBorrow, () => allBorrowChecked);
        SetupGridInteraction(gBorrow, true);

        var footer = new Panel { Dock = DockStyle.Bottom, Height = 40, BackColor = Color.Transparent };
        lblBorrowInfo = Theme.Lbl("Showing 0 requests", 8.5f, FontStyle.Regular, Theme.Muted);
        lblBorrowInfo.Location = new Point(4, 12);

        var btnPrev = Theme.Btn("‹", Theme.Input, Theme.Ink, 30, 26, 9.5f);
        var btnPage1 = Theme.Btn("1", Color.FromArgb(0x55, 0x48, 0x3A), Color.White, 30, 26, 9f);
        var btnNext = Theme.Btn("›", Theme.Input, Theme.Ink, 30, 26, 9.5f);

        btnPrev.Click += (s, e) => MessageBox.Show("You are on the first page.", "Pagination");
        btnNext.Click += (s, e) => MessageBox.Show("There are no more pages in this demo.", "Pagination");

        footer.Controls.AddRange(new Control[] { lblBorrowInfo, btnPrev, btnPage1, btnNext });
        footer.Resize += (s, e) =>
        {
            btnNext.Location = new Point(footer.Width - btnNext.Width - 4, 7);
            btnPage1.Location = new Point(btnNext.Left - btnPage1.Width - 4, 7);
            btnPrev.Location = new Point(btnPage1.Left - btnPrev.Width - 4, 7);
        };

        card.Controls.Add(gBorrow);
        card.Controls.Add(footer);
        card.Controls.Add(head);
        return card;
    }

    RoundedPanel BuildReturnCard()
    {
        var card = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            Fill = Color.FromArgb(0xF3, 0xEC, 0xDD)
        };

        var head = new Panel { Dock = DockStyle.Top, Height = 34, BackColor = Color.Transparent };
        var icon = new Label
        {
            Text = "🔄", Font = Theme.E(12f), ForeColor = Theme.Ink,
            AutoSize = true, Location = new Point(4, 4), BackColor = Color.Transparent
        };
        lblReturnTitle = Theme.Lbl("Pending Return Requests (0)", 11.5f, FontStyle.Bold);
        lblReturnTitle.Location = new Point(28, 4);
        head.Controls.Add(icon);
        head.Controls.Add(lblReturnTitle);

        gReturn = Tables.Create(54);
        gReturn.Dock = DockStyle.Fill;

        var chkCol = new DataGridViewCheckBoxColumn
        {
            Name = "chk", HeaderText = "", Width = 34,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        gReturn.Columns.Add(chkCol);
        gReturn.Columns.Add(Tables.Text("id", "Request ID", 90));
        gReturn.Columns.Add(Tables.Text("student", "Student", 125));
        gReturn.Columns.Add(Tables.Text("book", "Book Title", 200));
        gReturn.Columns.Add(Tables.Text("date", "Request Date", 100));
        gReturn.Columns.Add(Tables.Chip("status", "Status", 75));

        var actCol = new DataGridViewTextBoxColumn
        {
            Name = "actions", HeaderText = "Actions", Width = 175,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        gReturn.Columns.Add(actCol);

        SetupGridPainting(gReturn, () => allReturnChecked);
        SetupGridInteraction(gReturn, false);

        var footer = new Panel { Dock = DockStyle.Bottom, Height = 40, BackColor = Color.Transparent };
        lblReturnInfo = Theme.Lbl("Showing 0 requests", 8.5f, FontStyle.Regular, Theme.Muted);
        lblReturnInfo.Location = new Point(4, 12);

        var btnPrev = Theme.Btn("‹", Theme.Input, Theme.Ink, 30, 26, 9.5f);
        var btnPage1 = Theme.Btn("1", Color.FromArgb(0x55, 0x48, 0x3A), Color.White, 30, 26, 9f);
        var btnNext = Theme.Btn("›", Theme.Input, Theme.Ink, 30, 26, 9.5f);

        btnPrev.Click += (s, e) => MessageBox.Show("You are on the first page.", "Pagination");
        btnNext.Click += (s, e) => MessageBox.Show("There are no more pages in this demo.", "Pagination");

        footer.Controls.AddRange(new Control[] { lblReturnInfo, btnPrev, btnPage1, btnNext });
        footer.Resize += (s, e) =>
        {
            btnNext.Location = new Point(footer.Width - btnNext.Width - 4, 7);
            btnPage1.Location = new Point(btnNext.Left - btnPage1.Width - 4, 7);
            btnPrev.Location = new Point(btnPage1.Left - btnPrev.Width - 4, 7);
        };

        card.Controls.Add(gReturn);
        card.Controls.Add(footer);
        card.Controls.Add(head);
        return card;
    }

    void SetupGridPainting(DataGridView g, Func<bool> getAllChecked)
    {
        g.CellPainting += (s, e) =>
        {
            // 1. Header checkbox
            if (e.RowIndex == -1 && e.ColumnIndex == 0)
            {
                e.Paint(e.CellBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.Border);
                var boxRect = new Rectangle(e.CellBounds.X + (e.CellBounds.Width - 14) / 2, e.CellBounds.Y + (e.CellBounds.Height - 14) / 2, 14, 14);
                ControlPaint.DrawCheckBox(e.Graphics, boxRect, getAllChecked() ? ButtonState.Checked : ButtonState.Normal);
                e.Handled = true;
                return;
            }

            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            var req = g.Rows[e.RowIndex].Tag as BookRequest;
            if (req == null) return;
            string colName = g.Columns[e.ColumnIndex].Name;

            // 2. Student column (Name on top line, ID below)
            if (colName == "student")
            {
                e.Paint(e.CellBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.Border);
                var nameRect = new Rectangle(e.CellBounds.X + 8, e.CellBounds.Y + 9, e.CellBounds.Width - 16, 18);
                TextRenderer.DrawText(e.Graphics, req.Student?.Name ?? "", Theme.F(9f, FontStyle.Bold), nameRect, Theme.Ink,
                    TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

                var idRect = new Rectangle(e.CellBounds.X + 8, e.CellBounds.Y + 28, e.CellBounds.Width - 16, 16);
                TextRenderer.DrawText(e.Graphics, req.Student?.Id ?? "", Theme.F(8f), idRect, Theme.Muted,
                    TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
                e.Handled = true;
                return;
            }

            // 3. Book Title column (Cover thumbnail + Title on top line, Author below)
            if (colName == "book")
            {
                e.Paint(e.CellBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.Border);

                var thumb = CoverFactory.Make(req.Book, 28, 38);
                if (thumb != null)
                {
                    int imgY = e.CellBounds.Y + (e.CellBounds.Height - 38) / 2;
                    e.Graphics.DrawImage(thumb, e.CellBounds.X + 8, imgY, 28, 38);
                }

                int textX = e.CellBounds.X + 8 + 28 + 10;
                int textW = Math.Max(10, e.CellBounds.Width - (textX - e.CellBounds.X) - 8);

                var titleRect = new Rectangle(textX, e.CellBounds.Y + 9, textW, 18);
                TextRenderer.DrawText(e.Graphics, req.Book?.Title ?? "", Theme.F(9f, FontStyle.Bold), titleRect, Theme.Ink,
                    TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

                var authRect = new Rectangle(textX, e.CellBounds.Y + 28, textW, 16);
                TextRenderer.DrawText(e.Graphics, req.Book?.Author ?? "", Theme.F(8f), authRect, Theme.Muted,
                    TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
                e.Handled = true;
                return;
            }

            // 4. Request Date column (Date on top line, Time below)
            if (colName == "date")
            {
                e.Paint(e.CellBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.Border);
                var dRect = new Rectangle(e.CellBounds.X + 8, e.CellBounds.Y + 9, e.CellBounds.Width - 16, 18);
                TextRenderer.DrawText(e.Graphics, req.Date.ToString("MMM dd, yyyy"), Theme.F(8.5f, FontStyle.Bold), dRect, Theme.Ink,
                    TextFormatFlags.Left);

                var tRect = new Rectangle(e.CellBounds.X + 8, e.CellBounds.Y + 28, e.CellBounds.Width - 16, 16);
                TextRenderer.DrawText(e.Graphics, req.Date.ToString("hh:mm tt"), Theme.F(7.5f), tRect, Theme.Muted,
                    TextFormatFlags.Left);
                e.Handled = true;
                return;
            }

            // 5. Actions column (Approve [green], Reject [red], View [eye])
            if (colName == "actions")
            {
                e.Paint(e.CellBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.Border);
                int btnY = e.CellBounds.Y + (e.CellBounds.Height - 26) / 2;
                var approveRect = new Rectangle(e.CellBounds.X + 6, btnY, 60, 26);
                var rejectRect = new Rectangle(e.CellBounds.X + 72, btnY, 54, 26);
                var viewRect = new Rectangle(e.CellBounds.X + 132, btnY, 28, 26);

                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                // Approve button (Green)
                using (var path = Theme.Round(approveRect, 5))
                using (var br = new SolidBrush(Color.FromArgb(0x2E, 0x7D, 0x32)))
                {
                    e.Graphics.FillPath(br, path);
                }
                TextRenderer.DrawText(e.Graphics, "Approve", Theme.F(8f, FontStyle.Bold), approveRect, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                // Reject button (Red)
                using (var path = Theme.Round(rejectRect, 5))
                using (var br = new SolidBrush(Color.FromArgb(0xD3, 0x2F, 0x2F)))
                {
                    e.Graphics.FillPath(br, path);
                }
                TextRenderer.DrawText(e.Graphics, "Reject", Theme.F(8f, FontStyle.Bold), rejectRect, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                // View button (Eye icon in styled pill)
                using (var path = Theme.Round(viewRect, 5))
                using (var br = new SolidBrush(Color.FromArgb(0xE6, 0xDA, 0xC8)))
                using (var pen = new Pen(Color.FromArgb(0xC5, 0xB6, 0xA0)))
                {
                    e.Graphics.FillPath(br, path);
                    e.Graphics.DrawPath(pen, path);
                }
                TextRenderer.DrawText(e.Graphics, "👁", Theme.E(9.5f), viewRect, Theme.Ink,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                e.Handled = true;
                return;
            }
        };
    }

    void SetupGridInteraction(DataGridView g, bool isBorrow)
    {
        g.CellClick += (s, e) =>
        {
            if (e.RowIndex == -1 && e.ColumnIndex == 0)
            {
                bool newState = isBorrow ? !allBorrowChecked : !allReturnChecked;
                if (isBorrow) allBorrowChecked = newState; else allReturnChecked = newState;
                foreach (DataGridViewRow row in g.Rows)
                    row.Cells["chk"].Value = newState;
                g.Invalidate();
            }
        };

        g.CellMouseClick += (s, e) =>
        {
            if (e.RowIndex < 0 || e.RowIndex >= g.Rows.Count) return;
            if (g.Columns[e.ColumnIndex].Name != "actions") return;
            var req = g.Rows[e.RowIndex].Tag as BookRequest;
            if (req == null) return;

            int btnY = (g.Rows[e.RowIndex].Height - 26) / 2;
            var approveRect = new Rectangle(6, btnY, 60, 26);
            var rejectRect = new Rectangle(72, btnY, 54, 26);
            var viewRect = new Rectangle(132, btnY, 28, 26);

            if (approveRect.Contains(e.Location))
            {
                HandleApprove(req);
            }
            else if (rejectRect.Contains(e.Location))
            {
                HandleReject(req);
            }
            else if (viewRect.Contains(e.Location))
            {
                ShowRequestDetails(req);
            }
        };

        g.CellMouseMove += (s, e) =>
        {
            if (e.RowIndex >= 0 && g.Columns[e.ColumnIndex].Name == "actions")
            {
                int btnY = (g.Rows[e.RowIndex].Height - 26) / 2;
                var approveRect = new Rectangle(6, btnY, 60, 26);
                var rejectRect = new Rectangle(72, btnY, 54, 26);
                var viewRect = new Rectangle(132, btnY, 28, 26);
                if (approveRect.Contains(e.Location) || rejectRect.Contains(e.Location) || viewRect.Contains(e.Location))
                {
                    g.Cursor = Cursors.Hand;
                    return;
                }
            }
            g.Cursor = Cursors.Default;
        };
    }

    void HandleApprove(BookRequest req)
    {
        if (req.Status != "Pending")
        {
            MessageBox.Show("This request has already been handled.", "Request", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!AppData.Approve(req))
        {
            MessageBox.Show("No copies available for this book.", "Cannot Approve", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        MessageBox.Show($"Request {req.Id} for \"{req.Book?.Title}\" by {req.Student?.Name} has been approved.",
            "Request Approved", MessageBoxButtons.OK, MessageBoxIcon.Information);

        Reload();
        shell.RefreshBadge();
    }

    void HandleReject(BookRequest req)
    {
        if (req.Status != "Pending")
        {
            MessageBox.Show("This request has already been handled.", "Request", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (MessageBox.Show($"Are you sure you want to reject request {req.Id} for \"{req.Book?.Title}\"?",
            "Confirm Reject", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        AppData.Decline(req);

        Reload();
        shell.RefreshBadge();
    }

    void ShowRequestDetails(BookRequest r)
    {
        using var f = new Form
        {
            Text = $"Request Details - {r.Id}",
            Size = new Size(420, 390),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = Theme.Card,
            Font = Theme.F(9)
        };

        int y = 16;
        var lblH = Theme.Lbl($"📋  {r.Id}", 13, FontStyle.Bold);
        lblH.Location = new Point(20, y);
        f.Controls.Add(lblH);

        var chipStatus = Tables.ChipColors(r.Status, false);
        var pnlStatus = new RoundedPanel { Fill = chipStatus.Item1, Size = new Size(80, 22), Location = new Point(300, y) };
        var lblSt = Theme.Lbl(r.Status, 8f, FontStyle.Bold, chipStatus.Item2);
        lblSt.Location = new Point((80 - lblSt.PreferredWidth) / 2, 3);
        pnlStatus.Controls.Add(lblSt);
        f.Controls.Add(pnlStatus);

        y += 36;
        void AddRow(string label, string val)
        {
            var l = Theme.Lbl(label, 8.5f, FontStyle.Bold, Theme.Muted);
            l.Location = new Point(20, y);
            var v = Theme.Lbl(val, 9f, FontStyle.Bold, Theme.Ink);
            v.Location = new Point(140, y);
            f.Controls.Add(l);
            f.Controls.Add(v);
            y += 28;
        }

        AddRow("Request Type:", r.Type + " Request");
        AddRow("Student Name:", r.Student?.Name ?? "—");
        AddRow("Student ID:", r.Student?.Id ?? "—");
        AddRow("Book Title:", r.Book?.Title ?? "—");
        AddRow("Book Author:", r.Book?.Author ?? "—");
        AddRow("ISBN:", string.IsNullOrEmpty(r.Book?.Isbn) ? "—" : r.Book.Isbn);
        AddRow("Request Date:", r.Date.ToString("MMM dd, yyyy  hh:mm tt"));

        y += 16;
        var btnClose = Theme.Btn("Close", Theme.Brown, Color.White, 90, 32);
        btnClose.Location = new Point(300, y);
        btnClose.DialogResult = DialogResult.OK;
        f.Controls.Add(btnClose);

        if (r.Status == "Pending")
        {
            var btnApprove = Theme.Btn("Approve", Color.FromArgb(0x2E, 0x7D, 0x32), Color.White, 90, 32);
            btnApprove.Location = new Point(90, y);
            btnApprove.Click += (s, e) => { f.Close(); HandleApprove(r); };

            var btnReject = Theme.Btn("Reject", Color.FromArgb(0xD3, 0x2F, 0x2F), Color.White, 90, 32);
            btnReject.Location = new Point(190, y);
            btnReject.Click += (s, e) => { f.Close(); HandleReject(r); };

            f.Controls.Add(btnApprove);
            f.Controls.Add(btnReject);
        }

        f.ShowDialog(FindForm());
    }

    void SwitchTab(bool toBorrow)
    {
        tabBorrow.IsSelected = toBorrow;
        tabReturn.IsSelected = !toBorrow;
        tabBorrow.Invalidate();
        tabReturn.Invalidate();
        cardBorrow.Visible = toBorrow;
        cardReturn.Visible = !toBorrow;
        if (toBorrow) cardBorrow.BringToFront(); else cardReturn.BringToFront();
    }

    void Reload()
    {
        // 1. Reload Borrow table
        gBorrow.Rows.Clear();
        var pendingBorrows = AppData.Requests.Where(r => r.Type == "Borrow" && r.Status == "Pending").ToList();
        foreach (var r in pendingBorrows)
        {
            int i = gBorrow.Rows.Add(false, r.Id, r.Student?.Name ?? "", r.Book?.Title ?? "", r.Date.ToString("MMM dd, yyyy"), r.Status);
            gBorrow.Rows[i].Tag = r;
        }
        Tables.Done(gBorrow);
        lblBorrowTitle.Text = $"Pending Borrow Requests ({pendingBorrows.Count})";
        lblBorrowInfo.Text = $"Showing 1–{pendingBorrows.Count} of {pendingBorrows.Count} requests";
        tabBorrow.Count = pendingBorrows.Count;
        tabBorrow.Invalidate();

        // 2. Reload Return table
        gReturn.Rows.Clear();
        var pendingReturns = AppData.Requests.Where(r => r.Type == "Return" && r.Status == "Pending").ToList();
        foreach (var r in pendingReturns)
        {
            int i = gReturn.Rows.Add(false, r.Id, r.Student?.Name ?? "", r.Book?.Title ?? "", r.Date.ToString("MMM dd, yyyy"), r.Status);
            gReturn.Rows[i].Tag = r;
        }
        Tables.Done(gReturn);
        lblReturnTitle.Text = $"Pending Return Requests ({pendingReturns.Count})";
        lblReturnInfo.Text = $"Showing 1–{pendingReturns.Count} of {pendingReturns.Count} requests";
        tabReturn.Count = pendingReturns.Count;
        tabReturn.Invalidate();
    }
}

/// <summary>Admin screen: pick a borrow/return request and preview / print the transaction slip.</summary>
public class PrintFormatPage : PageBase
{
    ComboBox combo;
    Panel preview;
    BookRequest cur;

    public PrintFormatPage(Shell shell) : base("Print Format", "Preview and print library transaction slips")
    {
        var sel = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = Color.Transparent };
        var lbl = Theme.Lbl("Select Request", 10, FontStyle.Bold);
        lbl.Location = new Point(0, 8);
        combo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Input, ForeColor = Theme.Ink, Font = Theme.F(9),
            Location = new Point(110, 6), Width = 380
        };
        combo.SelectedIndexChanged += (s, e) => LoadSlip();
        var print = Theme.Btn("Print", Theme.Brown, Color.White, 110, 32);
        print.Location = new Point(510, 5);
        print.Click += (s, e) => DoPrint();
        sel.Controls.Add(lbl);
        sel.Controls.Add(combo);
        sel.Controls.Add(print);
        Body.Controls.Add(sel);

        preview = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        preview.Paint += (s, e) => PaintSlip(e);
        Body.Controls.Add(preview);

        RefreshCombo();
    }

    void RefreshCombo()
    {
        combo.Items.Clear();
        foreach (var r in AppData.Requests)
            combo.Items.Add($"{r.Id} · {(r.Type == "Borrow" ? "Borrow" : "Return")} · {r.Student?.Name} → {r.Book?.Title}");
        if (combo.Items.Count > 0) combo.SelectedIndex = 0;
    }

    void LoadSlip()
    {
        cur = combo.SelectedItem == null ? null : AppData.Requests.FirstOrDefault(r => r.Id == combo.SelectedItem.ToString());
        preview.Invalidate();
    }

    void PaintSlip(PaintEventArgs e)
    {
        using var bmp = RenderSlip(cur);
        float scale = Math.Min(preview.Width / (float)bmp.Width, preview.Height / (float)bmp.Height);
        float w = bmp.Width * scale;
        float h = bmp.Height * scale;
        e.Graphics.DrawImage(bmp, (preview.Width - w) / 2f, (preview.Height - h) / 2f, w, h);
    }

    void DoPrint()
    {
        if (cur == null) { MessageBox.Show("Please select a request to print.", "Print Format"); return; }
        var pd = new PrintDocument();
        pd.PrintPage += (s, e) =>
        {
            using var bmp = RenderSlip(cur);
            float scale = Math.Min(pd.DefaultPageSettings.PaperSize.Width / bmp.Width, pd.DefaultPageSettings.PaperSize.Height / bmp.Height);
            float w = bmp.Width * scale;
            float h = bmp.Height * scale;
            e.Graphics.DrawImage(bmp, (pd.DefaultPageSettings.PaperSize.Width - w) / 2f, (pd.DefaultPageSettings.PaperSize.Height - h) / 2f, w, h);
            e.HasMorePages = false;
        };
        var dlg = new PrintPreviewDialog { Document = pd, Owner = FindForm() };
        dlg.ShowDialog();
    }

    static Bitmap RenderSlip(BookRequest r)
    {
        var bmp = new Bitmap(600, 800);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.White);

        var type = r?.Type == "Return" ? "Return" : "Borrow";
        var title = r?.Book?.Title ?? "—";
        var author = r?.Book?.Author ?? "—";
        var isbn = r?.Book?.Isbn ?? "—";
        var cat = r?.Book?.Category ?? "—";
        var stName = r?.Student?.Name ?? "—";
        var stId = r?.Student?.Id ?? "—";
        var stEmail = r?.Student?.Email ?? "—";
        var status = r?.Status ?? "Pending";

        float y = 26;
        DrawText(g, "Library Inventory System", new Font("Segoe UI", 22, FontStyle.Bold), Theme.Ink, 40, y, 520); y += 30;
        DrawText(g, type == "Borrow" ? "OFFICIAL BORROW REQUEST SLIP" : "OFFICIAL RETURN REQUEST SLIP", new Font("Segoe UI", 10, FontStyle.Bold), Theme.Muted, 40, y, 520); y += 26;
        using (var pen = new Pen(Theme.Border)) { g.DrawLine(pen, 40, y, 560, y); }
        y += 34;

        DrawText(g, "BOOK", new Font("Segoe UI", 11, FontStyle.Bold), Theme.Brown, 40, y, 520); y += 30;
        DrawText(g, $"Title: {title}", new Font("Segoe UI", 10, FontStyle.Bold), Theme.Ink, 40, y, 520); y += 16;
        DrawText(g, $"Author: {author}", new Font("Segoe UI", 10, FontStyle.Regular), Theme.Ink, 40, y, 520); y += 16;
        DrawText(g, $"ISBN: {isbn}", new Font("Segoe UI", 10, FontStyle.Regular), Theme.Ink, 40, y, 520); y += 16;
        DrawText(g, $"Category: {cat}", new Font("Segoe UI", 10, FontStyle.Regular), Theme.Ink, 40, y, 520); y += 24;

        DrawText(g, "STUDENT", new Font("Segoe UI", 11, FontStyle.Bold), Theme.Brown, 40, y, 520); y += 30;
        DrawText(g, $"Name: {stName}", new Font("Segoe UI", 10, FontStyle.Bold), Theme.Ink, 40, y, 520); y += 16;
        DrawText(g, $"Student ID: {stId}", new Font("Segoe UI", 10, FontStyle.Regular), Theme.Ink, 40, y, 520); y += 16;
        DrawText(g, $"Email: {stEmail}", new Font("Segoe UI", 10, FontStyle.Regular), Theme.Ink, 40, y, 520); y += 24;

        DrawText(g, "TRANSACTION", new Font("Segoe UI", 11, FontStyle.Bold), Theme.Brown, 40, y, 520); y += 30;
        DrawText(g, $"Type: {type} Request", new Font("Segoe UI", 10, FontStyle.Bold), Theme.Ink, 40, y, 520); y += 16;
        DrawText(g, $"Request ID: {r?.Id ?? "—"}", new Font("Segoe UI", 10, FontStyle.Regular), Theme.Ink, 40, y, 520); y += 16;
        DrawText(g, $"Request Date: {r?.Date.ToString("MMM dd, yyyy hh:mm tt")}", new Font("Segoe UI", 10, FontStyle.Regular), Theme.Ink, 40, y, 520); y += 16;
        DrawText(g, $"Status: {status}", new Font("Segoe UI", 10, FontStyle.Bold), Theme.Ink, 40, y, 520); y += 26;

        float by = 724;
        var rnd = new Random(2025);
        float bx = 40;
        using (var br = new SolidBrush(Color.Black))
        {
            while (bx < 540)
            {
                float wdt = rnd.Next(2, 6);
                float hgt = rnd.Next(24, 46);
                g.FillRectangle(br, bx, by, wdt, hgt);
                bx += wdt + rnd.Next(1, 5);
            }
        }
        DrawText(g, "Library Inventory System", new Font("Segoe UI", 9, FontStyle.Bold), Theme.Muted, 40, 762, 520);

        return bmp;
    }

    static void DrawText(Graphics g, string text, Font f, Color c, float x, float y, float maxWidth)
    {
        if (string.IsNullOrWhiteSpace(text)) text = "—";
        var sf = new StringFormat { Alignment = StringAlignment.Near };
        using var br = new SolidBrush(c);
        float h = g.MeasureString(text, f, new SizeF(maxWidth, float.MaxValue), sf).Height;
        g.DrawString(text, f, br, new RectangleF(x, y, maxWidth, h), sf);
    }
}
