namespace LibraryInventory;

public class BrowseBooksPage : PageBase
{
    readonly FlowLayoutPanel flow;
    readonly SearchBox search;
    readonly List<Button> tabs = new();
    string cat = "ALL";

    public BrowseBooksPage(Shell shell) : base("Browse Books", "Explore the library collection")
    {
        flow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.Transparent, Padding = new Padding(0, 4, 0, 70) };

        var tabBar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, BackColor = Color.Transparent, WrapContents = false };
        foreach (var c in new[] { "ALL", "CIR", "FIC", "FIL", "REF" })
        {
            var b = Theme.Btn(c == "ALL" ? "ALL\nBOOKS" : c, Theme.Input, Theme.Ink, 92, 38, 9);
            b.Tag = c;
            b.Margin = new Padding(0, 2, 10, 8);
            b.Click += (s, e) => { cat = c; UpdateTabs(); Reload(); };
            tabBar.Controls.Add(b);
            tabs.Add(b);
        }

        search = new SearchBox("Search books by title, author, ....");
        search.Box.TextChanged += (s, e) => Reload();

        Body.Controls.Add(flow);
        Body.Controls.Add(tabBar);
        Body.Controls.Add(Ui.SearchWrap(search));
        UpdateTabs();
        Reload();
    }

    void UpdateTabs()
    {
        foreach (var b in tabs) b.BackColor = (string)b.Tag == cat ? Theme.Selected : Theme.Input;
    }

    void Reload()
    {
        flow.SuspendLayout();
        foreach (var c in flow.Controls.Cast<Control>().ToList()) { flow.Controls.Remove(c); c.Dispose(); }
        string q = search.Box.Text.Trim().ToLowerInvariant();
        foreach (var b in AppData.Books)
        {
            if (cat != "ALL" && b.Category != cat) continue;
            if (q.Length > 0 && !(b.Title + " " + b.Author + " " + b.Isbn).ToLowerInvariant().Contains(q)) continue;
            var card = new BookCard(b) { Margin = new Padding(0, 0, 12, 12) };
            card.AddClicked += AddToCart;
            flow.Controls.Add(card);
        }
        flow.ResumeLayout();
    }

    void AddToCart(Book b)
    {
        if (AppData.Suspended) { MessageBox.Show("Your account is suspended. Please contact the librarian.", "Account suspended"); return; }
        if (!b.Available) { MessageBox.Show("This book is currently borrowed.", "Not available"); return; }
        if (AppData.Cart.Contains(b)) { MessageBox.Show("This book is already in your cart.", "Book Cart"); return; }
        if (AppData.Cart.Count >= 3) { MessageBox.Show("You can only select a maximum of 3 books.", "Book Cart"); return; }
        AppData.Cart.Add(b);
        AppData.RaiseCartChanged();
    }
}

public class BookCartPage : PageBase
{
    readonly Shell shell;
    readonly Panel content = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
    readonly Button tabApproval, tabManage;

    public BookCartPage(Shell shell) : base("Book Cart", "Explore your collection")
    {
        this.shell = shell;
        var search = new SearchBox("Search books by title, author, ....");

        var tabRow = new TableLayoutPanel { Dock = DockStyle.Top, Height = 46, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent };
        tabRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        tabRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        tabRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        tabApproval = Theme.Btn("For approval", Theme.Selected, Theme.Ink, 100, 36, 10.5f);
        tabManage = Theme.Btn("Manage", Theme.Input, Theme.Ink, 100, 36, 10.5f);
        tabApproval.Dock = tabManage.Dock = DockStyle.Fill;
        tabApproval.Margin = new Padding(0, 0, 4, 8);
        tabManage.Margin = new Padding(4, 0, 0, 8);
        tabApproval.Click += (s, e) => ShowTab(false);
        tabManage.Click += (s, e) => ShowTab(true);
        tabRow.Controls.Add(tabApproval, 0, 0);
        tabRow.Controls.Add(tabManage, 1, 0);

        Body.Controls.Add(content);
        Body.Controls.Add(tabRow);
        Body.Controls.Add(Ui.SearchWrap(search));
        ShowTab(false);
    }

    void ShowTab(bool manage)
    {
        tabApproval.BackColor = manage ? Theme.Input : Theme.Selected;
        tabManage.BackColor = manage ? Theme.Selected : Theme.Input;
        foreach (var c in content.Controls.Cast<Control>().ToList()) { content.Controls.Remove(c); c.Dispose(); }
        content.Controls.Add(manage ? BuildManage() : BuildApproval());
    }

    Control BuildApproval()
    {
        var card = new RoundedPanel { Dock = DockStyle.Fill, Padding = new Padding(8), Fill = Color.FromArgb(0xF3, 0xEC, 0xDD) };
        var list = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = Color.Transparent };

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 84, BackColor = Color.Transparent };
        var sel = Theme.Lbl("Books selected", 10, FontStyle.Bold);
        sel.Location = new Point(10, 8);
        var max = Theme.Lbl("maximum of 3", 8.5f);
        max.Location = new Point(110, 11);
        var count = Theme.Lbl("0/3", 10, FontStyle.Bold);
        var submit = Theme.Btn("Submit borrow request", Color.FromArgb(0x0B, 0x5A, 0x2A), Color.White, 100, 38, 11);
        submit.Location = new Point(10, 38);
        bottom.Controls.AddRange(new Control[] { sel, max, count, submit });
        bottom.Resize += (s, e) =>
        {
            count.Location = new Point(bottom.Width - count.Width - 12, 8);
            submit.Width = Math.Max(100, bottom.Width - 20);
        };

        var boxes = new List<(CheckBox box, Book book)>();
        void UpdateCount() => count.Text = $"{boxes.Count(x => x.box.Checked)}/3";

        foreach (var b in AppData.Cart.ToList())
        {
            var row = new Panel { Size = new Size(700, 108), BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, 4) };
            var chk = new CheckBox { Checked = true, Location = new Point(6, 44), AutoSize = true, BackColor = Color.Transparent };
            chk.CheckedChanged += (s, e) => UpdateCount();
            boxes.Add((chk, b));
            var pic = new PictureBox { Image = CoverFactory.Make(b, 58, 86), Location = new Point(34, 10), Size = new Size(58, 86), SizeMode = PictureBoxSizeMode.StretchImage };
            var t = Theme.Lbl(b.Title, 11.5f, FontStyle.Bold); t.Location = new Point(104, 6);
            var a = Theme.Lbl(b.Author, 10, FontStyle.Regular, Theme.Muted); a.Location = new Point(104, 30);
            var c = Theme.Lbl(AppData.CategoryName(b.Category), 9, FontStyle.Bold, Theme.Green); c.Location = new Point(104, 50);
            var i = Theme.Lbl("ISBN: " + b.Isbn, 9, FontStyle.Bold); i.Location = new Point(104, 68);
            var chip = new Label { Text = "Available", Font = Theme.F(8, FontStyle.Bold), ForeColor = Color.White, BackColor = Color.FromArgb(0x3F, 0x9E, 0x4A), AutoSize = false, Size = new Size(70, 18), TextAlign = ContentAlignment.MiddleCenter, Location = new Point(104, 86) };
            var trash = new Button { Text = "🗑", Font = Theme.E(14), FlatStyle = FlatStyle.Flat, Size = new Size(40, 36), Cursor = Cursors.Hand, ForeColor = Theme.Ink, BackColor = Color.Transparent };
            trash.FlatAppearance.BorderSize = 0;
            trash.Click += (s, e) => { AppData.Cart.Remove(b); AppData.RaiseCartChanged(); ShowTab(false); };
            row.Controls.AddRange(new Control[] { chk, pic, t, a, c, i, chip, trash });
            row.Resize += (s, e) => trash.Location = new Point(row.Width - trash.Width - 8, 36);
            list.Controls.Add(row);
        }
        list.Resize += (s, e) => { foreach (Control r in list.Controls) r.Width = Math.Max(300, list.ClientSize.Width - 4); };
        if (AppData.Cart.Count == 0)
            list.Controls.Add(Theme.Lbl("Your cart is empty. Use the + button on Browse Books to add up to 3 books.", 10, FontStyle.Regular, Theme.Muted));
        UpdateCount();

        submit.Click += (s, e) =>
        {
            if (AppData.Suspended) { MessageBox.Show("Your account is suspended. Please contact the librarian.", "Account suspended"); return; }
            var chosen = boxes.Where(x => x.box.Checked).Select(x => x.book).ToList();
            if (chosen.Count == 0) { MessageBox.Show("Select at least one book.", "Book Cart"); return; }
            AppData.SubmitCart(chosen);
            shell.RefreshBadge();
            MessageBox.Show("Borrow request submitted. Wait for the librarian's approval.", "Book Cart");
            ShowTab(true);
        };

        card.Controls.Add(list);
        card.Controls.Add(bottom);
        return card;
    }

    Control BuildManage()
    {
        var g = Tables.Create(36, true);
        g.Dock = DockStyle.Top;
        g.Columns.Add(Tables.Text("title", "Title", 120));
        g.Columns.Add(Tables.Text("author", "Author", 90));
        g.Columns.Add(Tables.Text("isbn", "ISBN", 100));
        g.Columns.Add(Tables.Text("borrowed", "Borrowed", 80));
        g.Columns.Add(Tables.Text("due", "Due Date", 80));
        g.Columns.Add(Tables.SolidChip("action", "Action", 80));
        foreach (var l in AppData.Loans.Where(x => x.StudentId == AppData.Me.Id))
        {
            int i = g.Rows.Add(l.Book.Title, l.Book.Author, l.Book.Isbn, l.Borrowed.ToString("MM/dd/yyyy"), l.Due.ToString("MM/dd/yyyy"), l.Status);
            g.Rows[i].Tag = l;
        }
        g.CellClick += (s, e) =>
        {
            if (e.RowIndex < 0 || g.Columns[e.ColumnIndex].Name != "action") return;
            var l = (Loan)g.Rows[e.RowIndex].Tag;
            if (l.Status == "PENDING") { MessageBox.Show("Waiting for the librarian's approval.", "Pending"); return; }
            if (MessageBox.Show($"Return \"{l.Book.Title}\"?", "Return book", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            AppData.Return(l);
            ShowTab(true);
        };
        Tables.Fit(g);
        Tables.Done(g);
        var host = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        host.Controls.Add(g);
        return host;
    }
}

public class HistoryPage : PageBase
{
    readonly bool admin;
    readonly DataGridView g;
    readonly SearchBox search;

    public HistoryPage(bool admin) : base("History", admin ? "View, add, edit, and manage student accounts" : "Collection of your great deals")
    {
        this.admin = admin;
        search = new SearchBox(admin ? "Search books by title, author, student id,...." : "Search books by title, author, ....");
        search.Box.TextChanged += (s, e) => Reload();

        g = Tables.Create(36, true);
        g.Dock = DockStyle.Top;
        g.Columns.Add(Tables.Text("title", "Title", 110));
        g.Columns.Add(Tables.Text("author", "Author", 90));
        g.Columns.Add(Tables.Text("isbn", "ISBN", 100));
        if (admin) g.Columns.Add(Tables.Text("sid", "Student Id", 90));
        g.Columns.Add(Tables.Text("borrowed", "Borrowed", 70));
        g.Columns.Add(Tables.Text("returned", "Returned", 70));
        g.Columns.Add(Tables.Text("remark", "Remark", 70));
        g.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

        Body.Controls.Add(g);
        Body.Controls.Add(Ui.SearchWrap(search));
        Reload();
    }

    void Reload()
    {
        g.Rows.Clear();
        string q = search.Box.Text.Trim().ToLowerInvariant();
        foreach (var h in AppData.History)
        {
            if (!admin && h.StudentId != AppData.Me.Id) continue;
            if (q.Length > 0 && !(h.Book.Title + " " + h.Book.Author + " " + h.Book.Isbn + " " + h.StudentId).ToLowerInvariant().Contains(q)) continue;
            var vals = new List<object> { h.Book.Title, h.Book.Author, h.Book.Isbn };
            if (admin) vals.Add(h.StudentId);
            vals.Add(h.Borrowed.ToString("MM/dd/yy"));
            vals.Add(h.Returned.ToString("MM/dd/yy"));
            vals.Add(h.Remark);
            g.Rows.Add(vals.ToArray());
        }
        Tables.Fit(g);
        Tables.Done(g);
    }
}

public class AccountPage : PageBase
{
    readonly AvatarCircle avatar;
    readonly Label nmLabel, idLabel, emLabel;
    readonly TextBox txtFullName, txtStudentId, txtEmail, txtPassword, txtProgram, txtContact;
    readonly ComboBox cmbSection, cmbYear;
    readonly Button btnShowPassword, btnEdit;
    readonly Dictionary<Control, Label> errorLabels = new();
    bool editing = false;

    public AccountPage() : base("Manage Account", "Update your profile, change your password, and more.")
    {
        var me = AppData.Me;

        // --- profile card
        var profile = new RoundedPanel { Dock = DockStyle.Fill, Fill = Color.FromArgb(0xF3, 0xEC, 0xDD) };
        avatar = new AvatarCircle { Location = new Point(18, 12) };

        // Load avatar if exists
        if (!string.IsNullOrEmpty(me.AvatarPath))
        {
            var img = Database.LoadAvatar(me.AvatarPath);
            if (img != null) avatar.AvatarImage = img;
        }

        var change = Theme.Btn("Change Photo", Theme.Brown, Color.White, 84, 18, 7);
        change.Location = new Point(8, 74);
        change.Click += ChangePhoto_Click;

        string shortName = me.Name.Split(' ').Length >= 3 ? $"{me.Name.Split(' ')[0]} {me.Name.Split(' ')[1][0]}. {me.Name.Split(' ')[^1]}" : me.Name;
        nmLabel = Theme.Lbl(shortName, 13, FontStyle.Bold); nmLabel.Location = new Point(110, 14);
        idLabel = Theme.Lbl("Student ID: " + me.Id, 10, FontStyle.Bold); idLabel.Location = new Point(110, 42);
        emLabel = Theme.Lbl("Email: " + me.Email, 10, FontStyle.Bold); emLabel.Location = new Point(110, 62);
        var div = new Panel { Width = 1, BackColor = Theme.Border };
        var stTitle = Theme.Lbl("Account Status", 10, FontStyle.Bold);
        var chip = new Label { AutoSize = false, Size = new Size(110, 20), TextAlign = ContentAlignment.MiddleCenter, Font = Theme.F(8.5f), ForeColor = Color.White, Cursor = Cursors.Hand };
        void SetChip()
        {
            chip.Text = AppData.Suspended ? "Suspended" : "Active";
            chip.BackColor = AppData.Suspended ? Color.FromArgb(0xF0, 0x6A, 0x6A) : Color.FromArgb(0x7B, 0xC4, 0x7F);
        }
        SetChip();
        // Demo only: click the status chip to switch between the Active and Suspended designs.
        chip.Click += (s, e) => { AppData.Suspended = !AppData.Suspended; SetChip(); AppData.RaiseCartChanged(); };
        profile.Controls.AddRange(new Control[] { avatar, change, nmLabel, idLabel, emLabel, div, stTitle, chip });
        profile.Resize += (s, e) =>
        {
            int x = profile.Width - 210;
            div.SetBounds(x, 0, 1, profile.Height);
            stTitle.Location = new Point(x + (210 - stTitle.Width) / 2, 26);
            chip.Location = new Point(x + 50, 54);
        };

        // --- personal information
        var info = new RoundedPanel { Dock = DockStyle.Fill, Fill = Color.FromArgb(0xF3, 0xEC, 0xDD) };
        var head = Theme.Lbl("Personal Information", 12, FontStyle.Bold); head.Location = new Point(10, 6);
        info.Controls.Add(head);

        // Left column
        txtFullName = AddField(info, "Full Name", me.Name, 12, 38, 190, 96);
        txtStudentId = AddField(info, "Student ID", me.Id, 12, 68, 190, 96);
        txtEmail = AddField(info, "Email", me.Email, 12, 98, 250, 96);
        txtPassword = AddField(info, "Password", me.Password, 12, 128, 220, 96);
        txtPassword.UseSystemPasswordChar = true;   // masked on load and in view mode

        // Password show/hide button
        btnShowPassword = new Button
        {
            Text = "👁",
            Font = Theme.E(10),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(24, 22),
            Location = new Point(12 + 96 + 15 + 225, 128),
            Cursor = Cursors.Hand,
            BackColor = Color.FromArgb(0xC9, 0xB8, 0xA0),
            ForeColor = Theme.Ink,
            Visible = false
        };
        btnShowPassword.FlatAppearance.BorderSize = 0;
        btnShowPassword.Click += (s, e) => txtPassword.UseSystemPasswordChar = !txtPassword.UseSystemPasswordChar;
        info.Controls.Add(btnShowPassword);

        // Right column - split into Program, Section, Year
        AddLabel(info, "Program / Section / Year", 400, 38);
        txtProgram = AddFieldOnly(info, me.Program, 400, 60, 180);

        cmbSection = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(0xC9, 0xB8, 0xA0),
            ForeColor = Color.FromArgb(0x6A, 0x5A, 0x48),
            Font = Theme.F(9),
            Location = new Point(400, 90),
            Width = 86,
            Enabled = false
        };
        cmbSection.Items.AddRange(new[] { "A", "B", "C", "D", "E", "F" });
        if (!string.IsNullOrEmpty(me.Section) && cmbSection.Items.Contains(me.Section))
            cmbSection.SelectedItem = me.Section;
        else
            cmbSection.SelectedIndex = 0;
        info.Controls.Add(cmbSection);
        errorLabels[cmbSection] = AddErrorLabel(info, 400, 113);

        cmbYear = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(0xC9, 0xB8, 0xA0),
            ForeColor = Color.FromArgb(0x6A, 0x5A, 0x48),
            Font = Theme.F(9),
            Location = new Point(494, 90),
            Width = 86,
            Enabled = false
        };
        cmbYear.Items.AddRange(new[] { "1", "2", "3", "4" });
        var yrDigit = !string.IsNullOrEmpty(me.Year) && char.IsDigit(me.Year[0]) ? me.Year[0].ToString() : me.Year;
        if (!string.IsNullOrEmpty(yrDigit) && cmbYear.Items.Contains(yrDigit))
            cmbYear.SelectedItem = yrDigit;
        else
            cmbYear.SelectedIndex = 0;
        info.Controls.Add(cmbYear);
        errorLabels[cmbYear] = AddErrorLabel(info, 494, 113);

        txtContact = AddField(info, "Contact Number", me.Contact, 400, 125, 180, 115);

        btnEdit = Theme.Btn("✎  Edit Profile", Theme.Input, Theme.Ink, 110, 26, 8.5f);
        info.Controls.Add(btnEdit);
        info.Resize += (s, e) => btnEdit.Location = new Point(info.Width - btnEdit.Width - 14, info.Height - btnEdit.Height - 12);
        btnEdit.Click += Edit_Click;

        // --- help
        var help = new RoundedPanel { Dock = DockStyle.Fill, Fill = Color.FromArgb(0xEC, 0xE0, 0xC8) };
        var lockIcon = new Label { Text = "🔒", Font = Theme.E(16), ForeColor = Color.White, BackColor = Color.FromArgb(0x8A, 0x6E, 0x50), AutoSize = false, Size = new Size(40, 40), TextAlign = ContentAlignment.MiddleCenter, Location = new Point(12, 12) };
        var h1 = Theme.Lbl("Need help?", 10.5f, FontStyle.Bold); h1.Location = new Point(64, 10);
        var h2 = Theme.Lbl("If you notice any incorrect information or assistance, please contact the librarian or the staff", 8.5f, FontStyle.Bold); h2.Location = new Point(64, 32);
        help.Controls.AddRange(new Control[] { lockIcon, h1, h2 });

        Panel Wrap(Control c, int h) { var p = new Panel { Dock = DockStyle.Top, Height = h, Padding = new Padding(0, 0, 0, 12), BackColor = Color.Transparent }; p.Controls.Add(c); return p; }
        Body.Controls.Add(Wrap(help, 84));
        Body.Controls.Add(Wrap(info, 240));
        Body.Controls.Add(Wrap(profile, 112));
    }

    void AddLabel(Panel parent, string label, int x, int y)
    {
        var l = Theme.Lbl(label, 9.5f);
        l.Location = new Point(x, y + 3);
        parent.Controls.Add(l);
    }

    TextBox AddField(Panel parent, string label, string value, int x, int y, int w, int labelWidth)
    {
        AddLabel(parent, label, x, y);
        var t = AddFieldOnly(parent, value, x + labelWidth + 15, y, w);
        errorLabels[t] = AddErrorLabel(parent, x + labelWidth + 15, y + 23);
        return t;
    }

    TextBox AddFieldOnly(Panel parent, string value, int x, int y, int w)
    {
        var t = new TextBox
        {
            Text = value,
            ReadOnly = true,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(0xC9, 0xB8, 0xA0),
            ForeColor = Color.FromArgb(0x6A, 0x5A, 0x48),
            Font = Theme.F(9),
            Location = new Point(x, y),
            Width = w
        };
        parent.Controls.Add(t);
        errorLabels[t] = AddErrorLabel(parent, x, y + 23);
        return t;
    }

    Label AddErrorLabel(Panel parent, int x, int y)
    {
        var err = new Label
        {
            Location = new Point(x, y),
            AutoSize = true,
            ForeColor = Color.FromArgb(0xD9, 0x2D, 0x2D),
            Font = Theme.F(7.5f),
            BackColor = Color.Transparent,
            Visible = false
        };
        parent.Controls.Add(err);
        return err;
    }

    void ShowError(Control field, string message)
    {
        if (errorLabels.TryGetValue(field, out var label))
        {
            label.Text = message;
            label.Visible = !string.IsNullOrEmpty(message);
        }
    }

    void ClearErrors()
    {
        foreach (var label in errorLabels.Values)
            label.Visible = false;
    }

    void ChangePhoto_Click(object? sender, EventArgs e)
    {
        using var dialog = new ImageUploadDialog();
        if (dialog.ShowDialog(this) == DialogResult.OK && dialog.UploadedImage != null)
        {
            try
            {
                var me = AppData.Me;
                var avatarPath = Database.SaveAvatar(me.Id, dialog.UploadedImage);
                me.AvatarPath = avatarPath;
                Database.SaveStudent(me);

                avatar.AvatarImage = dialog.UploadedImage;
                avatar.Invalidate();
                MessageBox.Show("Profile photo updated successfully.", "Change Photo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save photo: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    void Edit_Click(object? sender, EventArgs e)
    {
        if (!editing)
        {
            // Enter edit mode
            editing = true;
            txtFullName.ReadOnly = false;
            txtStudentId.ReadOnly = false;
            txtEmail.ReadOnly = false;
            txtPassword.ReadOnly = false;
            txtPassword.UseSystemPasswordChar = true;
            btnShowPassword.Visible = true;
            txtProgram.ReadOnly = false;
            cmbSection.Enabled = true;
            cmbYear.Enabled = true;
            txtContact.ReadOnly = false;
            btnEdit.Text = "💾  Save";
        }
        else
        {
            // Save changes
            if (ValidateAndSave())
            {
                editing = false;
                txtFullName.ReadOnly = true;
                txtStudentId.ReadOnly = true;
                txtEmail.ReadOnly = true;
                txtPassword.ReadOnly = true;
                txtPassword.UseSystemPasswordChar = true;   // stay masked in view mode
                btnShowPassword.Visible = false;
                txtProgram.ReadOnly = true;
                cmbSection.Enabled = false;
                cmbYear.Enabled = false;
                txtContact.ReadOnly = true;
                btnEdit.Text = "✎  Edit Profile";

                // Update profile card
                var me = AppData.Me;
                string shortName = me.Name.Split(' ').Length >= 3 ? $"{me.Name.Split(' ')[0]} {me.Name.Split(' ')[1][0]}. {me.Name.Split(' ')[^1]}" : me.Name;
                nmLabel.Text = shortName;
                idLabel.Text = "Student ID: " + me.Id;
                emLabel.Text = "Email: " + me.Email;

                MessageBox.Show("Profile updated successfully.", "Manage Account", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }

    bool ValidateAndSave()
    {
        ClearErrors();
        bool valid = true;
        var me = AppData.Me;

        // Validate Full Name
        var nameResult = Validator.ValidateFullName(txtFullName.Text);
        if (!nameResult.valid)
        {
            ShowError(txtFullName, nameResult.error);
            valid = false;
        }

        // Validate Student ID
        var idResult = Validator.ValidateStudentId(txtStudentId.Text);
        if (!idResult.valid)
        {
            ShowError(txtStudentId, idResult.error);
            valid = false;
        }

        // Validate Email
        var emailResult = Validator.ValidateEmail(txtEmail.Text);
        if (!emailResult.valid)
        {
            ShowError(txtEmail, emailResult.error);
            valid = false;
        }

        // Validate Password
        var pwdResult = Validator.ValidatePassword(txtPassword.Text);
        if (!pwdResult.valid)
        {
            ShowError(txtPassword, pwdResult.error);
            valid = false;
        }

        // Validate Program
        var programResult = Validator.ValidateProgram(txtProgram.Text);
        if (!programResult.valid)
        {
            ShowError(txtProgram, programResult.error);
            valid = false;
        }

        // Validate Section (always valid from dropdown)

        // Validate Year (always valid from dropdown)

        // Validate Contact Number
        var contactResult = Validator.ValidateContactNumber(txtContact.Text);
        if (!contactResult.valid)
        {
            ShowError(txtContact, contactResult.error);
            valid = false;
        }

        if (!valid)
            return false;

        // Save to database
        string previousId = me.Id;
        me.Name = txtFullName.Text.Trim();
        me.Id = Validator.NormalizeStudentId(txtStudentId.Text.Trim());
        me.Email = txtEmail.Text.Trim();
        me.Password = txtPassword.Text;
        me.Program = txtProgram.Text.Trim();
        me.Section = cmbSection.SelectedItem?.ToString() ?? "A";
        me.Year = cmbYear.SelectedItem?.ToString() ?? "1";
        me.Contact = txtContact.Text.Trim();

        Database.SaveStudent(me, previousId);
        return true;
    }
}
