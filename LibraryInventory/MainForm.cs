namespace LibraryInventory;

public class MainForm : Form
{
    Control current;

    public MainForm()
    {
        Text = "Library Inventory System";
        ClientSize = new Size(1120, 690);
        MinimumSize = new Size(1020, 650);
        StartPosition = FormStartPosition.CenterScreen;
        Font = Theme.F(9);
        BackColor = Theme.Bg;
        Navigate(new AuthPage(this, false));
    }

    public void Navigate(Control next)
    {
        SuspendLayout();
        if (current != null)
        {
            Controls.Remove(current);
            current.Dispose();
        }
        current = next;
        next.Dock = DockStyle.Fill;
        Controls.Add(next);
        ResumeLayout();
    }
}

/// <summary>Log in and Sign up screens (same layout, different fields).</summary>
public class AuthPage : BackdropPanel
{
    public AuthPage(MainForm main, bool signup)
    {
        int cardH = signup ? 350 : 276;
        var stack = new Panel { Size = new Size(520, 126 + cardH), BackColor = Color.Transparent };

        var logo = Theme.Logo(44);
        logo.Location = new Point(238, 0);
        var title = new Label
        {
            Text = "Library Inventory System", Font = Theme.F(22, FontStyle.Bold), ForeColor = Theme.Ink,
            TextAlign = ContentAlignment.MiddleCenter, Bounds = new Rectangle(0, 50, 520, 40), BackColor = Color.Transparent
        };
        var sub = new Label
        {
            Text = signup ? "Create your account" : "Log in your account", Font = Theme.F(15), ForeColor = Theme.Ink,
            TextAlign = ContentAlignment.MiddleCenter, Bounds = new Rectangle(0, 88, 520, 30), BackColor = Color.Transparent
        };
        var card = new RoundedPanel { Bounds = new Rectangle(0, 126, 520, cardH), Fill = Theme.Card, Radius = 6 };

        Label L(string t, int y)
        {
            var l = Theme.Lbl(t, 11, FontStyle.Bold);
            l.Location = new Point(40, y);
            return l;
        }

        var user = new InputField("Enter your username") { Bounds = new Rectangle(40, 46, 440, 36) };
        var pass = new InputField("Enter your password", true) { Bounds = new Rectangle(40, 122, 440, 36) };
        card.Controls.Add(L("Username", 20));
        card.Controls.Add(user);
        card.Controls.Add(L("Password", 96));
        card.Controls.Add(pass);

        InputField confirm = null;
        int by = 184;
        if (signup)
        {
            confirm = new InputField("Re - enter your password", true) { Bounds = new Rectangle(40, 198, 440, 36) };
            card.Controls.Add(L("Confirm Password", 172));
            card.Controls.Add(confirm);
            by = 256;
        }

        var go = Theme.Btn(signup ? "Create Account" : "Log in", Theme.Green, Color.White, 440, 34, 11);
        go.Location = new Point(40, by);
        card.Controls.Add(go);

        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Color.Transparent };
        var q = Theme.Lbl(signup ? "Already have an account? " : "Don't have an account? ", 10.5f, FontStyle.Bold);
        var link = Theme.Lbl(signup ? "Log in" : "Sign up", 10.5f, FontStyle.Bold, Color.FromArgb(0x1B, 0x4D, 0x1F));
        link.Cursor = Cursors.Hand;
        q.Margin = new Padding(0);
        link.Margin = new Padding(0);
        row.Controls.Add(q);
        row.Controls.Add(link);
        card.Controls.Add(row);
        row.Location = new Point((520 - row.PreferredSize.Width) / 2, by + 46);

        link.Click += (s, e) => main.Navigate(new AuthPage(main, !signup));

        go.Click += (s, e) =>
        {
            try
            {
                string name = user.Box.Text.Trim();
                if (name.Length == 0 || pass.Box.Text.Length == 0)
                {
                    MessageBox.Show("Please fill in all fields.", "Library Inventory System");
                    return;
                }

                if (!AppData.Initialize())
                {
                    MessageBox.Show(
                        "Could not load the library database." + Environment.NewLine + Environment.NewLine +
                        (AppData.InitError?.ToString() ?? "Unknown error") + Environment.NewLine + Environment.NewLine +
                        "Run database/00_install.sql from the repository root, then try again.",
                        "Library Inventory System", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (signup)
                {
                    if (pass.Box.Text != confirm.Box.Text)
                    {
                        MessageBox.Show("Passwords do not match.", "Library Inventory System");
                        return;
                    }

                    var idCheck = Validator.ValidateStudentId(name);
                    if (!idCheck.valid)
                    {
                        MessageBox.Show(idCheck.error, "Library Inventory System");
                        return;
                    }

                    var passwordCheck = Validator.ValidatePassword(pass.Box.Text);
                    if (!passwordCheck.valid)
                    {
                        MessageBox.Show(passwordCheck.error, "Library Inventory System");
                        return;
                    }

                    string studentId = Validator.NormalizeStudentId(name);
                    if (Database.StudentExists(studentId))
                    {
                        MessageBox.Show("An account with this Student ID already exists.", "Library Inventory System");
                        return;
                    }

                    var created = new Student
                    {
                        Id = studentId,
                        Name = $"Student {studentId}",
                        Email = $"{studentId.ToLowerInvariant().Replace("-", "")}@library.edu",
                        Password = pass.Box.Text,
                        Program = "BS Computer Science",
                        Year = "1",
                        Section = "A",
                        Contact = "",
                        Joined = DateTime.Today,
                        Active = true
                    };
                    Database.SaveStudent(created);

                    MessageBox.Show("Account created. You can now log in.", "Library Inventory System");
                    main.Navigate(new AuthPage(main, false));
                    return;
                }

                AppData.UserName = name;
                var admin = Database.GetAdmin(name, pass.Box.Text);
                if (admin != null)
                {
                    main.Navigate(new Shell(main, true));
                }
                else
                {
                    var student = Database.GetStudent(name, pass.Box.Text);
                    if (student != null && student.Active)
                    {
                        AppData.SignIn(student);

                        // Student assistants get the full admin workspace; regular students
                        // get the student account screens.
                        if (student.IsSa)
                            main.Navigate(new Shell(main, true));
                        else
                            main.Navigate(new Shell(main, false));
                    }
                    else if (student != null)
                    {
                        MessageBox.Show("Your account is suspended. Please contact the librarian.", "Library Inventory System");
                    }
                    else
                    {
                        MessageBox.Show("Invalid username or password.", "Library Inventory System");
                    }
                }
            }
            catch (Exception ex)
            {
                // Include the InnerException: that is where the real database error lives.
                string detail = ex.InnerException != null ? Environment.NewLine + ex.InnerException.Message : "";
                MessageBox.Show("Login error: " + ex.Message + detail, "Library Inventory System", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        stack.Controls.Add(logo);
        stack.Controls.Add(title);
        stack.Controls.Add(sub);
        stack.Controls.Add(card);
        Controls.Add(stack);
        Resize += (s, e) => stack.Location = new Point(Math.Max(0, (Width - stack.Width) / 2), Math.Max(8, (Height - stack.Height) / 2));
    }
}
