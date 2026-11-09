using EnterpriseUI.Exporting;
using EnterpriseUI.WinForms;
using TestDataGrid.Services;

namespace TestDataGrid.Views;

public sealed class ReportsView : UserControl
{
    private readonly SchoolStore _store;
    private readonly ComboBox _reports;
    private readonly EnterpriseListViewControl _preview;
    public ReportsView(SchoolStore store)
    {
        _store = store; Dock = DockStyle.Fill; Font = SchoolUi.BodyFont;
        _preview = new EnterpriseListViewControl { Dock = DockStyle.Fill, AccessibleName = "Report preview", SelectionMode = ListSelectionMode.MultiExtended };
        var toolbar = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(18, 8, 8, 8) };
        _reports = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
        _reports.Items.AddRange(new object[] { "Outstanding Fees", "Attendance Summary", "Course Results", "Activity Log" });
        toolbar.Items.Add(new ToolStripControlHost(_reports));
        var refresh = new ToolStripButton("Refresh"); refresh.Click += (_, _) => Reload(); toolbar.Items.Add(refresh);
        foreach (var format in new[] { "pdf", "xlsx", "csv", "html", "json", "xml" })
        {
            var extension = format;
            var button = new ToolStripButton(format.ToUpperInvariant());
            button.Click += async (_, _) =>
            {
                using var dialog = new SaveFileDialog { Filter = extension.ToUpperInvariant() + "|*." + extension, FileName = "school-report." + extension };
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
                try { await ExportService.Default.ExportToFileAsync(SchoolReports.Build(_store.Data, (string)_reports.SelectedItem!), extension, dialog.FileName); }
                catch (Exception exception) { MessageBox.Show(FindForm(), exception.Message, "Report Export", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            };
            toolbar.Items.Add(button);
        }
        Controls.Add(_preview); Controls.Add(toolbar); Controls.Add(SchoolUi.Heading("Reports"));
        _reports.SelectedIndexChanged += (_, _) => Reload(); _reports.SelectedIndex = 0;
        _store.Changed += Changed;
        SchoolUi.Apply(this);
    }
    private void Changed(object? sender, EventArgs e) => Reload();
    private void Reload()
    {
        var sheet = SchoolReports.Build(_store.Data, (string)_reports.SelectedItem!).Sheets[0];
        _preview.Items.Clear();
        _preview.ClearColumns();
        foreach (var column in sheet.Columns) _preview.AddColumn(column.Title, Math.Max(110, 900 / Math.Max(1, sheet.Columns.Count)));
        _preview.Items.AddRange(sheet.Rows.Select(row => ToItem(sheet, row)));
    }

    private static EnterpriseListViewItem ToItem(ExportSheet sheet, ExportRow row)
    {
        var cells = row.Cells.Select((value, index) => Format(sheet.Columns[index], value)).ToArray();
        return new EnterpriseListViewItem(cells.FirstOrDefault() ?? string.Empty, cells.Skip(1).ToArray());
    }

    private static string Format(ExportColumn column, object? value) => value switch
    {
        null => string.Empty,
        IFormattable formattable when !string.IsNullOrEmpty(column.Format) => formattable.ToString(column.Format, null),
        DateTime date => date.ToString("g"),
        _ => Convert.ToString(value) ?? string.Empty
    };
    protected override void Dispose(bool disposing) { if (disposing) _store.Changed -= Changed; base.Dispose(disposing); }
}
