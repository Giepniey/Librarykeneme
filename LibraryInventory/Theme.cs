using System.Drawing.Drawing2D;

namespace LibraryInventory;

/// <summary>Colors, fonts and small factory helpers shared by every screen.</summary>
public static class Theme
{
    public static readonly Color Bg = Color.FromArgb(0xEF, 0xE6, 0xD5);
    public static readonly Color Sidebar = Color.FromArgb(0xEE, 0xE4, 0xD2);
    public static readonly Color Selected = Color.FromArgb(0xC4, 0xA9, 0x8A);
    public static readonly Color Card = Color.FromArgb(0xF5, 0xF1, 0xE6);
    public static readonly Color CardDark = Color.FromArgb(0xF1, 0xE9, 0xD9);
    public static readonly Color Input = Color.FromArgb(0xE8, 0xDC, 0xC8);
    public static readonly Color Border = Color.FromArgb(0xD8, 0xCB, 0xB4);
    public static readonly Color Ink = Color.FromArgb(0x3A, 0x2E, 0x22);
    public static readonly Color Muted = Color.FromArgb(0x8A, 0x7B, 0x68);
    public static readonly Color Green = Color.FromArgb(0x2E, 0x7D, 0x3A);
    public static readonly Color Brown = Color.FromArgb(0x9C, 0x7E, 0x5E);
    public static readonly Color RowAlt = Color.FromArgb(0xEE, 0xE3, 0xCE);
    public static readonly Color SearchFill = Color.FromArgb(0xF7, 0xF3, 0xEA);

    public static Font F(float size, FontStyle style = FontStyle.Regular) => new Font("Segoe UI", size, style);
    public static Font E(float size) => new Font("Segoe UI Emoji", size);

    public static Label Lbl(string text, float size, FontStyle style = FontStyle.Regular, Color? color = null) => new Label
    {
        Text = text,
        Font = F(size, style),
        ForeColor = color ?? Ink,
        AutoSize = true,
        BackColor = Color.Transparent
    };

    public static Button Btn(string text, Color back, Color fore, int w, int h, float size = 9.5f)
    {
        var b = new Button
        {
            Text = text, BackColor = back, ForeColor = fore, Size = new Size(w, h),
            FlatStyle = FlatStyle.Flat, Font = F(size, FontStyle.Bold), Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        b.FlatAppearance.BorderSize = 0;
        return b;
    }

    public static GraphicsPath Round(Rectangle r, int radius)
    {
        var p = new GraphicsPath();
        int d = Math.Max(1, radius * 2);
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static RoundedPanel Logo(int size)
    {
        var p = new RoundedPanel { Size = new Size(size, size), Fill = Color.FromArgb(0xE2, 0xD5, 0xB0), Stroke = Color.FromArgb(0xD0, 0xC0, 0x95), Radius = 4 };
        var g = new Label
        {
            Text = "📖", Font = E(size * 0.4f), ForeColor = Color.FromArgb(0xB8, 0x92, 0x2E),
            Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent
        };
        p.Controls.Add(g);
        return p;
    }
}

/// <summary>Panel with rounded corners, a fill and a thin border.</summary>
public class RoundedPanel : Panel
{
    public int Radius { get; set; } = 8;
    public Color Fill { get; set; } = Theme.Card;
    public Color Stroke { get; set; } = Theme.Border;

    public RoundedPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Theme.Round(new Rectangle(0, 0, Width - 1, Height - 1), Radius);
        using var b = new SolidBrush(Fill);
        using var p = new Pen(Stroke);
        e.Graphics.FillPath(b, path);
        e.Graphics.DrawPath(p, path);
    }
}

/// <summary>Beige gradient with faint vertical "book spine" bars, standing in for the library photo.</summary>
public class BackdropPanel : Panel
{
    public BackdropPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width < 2 || Height < 2) return;
        var g = e.Graphics;
        using (var br = new LinearGradientBrush(ClientRectangle, Color.FromArgb(0xF4, 0xEC, 0xDD), Color.FromArgb(0xE8, 0xDC, 0xC6), 90f))
            g.FillRectangle(br, ClientRectangle);
        var rnd = new Random(7);
        for (int x = 0; x < Width; x += 5)
        {
            int h = rnd.Next(Height / 6, Height / 2);
            using var pen = new Pen(Color.FromArgb(rnd.Next(10, 26), 0x8A, 0x6E, 0x4A), rnd.Next(1, 3));
            g.DrawLine(pen, x, Height - h, x, Height);
        }
    }
}

public class SearchBox : RoundedPanel
{
    public readonly TextBox Box = new TextBox();

    public SearchBox(string placeholder)
    {
        Height = 38;
        Fill = Theme.SearchFill;
        Radius = 8;
        Box.BorderStyle = BorderStyle.None;
        Box.Font = Theme.F(10);
        Box.BackColor = Fill;
        Box.ForeColor = Theme.Ink;
        Box.PlaceholderText = placeholder;
        var icon = new Label { Text = "🔍", Font = Theme.E(11), ForeColor = Theme.Muted, AutoSize = true, BackColor = Color.Transparent };
        Controls.Add(Box);
        Controls.Add(icon);
        Resize += (s, e) =>
        {
            Box.SetBounds(14, (Height - Box.Height) / 2, Math.Max(10, Width - 56), Box.Height);
            icon.Location = new Point(Width - icon.Width - 12, (Height - icon.Height) / 2);
        };
    }
}

public class InputField : RoundedPanel
{
    public readonly TextBox Box = new TextBox();

    public InputField(string placeholder, bool password = false)
    {
        Height = 36;
        Fill = Theme.Input;
        Stroke = Theme.Input;
        Radius = 3;
        Box.BorderStyle = BorderStyle.None;
        Box.Font = Theme.F(9.5f, FontStyle.Bold);
        Box.BackColor = Fill;
        Box.ForeColor = Theme.Ink;
        Box.PlaceholderText = placeholder;
        Box.UseSystemPasswordChar = password;
        Controls.Add(Box);
        Resize += (s, e) => Box.SetBounds(34, (Height - Box.Height) / 2, Math.Max(10, Width - 46), Box.Height);
    }
}
