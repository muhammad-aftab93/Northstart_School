using EnterpriseUI.WinForms;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Theming;
using EnterpriseUI.Theming.Themes;

namespace TestDataGrid.Views;

public sealed class ControlWorkshopView : UserControl
{
    private readonly Panel _preview;
    private readonly Panel _previewContent;
    private readonly EnterprisePropertyGridControl _properties;
    public ControlWorkshopView()
    {
        Dock = DockStyle.Fill; Font = SchoolUi.BodyFont;
        var controls = new EnterpriseListControl { Dock = DockStyle.Left, Width = 290, DisplayMember = "Name", ItemHeight = 30, AccessibleName = "Controls" };
        controls.DataSource = typeof(EnterpriseDataGrid).Assembly.GetExportedTypes().Where(type => typeof(Control).IsAssignableFrom(type) && !typeof(Form).IsAssignableFrom(type) && !type.IsAbstract && !type.ContainsGenericParameters && type.GetConstructor(Type.EmptyTypes) != null).OrderBy(type => type.Name).ToList();
        _preview = new Panel { Dock = DockStyle.Fill };
        _previewContent = new Panel { Dock = DockStyle.Fill, Padding = new Padding(30) };
        var tools = CreateDeveloperToolStrip();
        _preview.Controls.Add(_previewContent);
        _preview.Controls.Add(tools);
        _properties = new EnterprisePropertyGridControl { Dock = DockStyle.Right, Width = 380, AccessibleName = "Control properties" };
        controls.SelectionChanged += (_, _) =>
        {
            try
            {
                foreach (Control old in _previewContent.Controls.Cast<Control>().ToArray()) old.Dispose();
                if (controls.SelectedItem is not Type type) return;
                if (typeof(Form).IsAssignableFrom(type)) return;
                var control = (Control)Activator.CreateInstance(type)!;
                control.Location = new Point(30, 30); control.Width = Math.Max(200, _previewContent.Width - 60); control.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                _previewContent.Controls.Add(control); _properties.SelectedObject = control;
            }
            catch (Exception exception) { EnterpriseMessageBox.ShowMessage(exception.Message, "Control Preview", MessageBoxButtons.OK, MessageBoxIcon.Warning, owner: FindForm()); }
        };
        _properties.PropertyValueChanged += (_, _) => _previewContent.Invalidate(true);
        Controls.Add(_preview); Controls.Add(_properties); Controls.Add(controls); Controls.Add(SchoolUi.Heading("Control Explorer"));
        if (controls.Count > 0) controls.SelectedIndex = 0;
        SchoolUi.Apply(this);
    }

    private ToolStrip CreateDeveloperToolStrip()
    {
        var strip = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden, ImageScalingSize = new Size(16, 16) };
        strip.Items.Add("Spy", null, (_, _) => InspectPreview(new EnterpriseControlSpy()));
        strip.Items.Add("Hierarchy", null, (_, _) => InspectPreview(new EnterpriseElementHierarchyEditor()));
        strip.Items.Add("Style", null, (_, _) =>
        {
            var theme = ThemeManager.Instance.CurrentTheme as Theme ?? BuiltInThemes.CreateFluent(ColorScheme.Light);
            using var builder = new EnterpriseVisualStyleBuilder(theme);
            builder.ShowDialog(FindForm());
        });

        var more = new ToolStripDropDownButton("More");
        more.DropDownItems.Add("Edit shape", null, (_, _) => ShowShapeEditor());
        more.DropDownItems.Add("View image...", null, (_, _) => ShowImageViewer());
        more.DropDownItems.Add("Preview color...", null, (_, _) => PreviewColor());
        more.DropDownItems.Add("Convert preview form", null, (_, _) => ConvertPreviewForm());
        more.DropDownItems.Add("ZIP round-trip", null, (_, _) => VerifyZipRoundTrip());
        more.DropDownItems.Add("Message preview", null, (_, _) => EnterpriseMessageBox.ShowMessage("This message is rendered by the themed dialog.", "Control Explorer", MessageBoxButtons.OKCancel, MessageBoxIcon.Information, "Additional diagnostic details are available here.", FindForm()));
        strip.Items.Add(more);
        strip.Items.Add(CreateShellMenu());
        return strip;
    }

    private ToolStripDropDownButton CreateShellMenu()
    {
        var shell = new ToolStripDropDownButton("Shell");
        shell.DropDownItems.Add("Task dialog", null, (_, _) => ShowTaskDialogSample());
        shell.DropDownItems.Add("Markup dialog", null, (_, _) => EnterpriseMarkupDialog.ShowMarkup("**Markup dialog** shows *formatted* text, `code` and [links](https://example.org).", "Control Explorer", MessageBoxButtons.OKCancel, MessageBoxIcon.Information, owner: FindForm()));
        shell.DropDownItems.Add("About box", null, (_, _) => { using var about = new EnterpriseAboutBox(); about.LoadFromAssembly(typeof(EnterpriseDataGrid).Assembly); about.ShowDialog(FindForm()); });
        shell.DropDownItems.Add("Desktop alert", null, (_, _) => new EnterpriseDesktopAlertWindow(new EnterpriseUI.Notifications.EnterpriseDesktopAlert { CaptionText = "Control Explorer", ContentText = "Desktop alerts stack in the corner, can be pinned and never take focus.", AutoCloseDelaySeconds = 8 }).ShowAlert());
        shell.DropDownItems.Add("Toast", null, (_, _) => SchoolUi.Window(this)?.Toast("Control Explorer", "A toast confirms an action without interrupting."));
        shell.DropDownItems.Add("Overlay (2 seconds)", null, (_, _) => ShowOverlaySample());
        shell.DropDownItems.Add("Flyout", null, (_, _) => ShowFlyoutSample());
        return shell;
    }

    private static void ShowTaskDialogSample()
    {
        var model = new EnterpriseUI.Layout.EnterpriseTaskDialog { WindowTitle = "Control Explorer", MainInstruction = "How should the sample continue?", Content = "Task dialogs offer **command links**, a verification box and expandable details.", VerificationText = "Remember my choice", ExpandedInformation = "The chosen command and the checkbox are written back to the model." };
        model.CommandLinks.Add(new("fast", "Continue quickly", "Skip the optional checks"));
        model.CommandLinks.Add(new("safe", "Continue carefully", "Run every check first"));
        using var dialog = new EnterpriseTaskDialogForm(model) { DialogIcon = MessageBoxIcon.Question, CommonButtons = EnterpriseTaskDialogButtons.Cancel, FooterText = "Control Explorer sample" };
        dialog.Execute();
    }

    private void ShowOverlaySample()
    {
        var overlay = new EnterpriseOverlay { Caption = "Rendering preview\u2026", Indicator = EnterpriseOverlayIndicator.Dots };
        overlay.Show(_previewContent);
        var timer = new System.Windows.Forms.Timer { Interval = 2000 };
        timer.Tick += (_, _) => { timer.Dispose(); overlay.Hide(); overlay.Dispose(); };
        timer.Start();
    }

    private void ShowFlyoutSample()
    {
        var flyout = new EnterpriseFlyout { Text = "Inspector", FlyoutSize = 380, IsModal = false };
        flyout.Content.Controls.Add(new EnterprisePropertyGridControl { Dock = DockStyle.Fill, SelectedObject = _properties.SelectedObject, AccessibleName = "Inspector" });
        flyout.FlyoutClosed += (_, _) => flyout.Dispose();
        flyout.ShowFlyout(_preview);
    }

    private void InspectPreview(EnterpriseHierarchyToolForm inspector)
    {
        using (inspector)
        {
            inspector.Inspect(_previewContent);
            inspector.ShowDialog(FindForm());
        }
    }

    private void ShowShapeEditor()
    {
        using var window = new EnterpriseForm { Text = "Shape Editor", ClientSize = new Size(520, 430 + 36), StartPosition = FormStartPosition.CenterParent };
        var editor = new EnterpriseShapeEditor { Dock = DockStyle.Fill };
        editor.SetPoints(new[] { new PointF(0.5f, 0.1f), new PointF(0.9f, 0.5f), new PointF(0.5f, 0.9f), new PointF(0.1f, 0.5f) });
        editor.SetSegmentCurved(0, true);
        var preview = new EnterpriseButtonControl { Text = "Preview as window shape", Dock = DockStyle.Bottom, Height = 46 };
        preview.Click += (_, _) =>
        {
            using var shaped = new EnterpriseShapedForm { ClientSize = new Size(420, 320), StartPosition = FormStartPosition.CenterParent, KeyPreview = true };
            shaped.SetShape(editor);
            shaped.Controls.Add(new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Text = "This window has the shape you drew.\nDrag it by its body; Esc closes." });
            shaped.KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) shaped.Close(); };
            shaped.ShowDialog(window);
        };
        window.Controls.Add(editor);
        window.Controls.Add(preview);
        window.ShowDialog(FindForm());
    }

    private void ShowImageViewer()
    {
        using var files = new EnterpriseFileDialog { Title = "Open an image", Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|All files|*.*" };
        if (files.ShowOpenDialog(FindForm()) != DialogResult.OK) return;
        using var window = new EnterpriseForm { Text = Path.GetFileName(files.FileName), ClientSize = new Size(850, 600 + 36), StartPosition = FormStartPosition.CenterParent };
        var viewer = new EnterprisePictureBoxControl { Dock = DockStyle.Fill };
        viewer.LoadImage(files.FileName);
        window.Controls.Add(viewer);
        window.ShowDialog(FindForm());
    }

    private void PreviewColor()
    {
        using var dialog = new EnterpriseColorDialog { SelectedColor = _previewContent.BackColor };
        if (dialog.ShowDialog(FindForm()) == DialogResult.OK) _previewContent.BackColor = dialog.SelectedColor;
    }

    private void ConvertPreviewForm()
    {
        using var source = new Form { Text = "Control Explorer Preview", ClientSize = new Size(420, 160), StartPosition = FormStartPosition.CenterParent };
        source.Controls.Add(new Label { Text = "The form converter preserves this child control.", AutoSize = true, Location = new Point(20, 24) });
        var converter = new EnterpriseFormConverter();
        using var converted = converter.ConvertTo(source, typeof(ConvertedPreviewForm));
        converted.ShowDialog(FindForm());
    }

    private void VerifyZipRoundTrip()
    {
        const string text = "TestDataGrid control explorer";
        var bytes = EnterpriseUI.Documents.EnterpriseZipLibrary.CreateTextArchive(new Dictionary<string, string> { ["explorer.txt"] = text });
        var entries = EnterpriseUI.Documents.EnterpriseZipLibrary.ReadTextEntries(bytes);
        EnterpriseMessageBox.ShowMessage("ZIP round-trip complete", "Control Explorer", details: $"{entries.Count} entry; content: {entries["explorer.txt"]}", owner: FindForm());
    }

    public sealed class ConvertedPreviewForm : Form
    {
        public ConvertedPreviewForm() => Text = "Converted Control Explorer Form";
    }
}
