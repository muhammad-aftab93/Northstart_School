using EnterpriseUI.Exporting;
using TestDataGrid.Services;

namespace TestDataGrid.Views;

public sealed class ReportsView : UserControl
{
    private readonly SchoolStore _store;
    private readonly ComboBox _reports;
    private readonly DataGridView _preview;
    public ReportsView(SchoolStore store)
    {
        _store = store; Dock = DockStyle.Fill; Font = SchoolUi.BodyFont;
        _preview = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, RowHeadersVisible = false, AllowUserToAddRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
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
        _preview.Columns.Clear(); _preview.Rows.Clear();
        foreach (var column in sheet.Columns) _preview.Columns.Add(column.Key, column.Title);
        foreach (var row in sheet.Rows) _preview.Rows.Add(row.Cells.Select(value => value ?? string.Empty).ToArray());
    }
    protected override void Dispose(bool disposing) { if (disposing) _store.Changed -= Changed; base.Dispose(disposing); }
}