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
        return strip;
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
        using var window = new Form { Text = "Shape Editor", ClientSize = new Size(520, 380), StartPosition = FormStartPosition.CenterParent };
        var editor = new EnterpriseShapeEditor { Dock = DockStyle.Fill };
        editor.SetPoints(new[] { new PointF(0.5f, 0.1f), new PointF(0.9f, 0.5f), new PointF(0.5f, 0.9f), new PointF(0.1f, 0.5f) });
        editor.SetSegmentCurved(0, true);
        window.Controls.Add(editor);
        window.ShowDialog(FindForm());
    }

    private void ShowImageViewer()
    {
        using var files = new EnterpriseFileDialog { Title = "Open an image", Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|All files|*.*" };
        if (files.ShowOpenDialog(FindForm()) != DialogResult.OK) return;
        using var window = new Form { Text = Path.GetFileName(files.FileName), ClientSize = new Size(850, 600), StartPosition = FormStartPosition.CenterParent };
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
