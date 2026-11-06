using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Data;
using EnterpriseUI.Exporting;
using EnterpriseUI.Grids;
using EnterpriseUI.Theming;

namespace TestDataGrid;

public partial class Form1
{
    private Label _titleLabel = null!;
    private ToolStrip _commands = null!;
    private ToolStrip _views = null!;
    private ToolStripStatusLabel _status = null!;
    private ToolStripProgressBar _progress = null!;
    private ToolStripButton _cancel = null!;
    private ToolStripButton _undo = null!;
    private ToolStripButton _redo = null!;
    private ToolStripButton _save = null!;
    private ToolStripButton _discard = null!;
    private ToolStripButton _edit = null!;
    private System.Windows.Forms.Timer _liveTimer = null!;
    private CancellationTokenSource? _operationCancellation;
    private ColumnLayoutProfile? _baselineLayout;
    private bool _busy;
    private bool _editingDialogOpen;
    private ToolStripComboBox _searchModeSelector = null!;
    private bool _searchModeBound;

    private void InitializeFeatureWorkspace()
    {
        ClientSize = new Size(1280, 780);
        MinimumSize = new Size(1050, 600);
        _commands = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden, CanOverflow = true };
        _views = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden, CanOverflow = true };
        _status = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        _progress = new ToolStripProgressBar { Minimum = 0, Maximum = 100, Visible = false, Width = 140 };
        var statusStrip = new StatusStrip();
        statusStrip.Items.AddRange(new ToolStripItem[] { _status, _progress });
        Controls.Add(statusStrip);
        statusStrip.SendToBack();
        Controls.Add(_views);
        _views.SendToBack();
        Controls.Add(_commands);
        _commands.SendToBack();
        _titleLabel.SendToBack();

        ToolStripButton Command(string text, Func<Task> action, bool canCancel = false)
        {
            var button = new ToolStripButton(text) { ToolTipText = text };
            button.Click += async (_, _) => await RunGridOperationAsync(action, canCancel);
            _commands.Items.Add(button);
            return button;
        }

        Command("Add", AddStudentAsync);
        _edit = Command("Edit", () => { if (GetSelectedStudents().FirstOrDefault() is { } student) EditStudent(student); return Task.CompletedTask; });
        Command("Delete", () => DeleteStudentsAsync(GetSelectedStudents()));
        _commands.Items.Add(new ToolStripSeparator());
        _undo = Command("Undo", () => { dataGrid.UndoLastEdit(); return dataGrid.RefreshDataAsync(); });
        _redo = Command("Redo", () => { dataGrid.RedoLastEdit(); return dataGrid.RefreshDataAsync(); });
        _save = Command("Save Changes", () => { dataGrid.CommitChanges(); return dataGrid.RefreshDataAsync(); });
        _discard = Command("Discard", () =>
        {
            if (MessageBox.Show(this, "Discard pending changes?", "Discard Changes", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
                dataGrid.RollbackChanges();
            return dataGrid.RefreshDataAsync();
        });
        Command("Refresh", () => dataGrid.RefreshDataAsync(_operationCancellation!.Token), canCancel: true);

        var export = new ToolStripDropDownButton("Export");
        foreach (var format in new[] { "xlsx", "csv", "pdf", "html", "json", "xml" })
        {
            var extension = format;
            AddMenuCommand(export.DropDownItems, extension.ToUpperInvariant(), () => ExportStudentsAsync(extension), canCancel: true);
        }
        _commands.Items.Add(export);
        var print = new ToolStripDropDownButton("Print");
        AddMenuCommand(print.DropDownItems, "Preview", () => dataGrid.ShowPrintPreviewDialogAsync());
        AddMenuCommand(print.DropDownItems, "Print", () => dataGrid.PrintAsync());
        AddMenuCommand(print.DropDownItems, "Page Setup", () => { dataGrid.ShowPageSetupDialog(); return Task.CompletedTask; });
        AddToggle(print.DropDownItems, "Landscape", false, value => dataGrid.PrintTemplate.Settings.IsLandscape = value);
        _commands.Items.Add(print);

        var query = new ToolStripDropDownButton("Query");
        AddMenuCommand(query.DropDownItems, "Filter...", ShowFilterDialogAsync);
        AddMenuCommand(query.DropDownItems, "Active Students", () => { dataGrid.FilterBy(nameof(Student.IsActive), FilterOperator.IsEqualTo, true); return dataGrid.RefreshDataAsync(); });
        AddMenuCommand(query.DropDownItems, "Clear Filters", () => { dataGrid.ClearFilters(); dataGrid.SearchPanelText = null; return dataGrid.RefreshDataAsync(); });
        AddMenuCommand(query.DropDownItems, "Sort by Name", () => { dataGrid.SortBy(nameof(Student.Name), ListSortDirection.Ascending); return dataGrid.RefreshDataAsync(); });
        AddMenuCommand(query.DropDownItems, "Clear Sorting", () => { dataGrid.ClearSort(); return dataGrid.RefreshDataAsync(); });
        foreach (var field in new[] { nameof(Student.Department), "Address.Country", nameof(Student.IsActive) })
        {
            var member = field;
            AddMenuCommand(query.DropDownItems, "Group by " + field, () => { dataGrid.GroupBy(member); return dataGrid.RefreshDataAsync(); });
        }
        AddMenuCommand(query.DropDownItems, "Clear Groups", () => { dataGrid.ClearGroups(); return dataGrid.RefreshDataAsync(); });
        AddMenuCommand(query.DropDownItems, "Expand Groups", () => { dataGrid.ExpandAllGroups(); return Task.CompletedTask; });
        AddMenuCommand(query.DropDownItems, "Collapse Groups", () => { dataGrid.CollapseAllGroups(); return Task.CompletedTask; });
        AddMenuCommand(query.DropDownItems, "Expand Details", () => { dataGrid.ExpandAllDetails(); return Task.CompletedTask; });
        AddMenuCommand(query.DropDownItems, "Collapse Details", () => { dataGrid.CollapseAllDetails(); return Task.CompletedTask; });
        AddMenuCommand(query.DropDownItems, "Save Filter Preset...", () => SaveFilterPresetAsync());
        AddMenuCommand(query.DropDownItems, "Apply Filter Preset...", () => ApplyFilterPresetAsync());
        AddMenuCommand(query.DropDownItems, "Export Filter Presets...", () => SaveJsonAsync(dataGrid.ExportFilterPresets(), "filters.json"));
        AddMenuCommand(query.DropDownItems, "Import Filter Presets...", ImportFilterPresetsAsync);
        _commands.Items.Add(query);

        var layout = new ToolStripDropDownButton("Layout");
        AddMenuCommand(layout.DropDownItems, "Best Fit", () => { dataGrid.BestFitAllColumns(); return Task.CompletedTask; });
        AddMenuCommand(layout.DropDownItems, "Save Layout...", () => SaveJsonAsync(GridLayoutSerializer.ToJson(dataGrid.CreateLayoutProfile("Students")), "students-layout.json"));
        AddMenuCommand(layout.DropDownItems, "Load Layout...", LoadLayoutAsync);
        AddMenuCommand(layout.DropDownItems, "Reset Layout", () => { if (_baselineLayout != null) dataGrid.ApplyLayoutProfile(_baselineLayout); return dataGrid.RefreshDataAsync(); });
        AddToggle(layout.DropDownItems, "Header Bands", false, ConfigureHeaderBands);
        AddToggle(layout.DropDownItems, "Conditional Formatting", true, value => { foreach (var rule in dataGrid.FormatRules) rule.Enabled = value; dataGrid.Invalidate(); });
        _commands.Items.Add(layout);

        var tools = new ToolStripDropDownButton("Tools");
        AddMenuCommand(tools.DropDownItems, "Copy Selection", () => { dataGrid.CopySelectionToClipboard(); return Task.CompletedTask; });
        AddMenuCommand(tools.DropDownItems, "Move Selected to Top", () => { dataGrid.MoveRows(GetSelectedStudents(), _students.FirstOrDefault()); return dataGrid.RefreshDataAsync(); });
        AddMenuCommand(tools.DropDownItems, "Validate All", async () => { var errors = await dataGrid.ValidateAllAsync(); ShowText("Validation", errors.Count == 0 ? "No validation errors." : string.Join(Environment.NewLine, errors.Select(error => $"{error.Field}: {error.Message}"))); });
        AddMenuCommand(tools.DropDownItems, "Clear Validation", () => { dataGrid.ClearValidationErrors(); return Task.CompletedTask; });
        AddMenuCommand(tools.DropDownItems, "Accessibility", () => { ShowText("Accessibility", dataGrid.DescribeGrid() + Environment.NewLine + JsonSerializer.Serialize(GridAccessibility.Audit(ThemeManager.Instance.CurrentTheme), new JsonSerializerOptions { WriteIndented = true })); return Task.CompletedTask; });
        AddMenuCommand(tools.DropDownItems, "Copy C# Columns", () => { Clipboard.SetText(CreateColumnCode()); return Task.CompletedTask; });
        AddMenuCommand(tools.DropDownItems, "Copy Feature Catalog", () => { Clipboard.SetText(GridFeatureCatalog.ToMarkdown()); return Task.CompletedTask; });
        AddMenuCommand(tools.DropDownItems, "Measure Read / Render", async () =>
        {
            var watch = Stopwatch.StartNew();
            var rows = await dataGrid.ReadAllAsync(100000);
            var readTime = watch.ElapsedMilliseconds;
            using var bitmap = new Bitmap(dataGrid.Width, dataGrid.Height);
            dataGrid.DrawToBitmap(bitmap, dataGrid.ClientRectangle);
            ShowText("Measurements", $"Rows: {rows.Count:N0}\r\nRead: {readTime:N0} ms\r\nRender: {watch.ElapsedMilliseconds - readTime:N0} ms\r\nPrivate memory: {Process.GetCurrentProcess().PrivateMemorySize64 / 1048576:N0} MB");
        });
        _commands.Items.Add(tools);
        _cancel = new ToolStripButton("Cancel") { Enabled = false, ToolTipText = "Cancel current operation" };
        _cancel.Click += (_, _) => _operationCancellation?.Cancel();
        _commands.Items.Add(_cancel);

        AddEnumSelector<GridSelectionMode>("Selection", GridSelectionMode.MultipleRows, value => { dataGrid.SelectionMode = value; return Task.CompletedTask; });
        AddEnumSelector<GridEditMode>("Edit", GridEditMode.Form, value =>
        {
            if (dataGrid.HasPendingChanges) { MessageBox.Show(this, "Save or discard pending changes first.", "Pending Changes", MessageBoxButtons.OK, MessageBoxIcon.Warning); return Task.CompletedTask; }
            dataGrid.EditMode = value;
            return Task.CompletedTask;
        });
        _searchModeSelector = AddEnumSelector<GridSearchMode>("Search", GridSearchMode.Highlight, value => { dataGrid.SearchMode = value; SyncSearchModeControl(); return dataGrid.RefreshDataAsync(); });
        AddEnumSelector<GridClipboardCopyMode>("Copy", GridClipboardCopyMode.CellsWithHeaders, value => { dataGrid.ClipboardCopyMode = value; return Task.CompletedTask; });
        var themes = new ToolStripComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 145 };
        themes.Items.AddRange(ThemeManager.Instance.AvailableThemes.Select(theme => (object)theme.Name).ToArray());
        themes.SelectedItem = ThemeManager.Instance.CurrentTheme.Name;
        themes.SelectedIndexChanged += (_, _) => { if (themes.SelectedItem is string theme) ThemeManager.Instance.ApplyTheme(theme); };
        _views.Items.Add(new ToolStripLabel("Theme"));
        _views.Items.Add(themes);
        var view = new ToolStripDropDownButton("View");
        AddToggle(view.DropDownItems, "Search", true, value => dataGrid.ShowSearchPanel = value);
        AddToggle(view.DropDownItems, "Filter Row", true, value => dataGrid.ShowFilterRow = value);
        AddToggle(view.DropDownItems, "Group Panel", true, value => dataGrid.ShowGroupPanel = value);
        AddToggle(view.DropDownItems, "Summaries", true, value => dataGrid.ShowSummaryFooter = dataGrid.ShowColumnSummaries = value);
        AddToggle(view.DropDownItems, "Alternate Rows", true, value => dataGrid.EnableAlternateRows = value);
        AddToggle(view.DropDownItems, "Row Drag / Drop", true, value => dataGrid.AllowRowDrag = dataGrid.AllowRowDrop = value);
        AddToggle(view.DropDownItems, "Live Updates", false, value => _liveTimer.Enabled = value);
        _views.Items.Add(view);

        _liveTimer = new System.Windows.Forms.Timer(components!) { Interval = 1500 };
        _liveTimer.Tick += (_, _) =>
        {
            if (_busy || _editingDialogOpen || dataGrid.HasPendingChanges || _students.Count == 0 || dataGrid.IsEditing) return;
            var student = _students[Random.Shared.Next(_students.Count)];
            student.Credits = (student.Credits + 1) % 241;
        };
        dataGrid.DataWindowChanged += (_, _) => UpdateWorkspaceStatus();
        dataGrid.SelectionChanged += (_, _) => UpdateWorkspaceStatus();
        dataGrid.UndoStackChanged += (_, _) => UpdateWorkspaceStatus();
        dataGrid.ValidationChanged += (_, _) => UpdateWorkspaceStatus();
        dataGrid.PropertyChanged += (_, _) => UpdateWorkspaceStatus();
        ThemeManager.Instance.ThemeChanged += WorkspaceThemeChanged;
        FormClosed += (_, _) => { _operationCancellation?.Cancel(); ThemeManager.Instance.ThemeChanged -= WorkspaceThemeChanged; };

        _studentMenu.Items.Add("Delete", null, async (_, _) => { if (_contextStudent != null) await RunGridOperationAsync(() => DeleteStudentsAsync(new[] { _contextStudent })); });
        _studentMenu.Items.Add(new ToolStripSeparator());
        _studentMenu.Items.Add("Copy", null, (_, _) => dataGrid.CopySelectionToClipboard());
        _studentMenu.Items.Add("Undo", null, (_, _) => { dataGrid.UndoLastEdit(); UpdateWorkspaceStatus(); });
        _studentMenu.Items.Add("Redo", null, (_, _) => { dataGrid.RedoLastEdit(); UpdateWorkspaceStatus(); });
        _studentMenu.Opening += (_, _) => _studentMenu.Items[0].Enabled = dataGrid.EditMode != GridEditMode.None && !_busy;
        InitializeDataModes(tools, view);
        ApplyWorkspacePalette();
    }

    private void AddMenuCommand(ToolStripItemCollection items, string text, Func<Task> action, bool canCancel = false)
    {
        var item = new ToolStripMenuItem(text);
        item.Click += async (_, _) => await RunGridOperationAsync(action, canCancel);
        items.Add(item);
    }

    private static void AddToggle(ToolStripItemCollection items, string text, bool initial, Action<bool> change)
    {
        var item = new ToolStripMenuItem(text) { CheckOnClick = true, Checked = initial };
        item.CheckedChanged += (_, _) => change(item.Checked);
        items.Add(item);
    }

    private ToolStripComboBox AddEnumSelector<TEnum>(string label, TEnum selected, Func<TEnum, Task> changed) where TEnum : struct, Enum
    {
        var combo = new ToolStripComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
        combo.Items.AddRange(Enum.GetValues<TEnum>().Cast<object>().ToArray());
        combo.SelectedItem = selected;
        combo.SelectedIndexChanged += async (_, _) => { if (combo.SelectedItem is TEnum value) await RunGridOperationAsync(() => changed(value)); };
        _views.Items.Add(new ToolStripLabel(label));
        _views.Items.Add(combo);
        return combo;
    }

    private async Task RunGridOperationAsync(Func<Task> action, bool canCancel = false)
    {
        if (_busy) return;
        _busy = true;
        _operationCancellation = new CancellationTokenSource();
        _cancel.Enabled = canCancel;
        _views.Enabled = false;
        dataGrid.Enabled = false;
        _transferGrid.Enabled = false;
        foreach (ToolStripItem command in _commands.Items) if (command != _cancel) command.Enabled = false;
        UseWaitCursor = true;
        try { await action(); }
        catch (OperationCanceledException) { _status.Text = "Operation cancelled."; }
        catch (Exception exception) { MessageBox.Show(this, exception.Message, "Grid Operation", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally
        {
            _operationCancellation.Dispose();
            _operationCancellation = null;
            _busy = false;
            if (!IsDisposed)
            {
                _cancel.Enabled = false;
                _progress.Visible = false;
                UseWaitCursor = false;
                _views.Enabled = dataGrid.Enabled = _transferGrid.Enabled = true;
                foreach (ToolStripItem command in _commands.Items) if (command != _cancel) command.Enabled = true;
                UpdateWorkspaceStatus();
            }
        }
    }

    private IReadOnlyList<Student> GetSelectedStudents()
    {
        var indices = dataGrid.Selection.SelectedRows.Concat(dataGrid.Selection.CellRanges.SelectMany(range => Enumerable.Range(range.RowFrom, range.RowTo - range.RowFrom + 1))).Distinct();
        var selected = new List<Student>();
        foreach (var index in indices)
            if (dataGrid.TryGetRowAt(index, out var selectedRow) && selectedRow.Kind == GridRowKind.Data && selectedRow.Item != null)
                selected.Add(selectedRow.Item);
        if (selected.Count == 0 && dataGrid.TryGetRowAt(dataGrid.Selection.FocusedRow, out var row) && row.Kind == GridRowKind.Data && row.Item != null)
            selected.Add(row.Item);
        return selected;
    }

    private async Task DeleteStudentsAsync(IEnumerable<Student> students)
    {
        var rows = students.Distinct().ToList();
        if (rows.Count == 0) return;
        if (MessageBox.Show(this, $"Delete {rows.Count} student record(s)?", "Confirm Deletion", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        dataGrid.RemoveRows(rows);
        await dataGrid.RefreshDataAsync();
    }

    private async Task AddStudentAsync()
    {
        var id = _students.Count == 0 ? 1 : _students.Max(student => student.Id) + 1;
        var addressId = _students.Count == 0 ? 1000 : _students.Max(student => student.AddressId) + 1;
        var student = new Student { Id = id, AddressId = addressId, Name = "New student", Email = $"student{id}@example.com", IsActive = true, ProfileUrl = $"https://example.com/students/{id}", Address = new Address { Id = addressId } };
        var session = dataGrid.BeginRowEdit(student);
        using var dialog = new StudentEditDialog(session, dataGrid.Columns);
        if (dialog.ShowDialog(this) != DialogResult.OK) { session.Cancel(); return; }
        if (!dataGrid.AddRow(student)) throw new InvalidOperationException("The current data source cannot add rows.");
        RebuildAddressRelation();
        await dataGrid.RefreshDataAsync();
    }

    private void RebuildAddressRelation()
    {
        var relation = dataGrid.Relations.FirstOrDefault(relation => relation.RelationName == "Address");
        if (relation != null && _hierarchyMode != StudentHierarchyMode.LazyDetails) { relation.ChildSource = _students.Select(student => student.Address).ToList(); dataGrid.NotifyRelationsChanged(); }
    }

    private async Task ExportStudentsAsync(string extension)
    {
        using var dialog = new SaveFileDialog { Filter = $"{extension.ToUpperInvariant()} files|*.{extension}", FileName = $"students.{extension}", DefaultExt = extension, AddExtension = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _progress.Visible = true;
        _progress.Style = ProgressBarStyle.Continuous;
        _progress.Value = 0;
        var progress = new Progress<ExportProgress>(update =>
        {
            if (IsDisposed) return;
            _status.Text = $"Exported {update.RowsProcessed:N0} rows";
            if (update.TotalRows is > 0) _progress.Value = (int)Math.Clamp(update.RowsProcessed * 100d / update.TotalRows.Value, 0, 100);
        });
        var result = await dataGrid.ExportToFileAsync(dialog.FileName, new GridExportOptions { Title = "Students", SheetName = "Students", IncludeHierarchy = true, IncludeBands = true, IncludeGroups = true, IncludeSummaries = true }, progress: progress, cancellationToken: _operationCancellation!.Token);
        ShowText("Export Complete", $"Rows: {result.Rows:N0}\r\nFile: {dialog.FileName}");
    }

    private void ConfigureHeaderBands(bool enabled)
    {
        dataGrid.Bands.Clear();
        if (enabled)
        {
            foreach (var group in new[]
            {
                ("Identity", new[] { nameof(Student.Id), nameof(Student.Name), nameof(Student.Email) }),
                ("Address", new[] { "Address.City", "Address.Country" }),
                ("Academic", new[] { nameof(Student.Department), nameof(Student.EnrolledOn), nameof(Student.IsActive), nameof(Student.Credits), nameof(Student.Gpa), nameof(Student.Completion), "RemainingCredits", "RunningCredits" })
            })
            {
                var band = new GridColumnBand { Title = group.Item1 };
                foreach (var field in group.Item2) if (dataGrid.FindColumn(field) is { } column) band.Columns.Add(column);
                dataGrid.Bands.Add(band);
            }
        }
        dataGrid.ShowBandedHeaders = enabled;
        dataGrid.Invalidate();
    }

    private Task SaveJsonAsync(string json, string fileName)
    {
        using var dialog = new SaveFileDialog { Filter = "JSON files|*.json", FileName = fileName, DefaultExt = "json" };
        return dialog.ShowDialog(this) == DialogResult.OK ? File.WriteAllTextAsync(dialog.FileName, json) : Task.CompletedTask;
    }

    private async Task LoadLayoutAsync()
    {
        using var dialog = new OpenFileDialog { Filter = "JSON files|*.json" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var warnings = dataGrid.ApplyLayoutProfile(GridLayoutSerializer.FromJson(await File.ReadAllTextAsync(dialog.FileName)));
        if (warnings.Count > 0) ShowText("Layout Warnings", string.Join(Environment.NewLine, warnings));
        await dataGrid.RefreshDataAsync();
    }

    private async Task ImportFilterPresetsAsync()
    {
        using var dialog = new OpenFileDialog { Filter = "JSON files|*.json" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var warnings = dataGrid.ImportFilterPresets(await File.ReadAllTextAsync(dialog.FileName));
        if (warnings.Count > 0) ShowText("Filter Warnings", string.Join(Environment.NewLine, warnings));
    }

    private Task SaveFilterPresetAsync()
    {
        var name = PromptText("Save Filter Preset", "Name", "Students");
        if (!string.IsNullOrWhiteSpace(name)) dataGrid.SaveFilterPreset(name.Trim());
        return Task.CompletedTask;
    }

    private Task ApplyFilterPresetAsync()
    {
        var name = PromptText("Apply Filter Preset", "Name", dataGrid.SavedFilterPresets.Keys.FirstOrDefault() ?? "Students");
        if (name == null) return Task.CompletedTask;
        if (!dataGrid.ApplyFilterPreset(name)) throw new InvalidOperationException("Filter preset was not found.");
        return dataGrid.RefreshDataAsync();
    }

    private Task ShowFilterDialogAsync()
    {
        using var dialog = new Form { Text = "Filter Students", ClientSize = new Size(430, 230), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false };
        var field = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, DisplayMember = "Title" };
        field.Items.AddRange(dataGrid.Columns.Where(column => column.Filterable && !column.IsCalculated && !column.IsRunningTotal).Cast<object>().ToArray());
        field.SelectedIndex = 0;
        var op = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, DataSource = Enum.GetValues<FilterOperator>() };
        var first = new TextBox { Dock = DockStyle.Fill };
        var second = new TextBox { Dock = DockStyle.Fill };
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5, Padding = new Padding(16) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var row in new[] { ("Column", (Control)field), ("Operator", op), ("Value", first), ("Second value", second) }) { table.Controls.Add(new Label { Text = row.Item1, AutoSize = true }); table.Controls.Add(row.Item2); }
        var apply = new Button { Text = "Apply", DialogResult = DialogResult.OK, Width = 90 };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 90 };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.Add(cancel); buttons.Controls.Add(apply);
        dialog.Controls.Add(table); dialog.Controls.Add(buttons); dialog.AcceptButton = apply; dialog.CancelButton = cancel;
        if (dialog.ShowDialog(this) != DialogResult.OK) return Task.CompletedTask;
        var column = (GridColumn)field.SelectedItem!;
        var valueType = PropertyPath.GetPathType(typeof(Student), column.Field) ?? typeof(string);
        object? ConvertValue(string value) => value.Length == 0 ? null : Convert.ChangeType(value, Nullable.GetUnderlyingType(valueType) ?? valueType, CultureInfo.CurrentCulture);
        dataGrid.FilterBy(column.Field, (FilterOperator)op.SelectedItem!, ConvertValue(first.Text), ConvertValue(second.Text));
        return dataGrid.RefreshDataAsync();
    }

    private string? PromptText(string title, string label, string initial)
    {
        using var dialog = new Form { Text = title, ClientSize = new Size(360, 130), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false };
        var input = new TextBox { Text = initial, Dock = DockStyle.Top };
        var caption = new Label { Text = label, Dock = DockStyle.Top, Height = 28 };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 38, FlowDirection = FlowDirection.RightToLeft };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK };
        buttons.Controls.Add(cancel); buttons.Controls.Add(ok);
        dialog.Padding = new Padding(12); dialog.Controls.Add(input); dialog.Controls.Add(caption); dialog.Controls.Add(buttons); dialog.AcceptButton = ok; dialog.CancelButton = cancel;
        return dialog.ShowDialog(this) == DialogResult.OK ? input.Text : null;
    }

    private void ShowText(string title, string text)
    {
        using var dialog = new Form { Text = title, ClientSize = new Size(640, 420), StartPosition = FormStartPosition.CenterParent };
        dialog.Controls.Add(new TextBox { Text = text, Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false });
        dialog.ShowDialog(this);
    }

    private string CreateColumnCode()
    {
        var code = new StringBuilder("// Columns for EnterpriseDataGrid<Student>\r\n");
        foreach (var column in dataGrid.Columns)
        {
            var type = column is GridCommandColumn ? "GridCommandColumn" : "GridColumn";
            code.AppendLine($"grid.Columns.Add(new {type} {{ Field = {JsonSerializer.Serialize(column.Field)}, Title = {JsonSerializer.Serialize(column.Title)}, Width = {(column.Width ?? 120).ToString(CultureInfo.InvariantCulture)}d, ColumnType = GridColumnType.{column.ColumnType}, ReadOnly = {column.ReadOnly.ToString().ToLowerInvariant()}, Expression = {JsonSerializer.Serialize(column.Expression)}, RunningTotalOf = {JsonSerializer.Serialize(column.RunningTotalOf)} }});");
        }
        return code.ToString();
    }

    private void UpdateWorkspaceStatus()
    {
        if (_status == null || IsDisposed) return;
        _status.Text = $"{dataGrid.TotalRecordCount:N0} students | {dataGrid.PagerText} | Selected: {GetSelectedStudents().Count} | Pending: {dataGrid.PendingChanges.Count} | Errors: {dataGrid.ValidationSummary.Count} | Requests: {_remoteRequests}";
        _undo.Enabled = !_busy && dataGrid.CanUndo;
        _redo.Enabled = !_busy && dataGrid.CanRedo;
        _save.Enabled = _discard.Enabled = !_busy && dataGrid.HasPendingChanges;
        _edit.Enabled = dataGrid.EditMode != GridEditMode.None && !_busy;
        _status.ToolTipText = _status.Text;
    }

    private void WorkspaceThemeChanged(object? sender, ITheme theme) => ApplyWorkspacePalette();

    private void SyncSearchModeControl()
    {
        var checkbox = FindControls(dataGrid).OfType<CheckBox>().FirstOrDefault(control => control.Text == "Filter rows");
        if (checkbox == null) return;
        if (!_searchModeBound)
        {
            checkbox.CheckedChanged += (_, _) => _searchModeSelector.SelectedItem = checkbox.Checked ? GridSearchMode.Filter : GridSearchMode.Highlight;
            _searchModeBound = true;
        }
        checkbox.Checked = dataGrid.SearchMode == GridSearchMode.Filter;
    }

    private void ApplyWorkspacePalette()
    {
        var palette = GridPalette.FromTheme(ThemeManager.Instance.CurrentTheme);
        _titleLabel.BackColor = ColorTranslator.FromHtml(palette.Header);
        _titleLabel.ForeColor = ColorTranslator.FromHtml(palette.Text);
        BackColor = _commands.BackColor = _views.BackColor = ColorTranslator.FromHtml(palette.Surface);
        _commands.ForeColor = _views.ForeColor = ColorTranslator.FromHtml(palette.Text);
        dataGrid.PrintTemplate.Title = "Students";
    }
}