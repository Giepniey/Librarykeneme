using System.Drawing.Drawing2D;

namespace LibraryInventory;

/// <summary>Drag & drop image upload dialog.</summary>
public class ImageUploadDialog : Form
{
    public Image? UploadedImage { get; private set; }
    readonly Panel dropZone;
    readonly PictureBox preview;
    readonly Button btnBrowse, btnSave, btnCancel;
    bool hasImage = false;

    public ImageUploadDialog()
    {
        Text = "Change Photo";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(480, 420);
        BackColor = Theme.Card;

        var title = Theme.Lbl("Upload Profile Photo", 12, FontStyle.Bold);
        title.Location = new Point(20, 20);
        Controls.Add(title);

        // Drop zone
        dropZone = new Panel
        {
            Location = new Point(20, 60),
            Size = new Size(440, 200),
            BackColor = Color.FromArgb(0xF5, 0xF5, 0xF5),
            BorderStyle = BorderStyle.FixedSingle,
            AllowDrop = true
        };

        var cloudIcon = new Label
        {
            Text = "☁",
            Font = Theme.E(48),
            ForeColor = Color.FromArgb(0x8A, 0x7B, 0x68),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        cloudIcon.Location = new Point((dropZone.Width - cloudIcon.Width) / 2, 40);

        var uploadArrow = new Label
        {
            Text = "↑",
            Font = Theme.F(24, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            BackColor = Color.Transparent
        };
        uploadArrow.Location = new Point(cloudIcon.Left + cloudIcon.Width / 2 - 8, cloudIcon.Top + 10);

        var dragText = Theme.Lbl("Drag & drop", 14, FontStyle.Bold);
        dragText.Location = new Point((dropZone.Width - dragText.Width) / 2, 120);

        var browseText = new Label
        {
            AutoSize = true,
            BackColor = Color.Transparent,
            ForeColor = Theme.Muted,
            Font = Theme.F(10)
        };
        browseText.Text = "your profile photo here, or ";

        var browseLink = new LinkLabel
        {
            Text = "browse",
            Font = Theme.F(10, FontStyle.Bold),
            LinkColor = Theme.Brown,
            AutoSize = true,
            BackColor = Color.Transparent
        };
        browseLink.Click += (s, e) => BrowseForImage();

        var textPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            BackColor = Color.Transparent,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        textPanel.Controls.Add(browseText);
        textPanel.Controls.Add(browseLink);
        textPanel.Location = new Point((dropZone.Width - textPanel.Width) / 2, 150);

        dropZone.Controls.AddRange(new Control[] { cloudIcon, uploadArrow, dragText, textPanel });
        dropZone.DragEnter += DropZone_DragEnter;
        dropZone.DragDrop += DropZone_DragDrop;
        Controls.Add(dropZone);

        // Preview area
        preview = new PictureBox
        {
            Location = new Point(20, 270),
            Size = new Size(440, 80),
            SizeMode = PictureBoxSizeMode.Zoom,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White,
            Visible = false
        };
        Controls.Add(preview);

        // Buttons
        btnSave = Theme.Btn("Save", Theme.Green, Color.White, 100, 32);
        btnSave.Location = new Point(260, 370);
        btnSave.Enabled = false;
        btnSave.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };

        btnCancel = Theme.Btn("Cancel", Theme.Brown, Color.White, 100, 32);
        btnCancel.Location = new Point(370, 370);
        btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

        Controls.Add(btnSave);
        Controls.Add(btnCancel);
    }

    void DropZone_DragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            e.Effect = DragDropEffects.Copy;
        else
            e.Effect = DragDropEffects.None;
    }

    void DropZone_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            LoadImage(files[0]);
        }
    }

    void BrowseForImage()
    {
        using var ofd = new OpenFileDialog
        {
            Filter = "Image Files|*.png;*.jpg;*.jpeg",
            Title = "Select Profile Photo"
        };

        if (ofd.ShowDialog() == DialogResult.OK)
        {
            LoadImage(ofd.FileName);
        }
    }

    void LoadImage(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext != ".png" && ext != ".jpg" && ext != ".jpeg")
        {
            MessageBox.Show("Please select a valid image file (PNG, JPG, or JPEG).", "Invalid File Type", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        try
        {
            var img = Image.FromFile(path);
            UploadedImage?.Dispose();
            UploadedImage = img;
            preview.Image = img;
            preview.Visible = true;
            btnSave.Enabled = true;
            hasImage = true;

            // Hide drop zone instructions
            foreach (Control c in dropZone.Controls)
                c.Visible = false;

            // Show checkmark in drop zone
            var check = new Label
            {
                Text = "✓",
                Font = Theme.E(64),
                ForeColor = Theme.Green,
                AutoSize = true,
                BackColor = Color.Transparent
            };
            check.Location = new Point((dropZone.Width - check.Width) / 2, (dropZone.Height - check.Height) / 2);
            dropZone.Controls.Add(check);
        }
        catch
        {
            MessageBox.Show("Failed to load the image. Please try another file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
