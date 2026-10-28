using System.Drawing.Drawing2D;

namespace LibraryInventory;

/// <summary>Draws simple generated book covers (the screenshots' cover art is not available as separate assets).</summary>
public static class CoverFactory
{
    static readonly Dictionary<string, Bitmap> cache = new();

    public static Bitmap Make(Book b, int w, int h)
    {
        if (b == null) return new Bitmap(w, h);
        string key = $"{b.Title}|{b.Isbn}|{b.CoverPath}|{w}x{h}";
        if (cache.TryGetValue(key, out var hit)) return hit;

        // Uploaded cover takes precedence over the generated placeholder.
        if (!string.IsNullOrEmpty(b.CoverPath))
        {
            try
            {
                using var img = Image.FromFile(b.CoverPath);
                var scaled = new Bitmap(img, w, h);
                cache[key] = scaled;
                return scaled;
            }
            catch { /* fall through to generated cover */ }
        }

        var bmp = new Bitmap(w, h);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using (var br = new LinearGradientBrush(new Rectangle(0, 0, w, h), b.CoverColor, ControlPaint.Dark(b.CoverColor, 0.2f), 90f))
                g.FillRectangle(br, 0, 0, w, h);
            var fg = b.CoverColor.GetBrightness() > 0.6f ? Color.FromArgb(0x30, 0x28, 0x20) : Color.White;
            using var f = new Font("Georgia", Math.Max(5f, w / 9f), FontStyle.Bold);
            using var sb = new SolidBrush(fg);
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(b.Title.Split(':')[0], f, sb, new RectangleF(4, 4, w - 8, h - 8), sf);
            using var pen = new Pen(Color.FromArgb(70, 0, 0, 0));
            g.DrawRectangle(pen, 0, 0, w - 1, h - 1);
        }
        cache[key] = bmp;
        return bmp;
    }
}

/// <summary>Drag & drop book cover picker for the Add/Edit Book dialogs.
/// Light gray area with a cloud-upload icon, "Drag & drop" text and an underlined browse link.
/// Clicking anywhere on the box also opens the file picker. Accepts PNG/JPG/JPEG only.
/// When a cover exists it shows as a preview with a Delete Cover button; the preview
/// itself also accepts drops so the cover can be replaced directly.</summary>
public class CoverDropZone : Panel
{
    readonly Panel zone;
    readonly Label cloudIcon, dragText;
    readonly LinkLabel browseLink;
    readonly FlowLayoutPanel browseRow;
    readonly PictureBox preview;
    readonly Button deleteBtn;
    readonly int w;
    public Image CoverImage { get; private set; }
    public bool Changed { get; private set; }
    /// <summary>True when the user chose Delete Cover in the dialog (applied on Save).</summary>
    public bool Cleared { get; private set; }

    static readonly string[] AllowedExtensions = { ".png", ".jpg", ".jpeg" };

    public CoverDropZone(int width, int height, string existingPath)
    {
        w = width;
        Size = new Size(width, height);
        BackColor = Color.Transparent;

        zone = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(0xF5, 0xF5, 0xF5),
            AllowDrop = true,
            Cursor = Cursors.Hand
        };
        // Clicking anywhere on the gray box opens the file picker too.
        zone.Click += (s, e) => Browse();

        cloudIcon = new Label
        {
            Text = "☁",
            Font = Theme.E(38),
            ForeColor = Color.FromArgb(0x8A, 0x7B, 0x68),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        cloudIcon.Location = new Point((width - cloudIcon.PreferredWidth) / 2, 14);

        dragText = Theme.Lbl("Drag & drop", 11, FontStyle.Bold);
        dragText.AutoSize = true;
        dragText.Location = new Point((width - dragText.PreferredWidth) / 2, 62);

        var orText = new Label
        {
            Text = "your book cover here, or ",
            Font = Theme.F(9),
            ForeColor = Theme.Muted,
            AutoSize = true,
            BackColor = Color.Transparent
        };
        browseLink = new LinkLabel
        {
            Text = "browse",
            Font = Theme.F(9, FontStyle.Bold),
            LinkColor = Theme.Brown,
            ActiveLinkColor = Theme.Brown,
            VisitedLinkColor = Theme.Brown,
            LinkBehavior = LinkBehavior.HoverUnderline,
            AutoSize = true,
            BackColor = Color.Transparent
        };
        browseLink.Click += (s, e) => Browse();

        browseRow = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        orText.Margin = new Padding(0);
        browseLink.Margin = new Padding(0);
        browseRow.Controls.Add(orText);
        browseRow.Controls.Add(browseLink);

        zone.Controls.AddRange(new Control[] { cloudIcon, dragText, browseRow });
        zone.DragEnter += Zone_DragEnter;
        zone.DragDrop += Zone_DragDrop;
        // Labels sit on top of the zone, so clicks on them don't reach the zone —
        // route them to the file picker too ("click anywhere on the box").
        foreach (var c in new Control[] { cloudIcon, dragText, browseRow, orText })
            c.Click += (s, e) => Browse();
        Controls.Add(zone);

        // Preview thumbnail: shown next to the instructions when a cover exists.
        // It also accepts drops so the user can drag a new image onto the preview itself.
        preview = new PictureBox
        {
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.White,
            AllowDrop = true,
            Visible = false
        };
        preview.DragEnter += Zone_DragEnter;
        preview.DragDrop += Zone_DragDrop;
        zone.Controls.Add(preview);

        // Delete Cover button under the preview thumbnail (Edit dialog only shows it
        // when a cover exists; the confirmation happens on click, deletion on Save).
        deleteBtn = Theme.Btn("🗑 Delete Cover", Color.FromArgb(0xE0, 0x1B, 0x1B), Color.White, 110, 24, 8);
        deleteBtn.Visible = false;
        deleteBtn.Click += (s, e) =>
        {
            if (MessageBox.Show("Are you sure you want to delete this book cover?",
                    "Delete Book Cover", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                Cleared = true;
                CoverImage?.Dispose();
                CoverImage = null;
                preview.Image = null;
                LayoutZone(false);
            }
        };
        Controls.Add(deleteBtn);

        LayoutZone(false);

        if (!string.IsNullOrEmpty(existingPath))
        {
            var img = Database.LoadImage(existingPath);
            if (img != null) SetImage(img);
        }
    }

    /// <summary>Hides the Delete Cover button (used by the Add Book dialog, which has nothing to delete).</summary>
    public void HideDeleteButton() { deleteBtn.Visible = false; deleteBtn.Hide(); }

    /// <summary>Positions the preview thumbnail, its Delete button, and the instructions.
    /// No preview: instructions centered. With preview: thumbnail on the left with the
    /// Delete button under it, instructions (cloud icon, Drag & drop, browse) centered
    /// in the remaining space so the existing cover can still be replaced by drop or browse.</summary>
    void LayoutZone(bool hasPreview)
    {
        if (!hasPreview)
        {
            preview.Visible = false;
            deleteBtn.Visible = false;
            cloudIcon.Location = new Point((w - cloudIcon.PreferredWidth) / 2, 14);
            dragText.Location = new Point((w - dragText.PreferredWidth) / 2, 62);
            browseRow.Location = new Point((w - browseRow.PreferredSize.Width) / 2, 86);
        }
        else
        {
            preview.SetBounds(10, 10, 80, 110);
            preview.Visible = true;
            deleteBtn.SetBounds(4, 124, 92, 24);
            deleteBtn.Visible = true;
            int left = 104;   // space taken by the thumbnail + delete button column
            int right = w - left;
            cloudIcon.Location = new Point(left + (right - cloudIcon.PreferredWidth) / 2, 10);
            dragText.Location = new Point(left + (right - dragText.PreferredWidth) / 2, 56);
            browseRow.Location = new Point(left + (right - browseRow.PreferredSize.Width) / 2, 80);
        }
    }

    void Zone_DragEnter(object? sender, DragEventArgs e)
    {
        e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Copy : DragDropEffects.None;
    }

    void Zone_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            LoadFromFile(files[0]);
    }

    void Browse()
    {
        using var ofd = new OpenFileDialog { Filter = "Image Files|*.png;*.jpg;*.jpeg", Title = "Select Book Cover" };
        if (ofd.ShowDialog(FindForm()) == DialogResult.OK)
            LoadFromFile(ofd.FileName);
    }

    void LoadFromFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
        {
            MessageBox.Show("Please select a valid image file (PNG, JPG, or JPEG).", "Invalid File Type",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        try
        {
            SetImage(Database.LoadImage(path));
            Changed = true;
        }
        catch
        {
            MessageBox.Show("Failed to load the image. Please try another file.", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    void SetImage(Image img)
    {
        Cleared = false;
        CoverImage?.Dispose();
        CoverImage = img;
        preview.Image = img;
        LayoutZone(img != null);
    }
}

/// <summary>Styled DataGridView factory plus pill-shaped status chips.</summary>
public static class Tables
{    static readonly Font ChipFont = Theme.F(8f, FontStyle.Bold);

    public static DataGridView Create(int rowHeight = 34, bool darkGrid = false)
    {
        var g = new DataGridView
        {
            BackgroundColor = Theme.Card,
            BorderStyle = BorderStyle.FixedSingle,
            CellBorderStyle = darkGrid ? DataGridViewCellBorderStyle.Single : DataGridViewCellBorderStyle.SingleHorizontal,
            GridColor = darkGrid ? Color.FromArgb(0x55, 0x48, 0x3A) : Theme.Border,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AllowUserToResizeColumns = false,
            ReadOnly = true,
            MultiSelect = false,
            RowHeadersVisible = false,
            EnableHeadersVisualStyles = false,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersHeight = 34,
            ColumnHeadersBorderStyle = darkGrid ? DataGridViewHeaderBorderStyle.Single : DataGridViewHeaderBorderStyle.None,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            Font = Theme.F(9),
            ScrollBars = ScrollBars.Vertical
        };
        g.RowTemplate.Height = rowHeight;
        g.ColumnHeadersDefaultCellStyle.BackColor = darkGrid ? Theme.Card : Theme.RowAlt;
        g.ColumnHeadersDefaultCellStyle.ForeColor = Theme.Ink;
        g.ColumnHeadersDefaultCellStyle.SelectionBackColor = g.ColumnHeadersDefaultCellStyle.BackColor;
        g.ColumnHeadersDefaultCellStyle.SelectionForeColor = Theme.Ink;
        g.ColumnHeadersDefaultCellStyle.Font = Theme.F(9, FontStyle.Bold);
        g.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        g.DefaultCellStyle.BackColor = Theme.Card;
        g.DefaultCellStyle.ForeColor = Theme.Ink;
        g.DefaultCellStyle.SelectionBackColor = Color.FromArgb(0xE3, 0xD3, 0xB5);
        g.DefaultCellStyle.SelectionForeColor = Theme.Ink;
        g.DefaultCellStyle.Padding = new Padding(4, 0, 4, 0);
        g.AlternatingRowsDefaultCellStyle.BackColor = Theme.RowAlt;
        g.CellPainting += PaintChip;
        return g;
    }

    public static DataGridViewTextBoxColumn Text(string name, string header, int weight = 100) => new DataGridViewTextBoxColumn
    {
        Name = name, HeaderText = header, FillWeight = weight, SortMode = DataGridViewColumnSortMode.NotSortable
    };

    /// <summary>Soft pill (Active / Inactive / Pending ...).</summary>
    public static DataGridViewTextBoxColumn Chip(string name, string header, int weight = 80)
    {
        var c = Text(name, header, weight);
        c.Tag = "chip";
        c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        return c;
    }

    /// <summary>Solid rectangular badge (RETURN / PENDING / OVERDUE).</summary>
    public static DataGridViewTextBoxColumn SolidChip(string name, string header, int weight = 80)
    {
        var c = Text(name, header, weight);
        c.Tag = "solidchip";
        c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        return c;
    }

    public static DataGridViewButtonColumn Btn(string name, string header, string text, Color back, Color fore, int weight = 40, float fontSize = 8.5f)
    {
        var c = new DataGridViewButtonColumn
        {
            Name = name, HeaderText = header, Text = text, UseColumnTextForButtonValue = true,
            FlatStyle = FlatStyle.Flat, FillWeight = weight, SortMode = DataGridViewColumnSortMode.NotSortable
        };
        c.DefaultCellStyle.BackColor = back;
        c.DefaultCellStyle.ForeColor = fore;
        c.DefaultCellStyle.SelectionBackColor = back;
        c.DefaultCellStyle.SelectionForeColor = fore;
        c.DefaultCellStyle.Font = text.Length <= 2 ? Theme.E(fontSize + 1) : Theme.F(fontSize, FontStyle.Bold);
        c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        return c;
    }

    public static (Color, Color) ChipColors(string text, bool solid)
    {
        string t = (text ?? "").ToUpperInvariant();
        if (solid)
            return t switch
            {
                "RETURN" => (Color.FromArgb(0x1E, 0x7A, 0x2E), Color.White),
                "PENDING" => (Color.FromArgb(0xC9, 0x71, 0x0F), Color.White),
                "OVERDUE" => (Color.FromArgb(0x9C, 0x1C, 0x14), Color.White),
                _ => (Theme.Brown, Color.White)
            };
        if (t == "ACTIVE" || t == "ON TIME" || t == "APPROVED" || t.StartsWith("AVAILABLE"))
            return (Color.FromArgb(0xCD, 0xE8, 0xCB), Theme.Green);
        if (t == "INACTIVE" || t == "LATE" || t == "DECLINED" || t == "REJECTED" || t == "BORROWED" || t == "OVERDUE")
            return (Color.FromArgb(0xF4, 0xC7, 0xC7), Color.FromArgb(0xA0, 0x20, 0x20));
        return (Color.FromArgb(0xF6, 0xDD, 0xB0), Color.FromArgb(0xB0, 0x6A, 0x10));
    }

    static void PaintChip(object sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
        var g = (DataGridView)sender;
        var tag = g.Columns[e.ColumnIndex].Tag as string;
        if (tag != "chip" && tag != "solidchip") return;

        e.Paint(e.CellBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.Border);
        string text = Convert.ToString(e.Value) ?? "";
        if (text.Length > 0)
        {
            bool solid = tag == "solidchip";
            var (bk, fg) = ChipColors(text, solid);
            int w = TextRenderer.MeasureText(text, ChipFont).Width + 16, h = 20;
            var r = new Rectangle(e.CellBounds.X + (e.CellBounds.Width - w) / 2, e.CellBounds.Y + (e.CellBounds.Height - h) / 2, w, h);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = Theme.Round(r, solid ? 3 : 9);
            using var br = new SolidBrush(bk);
            e.Graphics.FillPath(br, path);
            TextRenderer.DrawText(e.Graphics, text, ChipFont, r, fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
        e.Handled = true;
    }

    /// <summary>Call after filling a grid.</summary>
    public static void Done(DataGridView g)
    {
        g.ClearSelection();
        g.CurrentCell = null;
    }

    /// <summary>Make a docked-top grid exactly as tall as its content (like the mock-ups).</summary>
    public static void Fit(DataGridView g)
    {
        g.Height = g.ColumnHeadersHeight + g.Rows.Count * g.RowTemplate.Height + 3;
    }
}

public static class StudentYearHelper
{
    public static string ToStandardDisplay(string y)
    {
        if (string.IsNullOrWhiteSpace(y)) return "1st Year";
        var t = y.Trim();
        if (t.StartsWith("1") || t.Equals("First Year", StringComparison.OrdinalIgnoreCase)) return "1st Year";
        if (t.StartsWith("2") || t.Equals("Second Year", StringComparison.OrdinalIgnoreCase)) return "2nd Year";
        if (t.StartsWith("3") || t.Equals("Third Year", StringComparison.OrdinalIgnoreCase)) return "3rd Year";
        if (t.StartsWith("4") || t.Equals("Fourth Year", StringComparison.OrdinalIgnoreCase)) return "4th Year";
        return t;
    }

    public static bool Matches(string studentYear, string filterYear)
    {
        if (string.IsNullOrEmpty(filterYear) || filterYear.StartsWith("All", StringComparison.OrdinalIgnoreCase))
            return true;
        return string.Equals(ToStandardDisplay(studentYear), ToStandardDisplay(filterYear), StringComparison.OrdinalIgnoreCase);
    }
}

public record Field(string Label, string Value, string[] Choices = null);

public static class PromptDialog
{
    /// <summary>Small modal form without a book cover section (used for student forms etc.).</summary>
    public static string[] Show(IWin32Window owner, string title, params Field[] fields)
    {
        int h = 20 + fields.Length * 58 + 64;
        using var f = new Form
        {
            Text = title, FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false, MinimizeBox = false, ClientSize = new Size(380, h), BackColor = Theme.Card, Font = Theme.F(9.5f)
        };
        var inputs = new List<Control>();
        int y = 14;
        foreach (var fd in fields)
        {
            f.Controls.Add(new Label { Text = fd.Label, Left = 20, Top = y, AutoSize = true, ForeColor = Theme.Ink });
            Control c;
            if (fd.Choices != null)
            {
                var cb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Left = 20, Top = y + 22, Width = 340 };
                cb.Items.AddRange(fd.Choices);
                cb.SelectedItem = fd.Value;
                if (cb.SelectedIndex < 0 && cb.Items.Count > 0) cb.SelectedIndex = 0;
                c = cb;
            }
            else c = new TextBox { Left = 20, Top = y + 22, Width = 340, Text = fd.Value };
            f.Controls.Add(c);
            inputs.Add(c);
            y += 58;
        }

        var ok = Theme.Btn("Save", Theme.Green, Color.White, 90, 32);
        ok.Location = new Point(170, y + 6);
        ok.DialogResult = DialogResult.OK;
        var cancel = Theme.Btn("Cancel", Theme.Brown, Color.White, 90, 32);
        cancel.Location = new Point(270, y + 6);
        cancel.DialogResult = DialogResult.Cancel;
        f.Controls.Add(ok);
        f.Controls.Add(cancel);
        f.AcceptButton = ok;
        f.CancelButton = cancel;
        if (f.ShowDialog(owner) != DialogResult.OK) return null;
        return inputs.Select(c => c.Text).ToArray();
    }

    /// <summary>Dialog with a drag & drop book cover section (used specifically for Book forms).</summary>
    public static string[] ShowWithCover(IWin32Window owner, string title, out CoverDropZone coverZone, string existingCoverPath, bool showDelete = true, params Field[] fields)
    {
        int h = 20 + fields.Length * 58 + 64 + 180;   // + room for the Book Cover section (zone + delete button)
        using var f = new Form
        {
            Text = title, FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false, MinimizeBox = false, ClientSize = new Size(380, h), BackColor = Theme.Card, Font = Theme.F(9.5f)
        };
        var inputs = new List<Control>();
        int y = 14;
        foreach (var fd in fields)
        {
            f.Controls.Add(new Label { Text = fd.Label, Left = 20, Top = y, AutoSize = true, ForeColor = Theme.Ink });
            Control c;
            if (fd.Choices != null)
            {
                var cb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Left = 20, Top = y + 22, Width = 340 };
                cb.Items.AddRange(fd.Choices);
                cb.SelectedItem = fd.Value;
                if (cb.SelectedIndex < 0 && cb.Items.Count > 0) cb.SelectedIndex = 0;
                c = cb;
            }
            else c = new TextBox { Left = 20, Top = y + 22, Width = 340, Text = fd.Value };
            f.Controls.Add(c);
            inputs.Add(c);
            y += 58;
        }
        f.Controls.Add(new Label { Text = "Book Cover", Left = 20, Top = y, AutoSize = true, ForeColor = Theme.Ink });
        var zone = new CoverDropZone(340, 150, existingCoverPath);
        zone.Location = new Point(20, y + 22);
        f.Controls.Add(zone);
        if (!showDelete) zone.HideDeleteButton();
        coverZone = zone;
        y += 180;

        var ok = Theme.Btn("Save", Theme.Green, Color.White, 90, 32);
        ok.Location = new Point(170, y + 6);
        ok.DialogResult = DialogResult.OK;
        var cancel = Theme.Btn("Cancel", Theme.Brown, Color.White, 90, 32);
        cancel.Location = new Point(270, y + 6);
        cancel.DialogResult = DialogResult.Cancel;
        f.Controls.Add(ok);
        f.Controls.Add(cancel);
        f.AcceptButton = ok;
        f.CancelButton = cancel;
        if (f.ShowDialog(owner) != DialogResult.OK) { coverZone = new CoverDropZone(0, 0, ""); return null; }
        return inputs.Select(c => c.Text).ToArray();
    }
}

public class StatCard : RoundedPanel
{
    readonly Label val;

    public StatCard(string glyph, string title, string value, Color glyphColor)
    {
        Fill = Theme.CardDark;
        Dock = DockStyle.Fill;
        Margin = new Padding(0, 0, 10, 0);
        var g = new Label { Text = glyph, Font = Theme.E(20), ForeColor = glyphColor, AutoSize = true, BackColor = Color.Transparent, Location = new Point(10, 14) };
        var t = Theme.Lbl(title, 8.5f, FontStyle.Bold);
        t.Location = new Point(58, 8);
        val = Theme.Lbl(value, 17, FontStyle.Bold);
        val.Location = new Point(58, 26);
        Controls.Add(g);
        Controls.Add(t);
        Controls.Add(val);
    }

    public string Value { set { val.Text = value; } }
}

/// <summary>Stat card with independent From/To calendar date filters and Clear option.</summary>
public class DateFilterStatCard : RoundedPanel
{
    readonly Label valLabel;
    readonly Label lblStatus;
    readonly DateTimePicker dtpFrom;
    readonly DateTimePicker dtpTo;
    readonly LinkLabel btnClear;
    readonly Func<Student, bool> statusFilter;
    bool hasFilter = false;
    bool isUpdating = false;

    public DateFilterStatCard(string glyph, string title, Color glyphColor, Func<Student, bool> filter)
    {
        statusFilter = filter;
        Fill = Theme.CardDark;
        Dock = DockStyle.Fill;
        Margin = new Padding(0, 0, 10, 0);

        var g = new Label
        {
            Text = glyph, Font = Theme.E(15), ForeColor = glyphColor,
            AutoSize = true, BackColor = Color.Transparent, Location = new Point(8, 8)
        };
        var t = Theme.Lbl(title, 8.5f, FontStyle.Bold);
        t.Location = new Point(32, 6);

        valLabel = Theme.Lbl("0", 13, FontStyle.Bold, glyphColor);
        valLabel.Location = new Point(32, 22);

        var lblFrom = new Label
        {
            Text = "From:", Font = Theme.F(7.5f), ForeColor = Theme.Muted,
            AutoSize = true, BackColor = Color.Transparent, Location = new Point(8, 48)
        };
        dtpFrom = new DateTimePicker
        {
            Format = DateTimePickerFormat.Short,
            Font = Theme.F(7.5f),
            Location = new Point(44, 46),
            Size = new Size(86, 20)
        };
        dtpFrom.Value = DateTime.Today.AddMonths(-1);

        lblStatus = new Label
        {
            Text = "All dates", Font = Theme.F(7.5f), ForeColor = Theme.Muted,
            AutoSize = true, BackColor = Color.Transparent, Location = new Point(136, 48)
        };

        var lblTo = new Label
        {
            Text = "To:", Font = Theme.F(7.5f), ForeColor = Theme.Muted,
            AutoSize = true, BackColor = Color.Transparent, Location = new Point(8, 74)
        };
        dtpTo = new DateTimePicker
        {
            Format = DateTimePickerFormat.Short,
            Font = Theme.F(7.5f),
            Location = new Point(44, 72),
            Size = new Size(86, 20)
        };
        dtpTo.Value = DateTime.Today;

        btnClear = new LinkLabel
        {
            Text = "Clear", Font = Theme.F(7.5f, FontStyle.Bold), LinkColor = Theme.Brown,
            ActiveLinkColor = Theme.Brown, VisitedLinkColor = Theme.Brown,
            LinkBehavior = LinkBehavior.HoverUnderline,
            AutoSize = true, BackColor = Color.Transparent, Location = new Point(136, 74)
        };
        btnClear.Click += (s, e) => Clear();

        Resize += (s, e) =>
        {
            int rx = Math.Max(132, dtpFrom.Right + 6);
            lblStatus.Left = rx;
            btnClear.Left = rx;
        };

        dtpFrom.ValueChanged += (s, e) => OnDateChanged(true);
        dtpTo.ValueChanged += (s, e) => OnDateChanged(false);

        Controls.AddRange(new Control[] { g, t, valLabel, lblFrom, dtpFrom, lblStatus, lblTo, dtpTo, btnClear });

        UpdateCount();
    }

    void OnDateChanged(bool isFrom)
    {
        if (isUpdating) return;
        if (dtpTo.Value.Date < dtpFrom.Value.Date)
        {
            isUpdating = true;
            MessageBox.Show("The 'To' date cannot be earlier than the 'From' date.", "Invalid Date Range",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            if (isFrom)
                dtpFrom.Value = dtpTo.Value.Date;
            else
                dtpTo.Value = dtpFrom.Value.Date;
            isUpdating = false;
            return;
        }

        hasFilter = true;
        lblStatus.Text = "Filtered";
        lblStatus.ForeColor = Theme.Green;
        UpdateCount();
    }

    public void Clear()
    {
        isUpdating = true;
        hasFilter = false;
        dtpFrom.Value = DateTime.Today.AddMonths(-1);
        dtpTo.Value = DateTime.Today;
        lblStatus.Text = "All dates";
        lblStatus.ForeColor = Theme.Muted;
        isUpdating = false;
        UpdateCount();
    }

    public void UpdateCount()
    {
        int count;
        if (hasFilter)
        {
            var from = dtpFrom.Value.Date;
            var to = dtpTo.Value.Date;
            count = AppData.Students.Count(s => statusFilter(s) && s.Joined.Date >= from && s.Joined.Date <= to);
        }
        else
        {
            count = AppData.Students.Count(statusFilter);
        }
        valLabel.Text = count.ToString("N0");
    }
}

/// <summary>Tab button with an icon, title, and count pill badge (e.g. Borrow Requests, Return Requests).</summary>
public class RequestTab : Control
{
    public bool IsSelected { get; set; }
    public string Glyph { get; }
    public string Title { get; }
    public int Count { get; set; }
    public Color BadgeColor { get; }

    public RequestTab(string glyph, string title, Color badgeColor, bool selected)
    {
        Glyph = glyph;
        Title = title;
        BadgeColor = badgeColor;
        IsSelected = selected;
        Cursor = Cursors.Hand;
        DoubleBuffered = true;
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Height = 36;
        Width = 180;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        if (IsSelected)
        {
            using var path = Theme.Round(r, 6);
            using var br = new SolidBrush(Color.White);
            using var pen = new Pen(Color.FromArgb(0xDC, 0xD0, 0xBD));
            e.Graphics.FillPath(br, path);
            e.Graphics.DrawPath(pen, path);
        }
        else
        {
            using var path = Theme.Round(r, 6);
            using var br = new SolidBrush(Color.FromArgb(0xEB, 0xDE, 0xCD));
            e.Graphics.FillPath(br, path);
        }

        // Glyph
        using var gFont = Theme.E(11f);
        TextRenderer.DrawText(e.Graphics, Glyph, gFont, new Point(12, (Height - 18) / 2), Theme.Ink);

        // Title
        using var tFont = Theme.F(9f, IsSelected ? FontStyle.Bold : FontStyle.Regular);
        var tSize = TextRenderer.MeasureText(Title, tFont);
        int tY = (Height - tSize.Height) / 2;
        TextRenderer.DrawText(e.Graphics, Title, tFont, new Point(34, tY), Theme.Ink);

        // Badge
        string countStr = Count.ToString();
        using var bFont = Theme.F(7.5f, FontStyle.Bold);
        var bSize = TextRenderer.MeasureText(countStr, bFont);
        int bW = Math.Max(20, bSize.Width + 10);
        int bH = 18;
        int bX = 34 + tSize.Width + 8;
        int bY = (Height - bH) / 2;
        var bRect = new Rectangle(bX, bY, bW, bH);
        using (var bPath = Theme.Round(bRect, 9))
        using (var bBr = new SolidBrush(BadgeColor))
        {
            e.Graphics.FillPath(bBr, bPath);
        }
        TextRenderer.DrawText(e.Graphics, countStr, bFont, bRect, Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        int totalNeeded = bX + bW + 12;
        if (Width != totalNeeded) Width = totalNeeded;
    }
}

public static class Ui
{
    public static Panel StatRow(params Control[] cards) => StatRow(78, cards);

    public static Panel StatRow(int height, params Control[] cards)
    {
        var t = new TableLayoutPanel
        {
            Dock = DockStyle.Top, Height = height, ColumnCount = cards.Length, RowCount = 1,
            BackColor = Color.Transparent, Padding = new Padding(0, 0, 0, 10)
        };
        t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        for (int i = 0; i < cards.Length; i++)
        {
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / cards.Length));
            t.Controls.Add(cards[i], i, 0);
        }
        cards[^1].Margin = new Padding(0);
        return t;
    }

    public static ComboBox Combo(params string[] items)
    {
        var c = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = Theme.SearchFill,
            ForeColor = Theme.Ink, Font = Theme.F(9), Anchor = AnchorStyles.Left | AnchorStyles.Right
        };
        c.Items.AddRange(items);
        c.SelectedIndex = 0;
        return c;
    }

    /// <summary>Search box that stretches, followed by fixed-width combo boxes.</summary>
    public static Panel FilterRow(Control search, params ComboBox[] combos)
    {
        var t = new TableLayoutPanel
        {
            Dock = DockStyle.Top, Height = 50, ColumnCount = 1 + combos.Length, RowCount = 1,
            BackColor = Color.Transparent, Padding = new Padding(0, 0, 0, 10)
        };
        t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        search.Dock = DockStyle.Fill;
        t.Controls.Add(search, 0, 0);
        for (int i = 0; i < combos.Length; i++)
        {
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            combos[i].Margin = new Padding(8, 0, 0, 0);
            t.Controls.Add(combos[i], i + 1, 0);
        }
        return t;
    }

    public static Panel SearchWrap(Control search)
    {
        var p = new Panel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(0, 0, 0, 12), BackColor = Color.Transparent };
        search.Dock = DockStyle.Fill;
        p.Controls.Add(search);
        return p;
    }

    /// <summary>"Showing x records" plus prev/next buttons (paging is a placeholder).</summary>
    public static Panel Pager(Label info)
    {
        var p = new Panel { Dock = DockStyle.Bottom, Height = 40, BackColor = Color.Transparent };
        info.Location = new Point(2, 12);
        var next = Theme.Btn(">", Theme.Input, Theme.Ink, 36, 26);
        var prev = Theme.Btn("<", Theme.Input, Theme.Ink, 36, 26);
        next.Click += (s, e) => MessageBox.Show("There are no more pages in this demo.", "Pagination");
        prev.Click += (s, e) => MessageBox.Show("You are on the first page.", "Pagination");
        p.Controls.Add(info);
        p.Controls.Add(next);
        p.Controls.Add(prev);
        p.Resize += (s, e) =>
        {
            next.Location = new Point(p.Width - next.Width - 2, 6);
            prev.Location = new Point(next.Left - prev.Width - 6, 6);
        };
        return p;
    }
}

/// <summary>One sidebar entry (icon glyph + text, optional red badge).</summary>
public class NavItem : Control
{
    public string Key { get; }
    public string Glyph { get; }
    public bool IsSelected { get; set; }
    public int Badge { get; set; }
    readonly Font glyphFont = Theme.E(15);
    readonly Font textFont = Theme.F(9.5f);

    public NavItem(string glyph, string text, string key)
    {
        Glyph = glyph; Text = text; Key = key;
        Size = new Size(214, 42);
        Cursor = Cursors.Hand;
        DoubleBuffered = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(IsSelected ? Theme.Selected : Theme.Sidebar);
        TextRenderer.DrawText(g, Glyph, glyphFont, new Rectangle(8, 0, 40, Height), Theme.Ink,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, Text, textFont, new Rectangle(54, 0, Width - 54, Height), Theme.Ink,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        if (Badge > 0)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var br = new SolidBrush(Color.FromArgb(0xD9, 0x2D, 0x2D));
            g.FillEllipse(br, 34, 5, 14, 14);
            TextRenderer.DrawText(g, Badge.ToString(), Theme.F(6.5f, FontStyle.Bold), new Rectangle(34, 5, 14, 14), Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }
}

/// <summary>Floating book-cart button with a count badge (bottom-right on student screens).</summary>
public class CartFab : Control
{
    public CartFab()
    {
        Size = new Size(56, 56);
        Cursor = Cursors.Hand;
        DoubleBuffered = true;
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var fill = AppData.Suspended ? Color.FromArgb(0x9A, 0x95, 0x8C) : Color.FromArgb(0x2B, 0x24, 0x1C);
        using (var br = new SolidBrush(fill)) g.FillEllipse(br, 2, 10, 44, 44);
        TextRenderer.DrawText(g, "📖", Theme.E(15), new Rectangle(2, 10, 44, 44), Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        int n = AppData.Cart.Count;
        if (n > 0 && !AppData.Suspended)
        {
            using var br = new SolidBrush(Color.FromArgb(0xD9, 0x2D, 0x2D));
            g.FillEllipse(br, 30, 0, 20, 20);
            TextRenderer.DrawText(g, n.ToString(), Theme.F(8, FontStyle.Bold), new Rectangle(30, 0, 20, 20), Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }
}

/// <summary>Book tile used on the Browse Books screen.</summary>
public class BookCard : Control
{
    public Book Book { get; }
    public event Action<Book> AddClicked;
    readonly Bitmap cover;
    readonly Rectangle plus;
    static readonly Font Small = Theme.F(7.5f);
    static readonly Font SmallBold = Theme.F(7.5f, FontStyle.Bold);

    public BookCard(Book book)
    {
        Book = book;
        cover = CoverFactory.Make(book, 100, 146);
        Size = new Size(196, 296);
        DoubleBuffered = true;
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        plus = new Rectangle(Width - 32, Height - 32, 20, 20);
    }

    static Color CatColor(string c) => c switch
    {
        "CIR" => Color.FromArgb(0xE0, 0x8A, 0x00),
        "FIC" => Color.FromArgb(0x2E, 0x9E, 0x3A),
        "FIL" => Color.FromArgb(0xD0, 0x20, 0x20),
        _ => Color.FromArgb(0x20, 0x40, 0xC0)
    };

    void Pair(Graphics g, string label, string value, Color valueColor, float y)
    {
        var sf = StringFormat.GenericTypographic;
        float w1 = g.MeasureString(label, Small, PointF.Empty, sf).Width;
        float w2 = g.MeasureString(value, SmallBold, PointF.Empty, sf).Width;
        float x = (Width - (w1 + w2)) / 2;
        using var ink = new SolidBrush(Theme.Ink);
        using var vb = new SolidBrush(valueColor);
        g.DrawString(label, Small, ink, x, y, sf);
        g.DrawString(value, SmallBold, vb, x + w1, y, sf);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        using (var path = Theme.Round(new Rectangle(0, 0, Width - 1, Height - 1), 6))
        using (var br = new SolidBrush(Color.FromArgb(0xF3, 0xEC, 0xDD)))
        using (var pen = new Pen(Theme.Border))
        {
            g.FillPath(br, path);
            g.DrawPath(pen, path);
        }
        g.DrawImage(cover, (Width - 100) / 2, 10, 100, 146);
        using (var sf = new StringFormat { Alignment = StringAlignment.Center })
        using (var ink = new SolidBrush(Theme.Ink))
            g.DrawString($"{Book.Title} By: {Book.Author}", Small, ink, new RectangleF(8, 162, Width - 16, 46), sf);
        Pair(g, "Category: ", Book.Category, CatColor(Book.Category), 212);
        Pair(g, "ISBN: ", Book.Isbn, Theme.Ink, 228);
        Pair(g, "Status: ", Book.Available ? "Available" : "Borrowed", Book.Available ? Color.FromArgb(0x1E, 0x9E, 0x2E) : Color.Red, 244);

        using (var pen = new Pen(Theme.Muted, 1.2f))
        {
            g.DrawEllipse(pen, plus);
            g.DrawLine(pen, plus.X + 5, plus.Y + 10, plus.Right - 5, plus.Y + 10);
            g.DrawLine(pen, plus.X + 10, plus.Y + 5, plus.X + 10, plus.Bottom - 5);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Cursor = plus.Contains(e.Location) ? Cursors.Hand : Cursors.Default;
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (plus.Contains(e.Location)) AddClicked?.Invoke(Book);
    }
}

public class AvatarCircle : Control
{
    public Image AvatarImage { get; set; }

    public AvatarCircle()
    {
        Size = new Size(64, 64);
        DoubleBuffered = true;
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        if (AvatarImage != null)
        {
            using var path = new GraphicsPath();
            path.AddEllipse(0, 0, Width - 1, Height - 1);
            e.Graphics.SetClip(path);
            e.Graphics.DrawImage(AvatarImage, 0, 0, Width, Height);
            e.Graphics.ResetClip();
        }
        else
        {
            using var br = new SolidBrush(Color.FromArgb(0x8A, 0x6E, 0x50));
            e.Graphics.FillEllipse(br, 0, 0, Width - 1, Height - 1);
            TextRenderer.DrawText(e.Graphics, "👤", Theme.E(22), ClientRectangle, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }
}

public class BarChart : Control
{
    readonly string[] labels = { "Jan", "Feb", "Mar", "Apr", "May", "Jun" };
    readonly int[] values = { 18, 24, 32, 28, 36, 23 };

    public BarChart()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var small = Theme.F(7);
        int left = 28, bottom = 20, top = 6;
        var plot = new Rectangle(left, top, Width - left - 6, Height - top - bottom);
        using var grid = new Pen(Color.FromArgb(60, 0x8A, 0x7B, 0x68));
        for (int t = 0; t <= 40; t += 10)
        {
            int y = plot.Bottom - plot.Height * t / 40;
            g.DrawLine(grid, plot.Left, y, plot.Right, y);
            TextRenderer.DrawText(g, t.ToString(), small, new Rectangle(0, y - 7, left - 4, 14), Theme.Muted, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
        }
        int slot = plot.Width / values.Length, bw = (int)(slot * 0.5);
        using var bar = new SolidBrush(Color.FromArgb(0xBB, 0x9F, 0x82));
        for (int i = 0; i < values.Length; i++)
        {
            int h = plot.Height * values[i] / 40;
            var r = new Rectangle(plot.Left + i * slot + (slot - bw) / 2, plot.Bottom - h, bw, h);
            g.FillRectangle(bar, r);
            TextRenderer.DrawText(g, labels[i], small, new Rectangle(plot.Left + i * slot, plot.Bottom + 2, slot, 16), Theme.Muted, TextFormatFlags.HorizontalCenter);
        }
        small.Dispose();
    }
}

public class DonutChart : Control
{
    readonly string[] names = { "Fiction", "Reference", "Filipino", "Circulation" };
    readonly int[] pct = { 42, 24, 18, 16 };
    readonly Color[] colors =
    {
        Color.FromArgb(0x7A, 0x5C, 0x40), Color.FromArgb(0xA8, 0x8A, 0x6C),
        Color.FromArgb(0xCB, 0xB4, 0x98), Color.FromArgb(0xE3, 0xD3, 0xBC)
    };

    public DonutChart()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        int size = Math.Min(Height - 6, Width / 2);
        var rect = new Rectangle(4, (Height - size) / 2, size, size);
        float a = -90;
        for (int i = 0; i < pct.Length; i++)
        {
            float sweep = pct[i] * 3.6f;
            using var br = new SolidBrush(colors[i]);
            g.FillPie(br, rect, a, sweep);
            a += sweep;
        }
        var hole = Rectangle.Inflate(rect, -(int)(size * 0.27), -(int)(size * 0.27));
        using (var br = new SolidBrush(Theme.Card)) g.FillEllipse(br, hole);

        var f = Theme.F(8);
        var fb = Theme.F(8, FontStyle.Bold);
        int y = (Height - names.Length * 24) / 2;
        for (int i = 0; i < names.Length; i++)
        {
            using var br = new SolidBrush(colors[i]);
            g.FillEllipse(br, size + 24, y + 5, 9, 9);
            TextRenderer.DrawText(g, names[i], f, new Point(size + 38, y + 1), Theme.Ink);
            TextRenderer.DrawText(g, pct[i] + "%", fb, new Rectangle(Width - 46, y, 40, 18), Theme.Ink, TextFormatFlags.Right);
            y += 24;
        }
        f.Dispose();
        fb.Dispose();
    }
}
