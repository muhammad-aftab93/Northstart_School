using EnterpriseUI.Exporting;
using EnterpriseUI.WinForms;
using TestDataGrid.Services;

namespace TestDataGrid.Views;

public sealed class ReportsView : UserControl
{
    private readonly SchoolStore _store;
    private readonly EnterpriseDropDownControl _reports;
    private readonly EnterpriseListViewControl _preview;
    public ReportsView(SchoolStore store)
    {
        _store = store; Dock = DockStyle.Fill; Font = SchoolUi.BodyFont;
        _preview = new EnterpriseListViewControl { Dock = DockStyle.Fill, AccessibleName = "Report preview", SelectionMode = ListSelectionMode.MultiExtended };
        var toolbar = new CommandBar();
        _reports = new EnterpriseDropDownControl { Width = 240, Height = 34, AccessibleName = "Report" };
        _reports.SetItems(new[] { "Outstanding Fees", "Attendance Summary", "Course Results", "Activity Log" });
        toolbar.AddControl("Report", _reports);
        toolbar.AddCommand("Refresh", Reload);
        async Task ExportAsyncTo(string extension)
        {
            using var dialog = new SaveFileDialog { Filter = extension.ToUpperInvariant() + "|*." + extension, FileName = "school-report." + extension };
            if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
            using var busy = SchoolUi.Busy(this, "Exporting report\u2026");
            try { await ExportService.Default.ExportToFileAsync(SchoolReports.Build(_store.Data, (string)_reports.SelectedItem!), extension, dialog.FileName); }
            catch (Exception exception) { MessageBox.Show(FindForm(), exception.Message, "Report Export", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
        toolbar.AddSplit("Export", () => _ = ExportAsyncTo("pdf"), new[] { "pdf", "xlsx", "csv", "html", "json", "xml" }.Select(format => (format.ToUpperInvariant(), (Action)(() => _ = ExportAsyncTo(format)))));
        Controls.Add(_preview); Controls.Add(toolbar); Controls.Add(SchoolUi.Heading("Reports"));
        _reports.SelectionChanged += (_, _) => Reload(); _reports.SelectedIndex = 0;
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
