using System.IO.Compression;
using EnterpriseUI.Data;
using EnterpriseUI.Grids;

namespace TestDataGrid;

public partial class Form1
{
    internal async Task VerifyGridFeaturesAsync()
    {
        var checks = 0;
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            Console.WriteLine($"PASS: {message}");
            checks++;
        }

        await PopulateGridAsync(GetData());
        await Task.Yield();
        PerformLayout();
        Check(dataGrid.TotalRecordCount == 100, "100 student records load");
        Check(_titleLabel.Bottom <= dataGrid.Top && _commands.Bottom <= dataGrid.Top && _views.Bottom <= dataGrid.Top, "Title and toolbars do not overlap the grid");
        Check(dataGrid.Bottom <= ClientSize.Height - 20, "Status bar does not overlap the grid");
        Check(dataGrid.Columns.Last().Field == "DeleteStudent" && dataGrid.Columns.Last().PinPosition == ColumnPinPosition.Right, "Actions column stays last and pinned");
        var first = _students[0];
        Check(Convert.ToDecimal(dataGrid.FindColumn("RemainingCredits")!.GetValue(first)) == 240 - first.Credits, "Calculated column evaluates");
        Check(Equals(dataGrid.FindColumn("DeleteStudent")!.GetValue(first), "Delete"), "Actions render a Delete command");
        Check(dataGrid.FooterValues.Count >= 3, "Footer summaries calculate");

        var relation = dataGrid.Relations.Single();
        Check(dataGrid.GetChildren(relation, first).Single() == first.Address, "Student.AddressId joins Address.Id");
        Check(dataGrid.GetChildren(relation.ChildRelations.Single(), first.Address).Count == 2, "Nested address contacts load");
        ConfigureHierarchy(StudentHierarchyMode.LazyDetails);
        await dataGrid.RefreshDataAsync();
        Check((await dataGrid.LoadChildrenAsync(dataGrid.Relations.Single(), first)).Count == 1, "Lazy detail loading works");
        ConfigureHierarchy(StudentHierarchyMode.Tree);
        await dataGrid.RefreshDataAsync();
        Check(dataGrid.IsTreeMode, "Self-referencing tree view activates");
        var collapsedRows = dataGrid.TotalItemsCount;
        dataGrid.ExpandAllTreeNodes();
        Check(dataGrid.TotalItemsCount > collapsedRows && dataGrid.TotalItemsCount <= dataGrid.PageSize, "Tree nodes expand within the current page");
        ConfigureHierarchy(StudentHierarchyMode.PropertySummary);
        await dataGrid.RefreshDataAsync();
        Check(dataGrid.Relations.Count == 0 && dataGrid.EnableMasterDetail, "Property-summary mode activates");
        ConfigureHierarchy(StudentHierarchyMode.MasterDetail);
        await dataGrid.RefreshDataAsync();

        dataGrid.GroupBy(nameof(Student.Department));
        await dataGrid.RefreshDataAsync();
        var selectedIndex = Enumerable.Range(0, dataGrid.TotalItemsCount).First(index => dataGrid.TryGetRowAt(index, out var row) && row.Kind == GridRowKind.Data);
        dataGrid.Selection.Click(selectedIndex, 0);
        dataGrid.TryGetRowAt(selectedIndex, out var selectedRow);
        Check(GetSelectedStudents().Single() == selectedRow.Item, "Grouped selection targets the correct student");
        dataGrid.ClearGroups();
        dataGrid.FilterBy(nameof(Student.Id), FilterOperator.IsBetween, 5, 10);
        await dataGrid.RefreshDataAsync();
        Check(dataGrid.TotalRecordCount == 6, "Range filtering works");
        dataGrid.SaveFilterPreset("Check");
        var filters = dataGrid.ExportFilterPresets();
        dataGrid.SavedFilterPresets.Clear();
        Check(dataGrid.ImportFilterPresets(filters).Count == 0 && dataGrid.ApplyFilterPreset("Check"), "Filter presets round-trip");
        dataGrid.ClearFilters();
        await dataGrid.RefreshDataAsync();
        var search = await dataGrid.SearchAsync("Name 0");
        Check(search.MatchCount > 0, "Search highlights matches");
        dataGrid.SearchPanelText = null;
        await dataGrid.RefreshDataAsync();

        var originalName = first.Name;
        var draft = dataGrid.BeginRowEdit(first);
        using (var dialog = new StudentEditDialog(draft, dataGrid.Columns))
        {
            dialog.Show(this);
            await Task.Yield();
            using (var image = new Bitmap(dialog.Width, dialog.Height))
            {
                dialog.DrawToBitmap(image, new Rectangle(Point.Empty, dialog.Size));
                image.Save(Path.Combine(Path.GetTempPath(), "TestDataGrid-edit-modal.png"), System.Drawing.Imaging.ImageFormat.Png);
            }
            FindControls(dialog).OfType<TextBox>().Single(input => input.AccessibleName == "Name").Text = "Cancelled change";
            dialog.CancelButton!.PerformClick();
            dialog.Close();
            draft.Cancel();
        }
        Check(first.Name == originalName, "Custom modal Cancel leaves the model unchanged");
        draft = dataGrid.BeginRowEdit(first);
        using (var dialog = new StudentEditDialog(draft, dataGrid.Columns))
        {
            dialog.Show(this);
            await Task.Yield();
            FindControls(dialog).OfType<TextBox>().Single(input => input.AccessibleName == "Name").Text = "Verified student";
            dialog.AcceptButton!.PerformClick();
            Check(dialog.DialogResult == DialogResult.OK && first.Name == "Verified student", "Custom modal Save commits valid fields");
            dialog.Close();
        }
        Check(!dataGrid.TryEditCell(first, nameof(Student.Gpa), 5m).Success, "GPA range validation rejects invalid edits");
        dataGrid.ClearValidationErrors();
        dataGrid.EditMode = GridEditMode.Batch;
        Check(dataGrid.TryEditCell(first, nameof(Student.Name), "Batch change").Success && dataGrid.HasPendingChanges, "Batch editing tracks pending changes");
        dataGrid.RollbackChanges();
        Check(first.Name == "Verified student", "Rollback restores committed values");
        dataGrid.EditMode = GridEditMode.Form;

        foreach (var mode in Enum.GetValues<GridPagingMode>())
        {
            dataGrid.PagingMode = mode;
            await dataGrid.RefreshDataAsync();
            Check(dataGrid.TotalRecordCount == 100 && dataGrid.LastError == null, $"{mode} paging loads");
            if (mode == GridPagingMode.Infinite && dataGrid.HasMoreRows)
                Check(await dataGrid.LoadMoreAsync(), "Infinite paging loads another window");
        }
        _simulatedRemote = true;
        dataGrid.PagingMode = GridPagingMode.Server;
        ConfigureSource();
        await dataGrid.RefreshDataAsync();
        Check(_remoteRequests > 0 && dataGrid.TotalRecordCount == 100, "Simulated remote paging honors the data-source contract");
        _simulatedRemote = false;
        ConfigureSource();
        dataGrid.PagingMode = GridPagingMode.Client;
        await dataGrid.RefreshDataAsync();

        _transferPanel.Visible = true;
        PerformLayout();
        Check(dataGrid.Right <= _transferPanel.Left, "Transfer target does not overlap the main grid");
        Check(dataGrid.TransferRows(new[] { first }, _transferGrid) == 1, "Cross-grid row transfer works");
        Check(_transferGrid.TransferRows(new[] { first }, dataGrid) == 1, "Transferred rows can return");
        RebuildAddressRelation();
        _transferPanel.Visible = false;
        ConfigureHeaderBands(true);
        Check(dataGrid.Bands.Count == 3, "Header bands configure");
        var layout = dataGrid.CreateLayoutProfile("Check");
        Check(dataGrid.ApplyLayoutProfile(GridLayoutSerializer.FromJson(GridLayoutSerializer.ToJson(layout))).Count == 0, "Layout JSON round-trips");
        await dataGrid.RefreshDataAsync();
        Check((await dataGrid.CreatePrintPlanAsync()).PageCount > 0, "Printing produces a page plan");

        var directory = Path.Combine(Path.GetTempPath(), "TestDataGrid-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            foreach (var format in new[] { "csv", "json", "xml", "html", "xlsx", "pdf" })
            {
                var path = Path.Combine(directory, "students." + format);
                await dataGrid.ExportToFileAsync(path, new GridExportOptions { IncludeHierarchy = true, IncludeGroups = true, IncludeBands = true, IncludeSummaries = true });
                Check(new FileInfo(path).Length > 0, $"{format.ToUpperInvariant()} export writes output");
                if (format == "xlsx")
                {
                    using var archive = ZipFile.OpenRead(path);
                    Check(archive.GetEntry("xl/workbook.xml") != null, "XLSX export contains a workbook");
                }
            }
        }
        finally { Directory.Delete(directory, true); }
        Check(dataGrid.DescribeGrid().Contains("Students"), "Accessible grid title is available");
        var clone = CloneStudent(first, 999999, 1999999);
        Check(clone.Id != first.Id && !ReferenceEquals(clone.Address, first.Address) && clone.Address.Id == clone.AddressId, "Copied students have independent identities and addresses");
        var email = first.Email;
        first.Email = "student@domain.invalid";
        Check((await dataGrid.ValidateAllAsync()).Any(error => error.Field == nameof(Student.Email)), "Async row validation reports reserved email domains");
        first.Email = email;
        dataGrid.ClearValidationErrors();
        using (var cancellation = new CancellationTokenSource())
        {
            cancellation.Cancel();
            try
            {
                await dataGrid.ReadAllAsync(cancellationToken: cancellation.Token);
                Check(false, "Cancelled reads stop");
            }
            catch (OperationCanceledException) { Check(true, "Cancelled reads stop"); }
        }
        ClientSize = new Size(1050, 650);
        PerformLayout();
        Check(_titleLabel.Bottom <= dataGrid.Top && _views.Bottom <= dataGrid.Top && dataGrid.Height > 300, "Compact-window layout remains usable");
        ClientSize = new Size(1280, 780);
        foreach (var count in new[] { 10000, 100000 })
        {
            await ReplaceDatasetAsync(GetData(count));
            Check(dataGrid.TotalRecordCount == count && dataGrid.LastError == null, $"{count:N0}-row dataset loads");
        }
        PerformLayout();
        using (var image = new Bitmap(Width, Height))
        {
            DrawToBitmap(image, new Rectangle(Point.Empty, Size));
            image.Save(Path.Combine(Path.GetTempPath(), "TestDataGrid-grid.png"), System.Drawing.Imaging.ImageFormat.Png);
        }
        Console.WriteLine($"Verified {checks} grid feature checks.");
    }

    private static IEnumerable<Control> FindControls(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var descendant in FindControls(child)) yield return descendant;
        }
    }
}