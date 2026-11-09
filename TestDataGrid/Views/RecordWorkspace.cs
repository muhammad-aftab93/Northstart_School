using EnterpriseUI.Data;
using EnterpriseUI.Grids;
using EnterpriseUI.WinForms;
using TestDataGrid.Dialogs;
using TestDataGrid.Models;
using TestDataGrid.Services;

namespace TestDataGrid.Views;

public class SchoolRecordGrid<T> : EnterpriseDataGrid<T> where T : SchoolRecord
{
    public event Action<T, Point>? ContextRequested;
    public event Action<T>? EditRequested;

    public T? FocusedRecord => TryGetRowAt(Selection.FocusedRow, out var row) && row.Kind == GridRowKind.Data ? row.Item : null;
    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            var point = PointToScreen(e.Location);
            var hit = AccessibilityObject.HitTest(point.X, point.Y);
            if (hit?.Role is AccessibleRole.Row or AccessibleRole.Cell)
            {
                hit.Select(AccessibleSelection.TakeSelection | AccessibleSelection.TakeFocus);
                if (FocusedRecord is { } record) ContextRequested?.Invoke(record, e.Location);
                return;
            }
        }
        base.OnMouseDown(e);
    }
    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && FocusedRecord is { } record) { EditRequested?.Invoke(record); return; }
        base.OnMouseDoubleClick(e);
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Enter or Keys.F2 && FocusedRecord is { } record)
        {
            e.SuppressKeyPress = true; e.Handled = true; EditRequested?.Invoke(record); return;
        }
        base.OnKeyDown(e);
    }
}

public class RecordWorkspace<T> : UserControl where T : SchoolRecord, new()
{
    protected readonly SchoolStore Store;
    public SchoolRecordGrid<T> Grid { get; }
    protected readonly ToolStrip Commands;
    private readonly ContextMenuStrip _menu = new();
    private T? _context;
    private readonly TextBox _search = new();
    private readonly Label _summary;
    private readonly System.Windows.Forms.Timer _debounce;
    private readonly string _title;
    private EnterpriseCardViewControl? _cards;
    private EnterpriseFilterViewControl? _facets;
    private IFilterDescriptor? _builderFilter;
    private readonly List<IFilterDescriptor> _facetFilters = new();

    public RecordWorkspace(SchoolStore store, string title)
    {
        Store = store; _title = title; Dock = DockStyle.Fill; Font = SchoolUi.BodyFont;
        Grid = new SchoolRecordGrid<T> { Dock = DockStyle.Fill, AriaLabel = title, ShowGroupPanel = true, EnableQuickFilterRow = true, ShowFilterRow = true, EnableSearchPanel = true, ShowSearchPanel = false, SearchMode = GridSearchMode.Filter, PagingMode = GridPagingMode.Client, PageSize = store.Data.Settings.PageSize, ShowPager = true, ShowSummaryFooter = true, EnableAlternateRows = true, AllowRowDelete = false, EditMode = GridEditMode.None, AutoSizeMode = GridAutoSizeMode.None, FillColumnsToWidth = false };
        Grid.Columns.Add(new GridColumn { Field = "Id", Title = "ID", Width = 65, ReadOnly = true, PinPosition = ColumnPinPosition.Left });
        foreach (var field in SchoolSchema.Fields<T>())
        {
            var type = typeof(T).GetProperty(field.Name)!.PropertyType;
            var column = new GridColumn { Field = field.Name, Title = field.Label, Width = field.Width, ReadOnly = true, ColumnType = type == typeof(bool) ? GridColumnType.Boolean : type == typeof(DateTime) ? GridColumnType.Date : GridColumnType.Text };
            if (field.Lookup != null)
            {
                column.Title = field.Label.Replace(" ID", "", StringComparison.Ordinal);
                column.Width = Math.Max(220, field.Width);
                column.Field = field.Name + "Label";
            }
            if (type == typeof(DateTime)) column.FormatString = field.Name.EndsWith("At", StringComparison.Ordinal) ? "g" : "d";
            if (type == typeof(decimal)) { column.FormatString = "N2"; column.Alignment = ColumnTextAlignment.Right; column.FooterSummaries.Add(new GridColumnSummary(EnterpriseUI.Data.AggregateFunction.Sum, "{0:N2}")); }
            Grid.Columns.Add(column);
        }
        Grid.Columns.Add(new GridCommandColumn { Field = "EditAction", Title = "Actions", Expression = "'Edit'", Width = 90, MinWidth = 90, MaxWidth = 90, Reorderable = false, CanHide = false, AllowColumnChooser = false });
        Grid.CellClick += (_, e) => { if (e.Column.Field == "EditAction") Edit(e.Item); };
        Grid.EditRequested += Edit;
        Grid.ContextRequested += (record, point) => { _context = record; _menu.Show(Grid, point); };

        Commands = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(16, 6, 12, 6), AutoSize = true };
        AddCommand("Add", () => Edit(CreateRecord()));
        AddCommand("Edit", () => { if (Grid.FocusedRecord is { } record) Edit(record); });
        AddCommand("Delete", () => { if (Grid.FocusedRecord is { } record) Delete(record); });
        Commands.Items.Add(new ToolStripSeparator());
        var export = new ToolStripDropDownButton("Export");
        foreach (var extension in new[] { "xlsx", "csv", "pdf", "html", "json", "xml" })
        {
            var format = extension;
            export.DropDownItems.Add(extension.ToUpperInvariant(), null, async (_, _) => await ExportAsync(format));
        }
        Commands.Items.Add(export);
        AddCommand("Print Preview", () => Grid.ShowPrintPreviewDialog());
        AddCommand("Fit Columns", Grid.BestFitAllColumns);
        AddCommand("Clear Filters", () =>
        {
            _search.Clear(); Grid.ClearFilters(); Grid.ClearSort();
            _builderFilter = null; _facetFilters.Clear(); _facets?.ClearAll();
        });
        AddCommand("Filter Builder", () =>
        {
            var descriptor = EnterpriseDataFilter.Edit(FindForm(), typeof(T), _builderFilter);
            if (_builderFilter != null) Grid.RemoveFilter(_builderFilter);
            _builderFilter = descriptor;
            if (descriptor != null) Grid.AddFilter(descriptor);
        });
        var view = new ToolStripDropDownButton("View");
        view.DropDownItems.Add("Expand Groups", null, (_, _) => Grid.ExpandAllGroups());
        view.DropDownItems.Add("Collapse Groups", null, (_, _) => Grid.CollapseAllGroups());
        view.DropDownItems.Add("Save Layout", null, (_, _) => ManageLayout(true));
        view.DropDownItems.Add("Load Layout", null, (_, _) => ManageLayout(false));
        Commands.Items.Add(view);

        _search.Width = 230;
        _search.PlaceholderText = "Search " + title.ToLowerInvariant();
        _search.AccessibleName = "Search " + title;
        Commands.Items.Add(new ToolStripControlHost(_search));
        _debounce = new System.Windows.Forms.Timer { Interval = 180 };
        _search.TextChanged += (_, _) => { _debounce.Stop(); _debounce.Start(); };
        _debounce.Tick += (_, _) => { _debounce.Stop(); Grid.SearchPanelText = _search.Text; if (_cards != null) _cards.FilterText = _search.Text; };
        _summary = new Label { Dock = DockStyle.Bottom, Height = 30, Padding = new Padding(18, 0, 0, 0), TextAlign = ContentAlignment.MiddleLeft };
        _menu.Items.Add("Edit", null, (_, _) => { if (_context != null) Edit(_context); });
        _menu.Items.Add("Delete", null, (_, _) => { if (_context != null) Delete(_context); });
        _menu.Items.Add("Copy", null, (_, _) => Grid.CopySelectionToClipboard());
        Controls.Add(Grid); Controls.Add(_summary); Controls.Add(Commands); Controls.Add(SchoolUi.Heading(title));
        Store.Changed += StoreChanged;
        Grid.DataWindowChanged += (_, _) => _summary.Text = $"{Grid.TotalRecordCount:N0} records   ·   {Grid.PagerText}";
        SchoolUi.Apply(this);
        Reload();
    }

    protected void AddCommand(string title, Action action)
    {
        var button = new ToolStripButton(title);
        button.Click += (_, _) => { try { action(); } catch (Exception exception) { MessageBox.Show(FindForm(), exception.Message, _title, MessageBoxButtons.OK, MessageBoxIcon.Warning); } };
        Commands.Items.Add(button);
    }

    public EnterpriseCardViewControl? Cards => _cards;
    public EnterpriseFilterViewControl? Facets => _facets;

    /// <summary>Adds a card view of the same records, switched with the Cards / Grid command.</summary>
    protected void UseCards(string title, string subtitle, string badge, params (string Field, string Caption, string? Format)[] fields)
    {
        _cards = new EnterpriseCardViewControl { Dock = DockStyle.Fill, Visible = false, TitleField = title, SubtitleField = subtitle, BadgeField = badge, AccessibleName = _title + " cards" };
        foreach (var (field, caption, format) in fields) _cards.Fields.Add(new CardField(field, caption, format));
        var active = typeof(T).GetProperty("Active");
        _cards.CardFormatting += (_, e) => { if (active != null && active.GetValue(e.Item) is false) e.Accent = Color.Gray; };
        _cards.CardActivate += (_, item) => { if (item is T record) Edit(record); };
        _cards.DataSource = Store.Data.Records<T>();
        Controls.Add(_cards);
        Controls.SetChildIndex(_cards, Controls.GetChildIndex(Grid));
        AddCommand("Cards / Grid", () => { _cards.Visible = !_cards.Visible; Grid.Visible = !_cards.Visible; });
    }

    /// <summary>Adds a faceted filter panel (checklists with counts, optional ranges) that filters the grid.</summary>
    protected void UseFacets(string[] listFields, string[]? rangeFields = null)
    {
        _facets = new EnterpriseFilterViewControl { Dock = DockStyle.Left, Width = 290, Visible = false, AccessibleName = _title + " filters" };
        foreach (var field in listFields) _facets.AddListFacet(field, FacetTitle(field));
        foreach (var field in rangeFields ?? Array.Empty<string>()) _facets.AddRangeFacet(field, FacetTitle(field));
        _facets.DataSource = Store.Data.Records<T>();
        _facets.FilterChanged += (_, _) => ApplyFacets();
        Controls.Add(_facets);
        Controls.SetChildIndex(_facets, Controls.GetChildIndex(Grid) + 1);
        AddCommand("Facets", () => _facets.Visible = !_facets.Visible);
    }

    private static string FacetTitle(string field)
    {
        var match = SchoolSchema.Fields<T>().FirstOrDefault(item => item.Name == field || item.Name + "Label" == field);
        return match == null ? field : match.Label.Replace(" ID", string.Empty, StringComparison.Ordinal);
    }

    private void ApplyFacets()
    {
        var descriptors = _facets!.ToDescriptors();
        if (_facetFilters.Count == 0 && descriptors.Count == 0) return;
        foreach (var previous in _facetFilters) Grid.RemoveFilter(previous);
        _facetFilters.Clear();
        foreach (var descriptor in descriptors) { Grid.AddFilter(descriptor); _facetFilters.Add(descriptor); }
    }

    protected virtual T CreateRecord()
    {
        var record = new T();
        var next = SchoolOperations.NextId<T>(Store.Data);
        if (record is StudentRecord student) { student.StudentCode = $"ST-{next:D4}"; student.Email = "student@example.com"; }
        if (record is InvoiceRecord invoice) { invoice.Number = $"INV-{DateTime.Today.Year}-{next:D4}"; invoice.StudentId = Store.Data.Students.FirstOrDefault()?.Id ?? 0; invoice.Amount = 100; }
        return record;
    }

    protected void Edit(T record)
    {
        using var dialog = new SchoolRecordDialog<T>(Store, record, record.Id == 0 ? "Add " + _title : "Edit " + _title);
        dialog.ShowDialog(FindForm());
    }

    protected void Delete(T record)
    {
        if (MessageBox.Show(FindForm(), $"Delete record #{record.Id}?", "Confirm Deletion", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        try { SchoolOperations.DeleteRecord(Store, record); }
        catch (Exception exception) { MessageBox.Show(FindForm(), exception.Message, "Cannot Delete", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    public void Reload()
    {
        foreach (var field in SchoolSchema.Fields<T>().Where(field => field.Lookup != null))
        {
            var labels = field.Lookup!(Store.Data).ToDictionary(option => option.Id, option => option.Label);
            foreach (var record in Store.Data.Records<T>())
            {
                var id = Convert.ToInt32(typeof(T).GetProperty(field.Name)!.GetValue(record));
                typeof(T).GetProperty(field.Name + "Label")!.SetValue(record, labels.GetValueOrDefault(id) ?? id.ToString());
            }
        }
        Grid.DataSource = Store.Data.Records<T>();
        if (_cards != null) _cards.DataSource = Store.Data.Records<T>();
        if (_facets != null) _facets.DataSource = Store.Data.Records<T>();
        Grid.PrintTemplate.Title = Store.Data.Settings.SchoolName + " · " + _title;
        Grid.Invalidate();
    }

    private void StoreChanged(object? sender, EventArgs e) => Reload();
    private async Task ExportAsync(string format)
    {
        using var dialog = new SaveFileDialog { Filter = format.ToUpperInvariant() + "|*." + format, FileName = _title.ToLowerInvariant() + "." + format, DefaultExt = format };
        if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
        try { await Grid.ExportToFileAsync(dialog.FileName, new GridExportOptions { Title = _title }); }
        catch (Exception exception) { MessageBox.Show(FindForm(), exception.Message, "Export", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
    private void ManageLayout(bool save)
    {
        var path = Path.Combine(Path.GetDirectoryName(Store.FilePath)!, typeof(T).Name + "-layout.json");
        if (save) File.WriteAllText(path, GridLayoutSerializer.ToJson(Grid.CreateLayoutProfile(_title)));
        else if (File.Exists(path)) Grid.ApplyLayoutProfile(GridLayoutSerializer.FromJson(File.ReadAllText(path)));
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { Store.Changed -= StoreChanged; _debounce?.Dispose(); _menu?.Dispose(); }
        base.Dispose(disposing);
    }
}