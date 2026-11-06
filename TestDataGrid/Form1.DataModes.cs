using System.ComponentModel;
using EnterpriseUI.Data;
using EnterpriseUI.Grids;

namespace TestDataGrid;

public partial class Form1
{
    private enum StudentHierarchyMode { MasterDetail, LazyDetails, Tree, PropertySummary }
    private StudentHierarchyMode _hierarchyMode = StudentHierarchyMode.MasterDetail;
    private bool _simulatedRemote;
    private bool _failNextRequest;
    private int _remoteRequests;
    private Panel _transferPanel = null!;
    private StudentDataGrid _transferGrid = null!;

    private void InitializeDataModes(ToolStripDropDownButton tools, ToolStripDropDownButton view)
    {
        var data = new ToolStripDropDownButton("Data");
        var paging = new ToolStripMenuItem("Paging");
        foreach (var value in Enum.GetValues<GridPagingMode>())
        {
            var mode = value;
            AddMenuCommand(paging.DropDownItems, mode.ToString(), async () =>
            {
                if (_hierarchyMode == StudentHierarchyMode.Tree && mode != GridPagingMode.Client)
                    throw new InvalidOperationException("Tree view uses client paging.");
                dataGrid.PagingMode = mode;
                await dataGrid.RefreshDataAsync();
            });
        }
        data.DropDownItems.Add(paging);
        AddMenuCommand(data.DropDownItems, "Load More", async () => { if (dataGrid.PagingMode != GridPagingMode.Infinite) throw new InvalidOperationException("Load More requires infinite paging."); await dataGrid.LoadMoreAsync(_operationCancellation!.Token); }, canCancel: true);
        AddMenuCommand(data.DropDownItems, "Local Source", async () => { _simulatedRemote = false; ConfigureSource(); await dataGrid.RefreshDataAsync(); });
        AddMenuCommand(data.DropDownItems, "Simulated Remote Source", async () =>
        {
            if (_hierarchyMode == StudentHierarchyMode.Tree) throw new InvalidOperationException("Switch from tree view before selecting a remote source.");
            _simulatedRemote = true;
            dataGrid.PagingMode = GridPagingMode.Server;
            ConfigureSource();
            await dataGrid.RefreshDataAsync();
        });
        AddMenuCommand(data.DropDownItems, "Fail Next Remote Request", async () => { if (!_simulatedRemote) throw new InvalidOperationException("No remote source is selected."); _failNextRequest = true; await dataGrid.RefreshDataAsync(); });
        AddMenuCommand(data.DropDownItems, "Retry", () => dataGrid.RefreshDataAsync());
        AddMenuCommand(data.DropDownItems, "Copy OData Query", () => { Clipboard.SetText(RemoteDataSource<Student>.ToODataParameters(dataGrid.CurrentRequest)); return Task.CompletedTask; });
        var datasets = new ToolStripMenuItem("Dataset Size");
        foreach (var size in new[] { 100, 10000, 100000 })
        {
            var count = size;
            AddMenuCommand(datasets.DropDownItems, count.ToString("N0"), async () =>
            {
                if (dataGrid.HasPendingChanges) throw new InvalidOperationException("Save or discard pending changes before replacing the dataset.");
                var rows = await Task.Run(() => GetData(count, _operationCancellation!.Token), _operationCancellation!.Token);
                await ReplaceDatasetAsync(rows);
                _baselineLayout = dataGrid.CreateLayoutProfile("Students");
            }, canCancel: true);
        }
        data.DropDownItems.Add(datasets);
        _views.Items.Add(data);

        var hierarchy = new ToolStripDropDownButton("Hierarchy");
        foreach (var value in Enum.GetValues<StudentHierarchyMode>())
        {
            var mode = value;
            AddMenuCommand(hierarchy.DropDownItems, mode.ToString(), async () => { ConfigureHierarchy(mode); await dataGrid.RefreshDataAsync(); });
        }
        AddMenuCommand(hierarchy.DropDownItems, "Expand Tree", () => { dataGrid.ExpandAllTreeNodes(); return Task.CompletedTask; });
        AddMenuCommand(hierarchy.DropDownItems, "Collapse Tree", () => { dataGrid.CollapseAllTreeNodes(); return Task.CompletedTask; });
        _views.Items.Add(hierarchy);

        _transferGrid = new StudentDataGrid
        {
            Dock = DockStyle.Fill,
            AllowRowDrag = true,
            AllowRowDrop = true,
            ShowGroupPanel = false,
            ShowSearchPanel = true,
            ShowFilterRow = true,
            EnableMasterDetail = false,
            SelectionMode = GridSelectionMode.MultipleRows,
            AriaLabel = "Transferred students",
            DataSource = new System.Collections.ObjectModel.ObservableCollection<Student>()
        };
        _transferGrid.Columns.Add(new GridColumn { Field = nameof(Student.Id), Title = "ID", Width = 60, ReadOnly = true });
        _transferGrid.Columns.Add(new GridColumn { Field = nameof(Student.Name), Title = "Name", Width = 180 });
        _transferGrid.Columns.Add(new GridColumn { Field = nameof(Student.Department), Title = "Department", Width = 140 });
        _transferPanel = new Panel { Dock = DockStyle.Right, Width = 350, Visible = false };
        _transferPanel.Controls.Add(_transferGrid);
        var targetTitle = new Label { Text = "Transferred Students", Dock = DockStyle.Top, Height = 36, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0) };
        _transferPanel.Controls.Add(targetTitle);
        var returnButton = new Button { Text = "Return Selected", ForeColor = SystemColors.ControlText, Dock = DockStyle.Bottom, Height = 36 };
        returnButton.Click += async (_, _) => await RunGridOperationAsync(async () =>
        {
            var selected = _transferGrid.Selection.SelectedRows.Select(index => _transferGrid.TryGetRowAt(index, out var row) ? row.Item : null).OfType<Student>().ToList();
            _transferGrid.TransferRows(selected, dataGrid);
            RebuildAddressRelation();
            await dataGrid.RefreshDataAsync();
        });
        _transferPanel.Controls.Add(returnButton);
        Controls.Add(_transferPanel);
        _transferPanel.SendToBack();
        AddToggle(view.DropDownItems, "Transfer Target", false, value => _transferPanel.Visible = value);
        AddMenuCommand(tools.DropDownItems, "Transfer Selected", async () => { _transferPanel.Visible = true; dataGrid.TransferRows(GetSelectedStudents(), _transferGrid); await dataGrid.RefreshDataAsync(); });
        AddMenuCommand(tools.DropDownItems, "Copy to Transfer Target", async () =>
        {
            _transferPanel.Visible = true;
            var existing = _students.Concat(_transferGrid.DataSource ?? Enumerable.Empty<Student>()).ToList();
            var id = existing.Count == 0 ? 1 : existing.Max(student => student.Id) + 1;
            var addressId = existing.Count == 0 ? 1000 : existing.Max(student => student.AddressId) + 1;
            dataGrid.TransferRows(GetSelectedStudents(), _transferGrid, copy: true, clone: student => CloneStudent(student, id++, addressId++));
            await _transferGrid.RefreshDataAsync();
        });
        AddMenuCommand(tools.DropDownItems, "Custom Group: Name Initial", () => { dataGrid.GroupBy(nameof(Student.Name), value => Convert.ToString(value)?.FirstOrDefault().ToString() ?? ""); return dataGrid.RefreshDataAsync(); });
        AddToggle(view.DropDownItems, "Row Styling", true, value => { _rowStyling = value; dataGrid.Invalidate(); });
        dataGrid.RowStyleNeeded += (_, e) =>
        {
            if (!_rowStyling) return;
            e.Bold = e.Item.Gpa >= 3.8m;
            if (!e.Item.IsActive) e.ForeColorHex = GridPalette.FromTheme(EnterpriseUI.Theming.ThemeManager.Instance.CurrentTheme).Muted;
        };
        dataGrid.DataLoadFailed += (_, e) => _status.Text = $"Load failed: {e.Exception.Message}";
        dataGrid.Announcement += (_, e) => UpdateWorkspaceStatus();
    }

    private bool _rowStyling = true;

    private async Task ReplaceDatasetAsync(List<Student> rows)
    {
        _students = new System.Collections.ObjectModel.ObservableCollection<Student>(rows);
        dataGrid.DataSource = _students;
        ConfigureHierarchy(_hierarchyMode);
        ConfigureSource();
        await dataGrid.RefreshDataAsync(_operationCancellation?.Token ?? default);
    }

    private static Student CloneStudent(Student student, int id, int addressId) => new()
    {
        Id = id,
        AddressId = addressId,
        Name = student.Name,
        Email = student.Email,
        Department = student.Department,
        EnrolledOn = student.EnrolledOn,
        IsActive = student.IsActive,
        Credits = student.Credits,
        Gpa = student.Gpa,
        ProfileUrl = $"https://example.com/students/{id}",
        Address = new Address
        {
            Id = addressId,
            City = student.Address.City,
            Country = student.Address.Country,
            Contacts = student.Address.Contacts.Select(contact => new AddressContact { AddressId = addressId, Kind = contact.Kind, Value = contact.Value }).ToArray()
        }
    };

    private void ConfigureSource()
    {
        _remoteRequests = 0;
        _failNextRequest = false;
        dataGrid.CustomDataSource = _simulatedRemote ? new RemoteDataSource<Student>((request, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            _remoteRequests++;
            if (_failNextRequest) { _failNextRequest = false; return Task.FromException<DataSourceResult<Student>>(new IOException("Simulated server failure.")); }
            return Task.FromResult(QueryEngine.Execute(_students.ToArray().AsQueryable(), request));
        }) : new DataSource<Student>(_students);
    }

    private void ConfigureHierarchy(StudentHierarchyMode mode)
    {
        _hierarchyMode = mode;
        dataGrid.Relations.Clear();
        dataGrid.EnableMasterDetail = true;
        if (mode == StudentHierarchyMode.Tree)
        {
            _simulatedRemote = false;
            dataGrid.CustomDataSource = new DataSource<Student>(_students);
            dataGrid.PagingMode = GridPagingMode.Client;
            dataGrid.ClearGroups();
            dataGrid.AddSelfReference(nameof(Student.Id), nameof(Student.ParentStudentId));
            dataGrid.TreeExpandedByDefault = false;
        }
        else if (mode != StudentHierarchyMode.PropertySummary)
        {
            var relation = dataGrid.AddRelation("Address", nameof(Student.AddressId), nameof(Address.Id), _students.Select(student => student.Address).ToList(), typeof(Address));
            relation.ChildColumns.Add(new GridColumn { Field = nameof(Address.Id), Title = "Address ID", Width = 100 });
            relation.ChildColumns.Add(new GridColumn { Field = nameof(Address.City), Title = "City", Width = 180 });
            relation.ChildColumns.Add(new GridColumn { Field = nameof(Address.Country), Title = "Country", Width = 180 });
            if (mode == StudentHierarchyMode.LazyDetails)
            {
                relation.ChildSource = null;
                relation.LoadChildrenAsync = async (parent, cancellationToken) =>
                {
                    await Task.Delay(100, cancellationToken);
                    return new[] { ((Student)parent).Address };
                };
            }
            var contacts = new GridHierarchyRelation { RelationName = "Contacts", KeyField = nameof(Address.Id), ForeignField = nameof(AddressContact.AddressId), ChildItemType = typeof(AddressContact), DetailItemsSelector = parent => ((Address)parent).Contacts };
            contacts.ChildColumns.Add(new GridColumn { Field = nameof(AddressContact.Kind), Title = "Kind", Width = 100 });
            contacts.ChildColumns.Add(new GridColumn { Field = nameof(AddressContact.Value), Title = "Value", Width = 250 });
            relation.ChildRelations.Add(contacts);
        }
        dataGrid.NotifyRelationsChanged();
        if (dataGrid.FindColumn(nameof(Student.Name)) is { } name)
            name.CustomComparer = Comparer<object>.Create((left, right) =>
            {
                var leftText = Convert.ToString(left) ?? string.Empty;
                var rightText = Convert.ToString(right) ?? string.Empty;
                return int.TryParse(leftText.Split(' ').Last(), out var leftId) && int.TryParse(rightText.Split(' ').Last(), out var rightId)
                    ? leftId.CompareTo(rightId) : StringComparer.CurrentCultureIgnoreCase.Compare(leftText, rightText);
            });
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_verificationMode && dataGrid.HasPendingChanges && MessageBox.Show(this, "Discard pending changes and close?", "Pending Changes", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            e.Cancel = true;
        base.OnFormClosing(e);
    }
}