namespace LibraryInventory;

/// <summary>Sidebar + content area shared by the student and admin screens.</summary>
public class Shell : Panel
{
    readonly MainForm main;
    readonly bool admin;
    readonly BackdropPanel host = new BackdropPanel { Dock = DockStyle.Fill };
    readonly List<NavItem> items = new();
    readonly CartFab fab;
    Control page;

    public Shell(MainForm main, bool admin)
    {
        this.main = main;
        this.admin = admin;
        Dock = DockStyle.Fill;

        var sidebar = new Panel { Dock = DockStyle.Left, Width = 215, BackColor = Theme.Sidebar };
        sidebar.Paint += (s, e) =>
        {
            using var p = new Pen(Theme.Border);
            e.Graphics.DrawLine(p, sidebar.Width - 1, 0, sidebar.Width - 1, sidebar.Height);
        };
        Controls.Add(host);
        Controls.Add(sidebar);

        // header
        var logo = Theme.Logo(46);
        logo.Location = new Point(14, 14);
        var name = Theme.Lbl("Library Inventory\nSystem", 12, FontStyle.Bold);
        name.Location = new Point(66, 14);
        var line = new Panel { Bounds = new Rectangle(14, 72, 188, 1), BackColor = Color.FromArgb(0x9A, 0x8C, 0x78) };
        var initials = Theme.Lbl(AppData.Initials(AppData.UserName), 12, FontStyle.Bold);
        initials.Location = new Point(14, 88);
        var welcome = Theme.Lbl($"Welcome, {AppData.UserName}!", 10.5f, FontStyle.Bold);
        welcome.Location = new Point(52, 90);
        var role = Theme.Lbl(admin ? "Admin Menu" : "Student Account", 10, FontStyle.Bold);
        role.Location = new Point(14, 128);
        foreach (Control c in new Control[] { logo, name, line, initials, welcome, role }) sidebar.Controls.Add(c);

        // nav
        var defs = admin
            ? new[] { ("▦", "Dashboard", "dashboard"), ("📖", "Manage Books", "books"), ("👤", "Manage Students", "students"), ("📋", "Requests", "requests"), ("🕘", "History", "history") }
            : new[] { ("📚", "Browse Books", "browse"), ("📖", "Book Cart", "cart"), ("🕘", "History", "history"), ("👤", "Manage Account", "account") };
        int y = 158;
        foreach (var (glyph, text, key) in defs)
        {
            var it = new NavItem(glyph, text, key) { Location = new Point(0, y) };
            it.Click += (s, e) => Go(key);
            sidebar.Controls.Add(it);
            items.Add(it);
            y += 46;
        }

        // logout
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 78 };
        bottom.Paint += (s, e) =>
        {
            using var p = new Pen(Color.FromArgb(0x9A, 0x8C, 0x78));
            e.Graphics.DrawLine(p, 14, 6, 202, 6);
        };
        var logout = new NavItem("➜", "Log Out", "logout") { Location = new Point(0, 18) };
        logout.Click += (s, e) => main.Navigate(new AuthPage(main, false));
        bottom.Controls.Add(logout);
        sidebar.Controls.Add(bottom);

        if (!admin)
        {
            fab = new CartFab();
            fab.Click += (s, e) => Go("cart");
            host.Controls.Add(fab);
            host.Resize += (s, e) => fab.Location = new Point(host.Width - fab.Width - 14, host.Height - fab.Height - 12);
            AppData.CartChanged += fab.Invalidate;
        }

        Go(admin ? "dashboard" : "browse");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && fab != null) AppData.CartChanged -= fab.Invalidate;
        base.Dispose(disposing);
    }

    public void RefreshBadge()
    {
        foreach (var it in items)
            if (it.Key == "requests") { it.Badge = AppData.PendingCount; it.Invalidate(); }
    }

    public void Go(string key)
    {
        if (page != null)
        {
            host.Controls.Remove(page);
            page.Dispose();
        }
        Control p = key switch
        {
            "browse" => new BrowseBooksPage(this),
            "cart" => new BookCartPage(this),
            "history" => new HistoryPage(admin),
            "account" => new AccountPage(),
            "dashboard" => new AdminDashboardPage(this),
            "books" => new ManageBooksPage(this),
            "students" => new ManageStudentsPage(),
            "requests" => new RequestsPage(this),
            _ => new Panel()
        };
        p.Dock = DockStyle.Fill;
        page = p;
        host.Controls.Add(p);
        fab?.BringToFront();
        foreach (var it in items)
        {
            it.IsSelected = it.Key == key;
            it.Invalidate();
        }
        RefreshBadge();
    }
}

public abstract class PageBase : Panel
{
    protected readonly Panel Header;
    protected readonly Panel Body;

    protected PageBase(string title, string subtitle)
    {
        Dock = DockStyle.Fill;
        BackColor = Color.Transparent;
        Padding = new Padding(22, 14, 22, 14);
        Body = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        Header = new Panel { Dock = DockStyle.Top, Height = 66, BackColor = Color.Transparent };
        var t = Theme.Lbl(title, 22, FontStyle.Bold);
        t.Location = new Point(0, 0);
        var s = Theme.Lbl(subtitle, 9.5f, FontStyle.Bold);
        s.Location = new Point(3, 42);
        Header.Controls.Add(t);
        Header.Controls.Add(s);
        Controls.Add(Body);
        Controls.Add(Header);
    }
}
