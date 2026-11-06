using EnterpriseUI.Grids;
using EnterpriseUI.Theming;
using EnterpriseUI.WinForms;
using EnterpriseUI.Data;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

namespace TestDataGrid
{
    public partial class Form1 : Form
    {
        public Form1() : this(false)
        {
        }

        internal Form1(bool verificationMode)
        {
            _verificationMode = verificationMode;
            InitializeComponent();
            ThemeManager.Instance.ApplyTheme("Office-Dark");
            dataGrid.CellClick += DataGridCellClick;
            dataGrid.RowContextMenuRequested += ShowStudentContextMenu;
            dataGrid.EditRequested += EditStudent;

            var palette = GridPalette.FromTheme(ThemeManager.Instance.CurrentTheme);
            var title = new Label
            {
                Text = "Students",
                Dock = DockStyle.Top,
                Height = 52,
                Padding = new Padding(16, 0, 16, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font(dataGrid.Font.FontFamily, 14f, FontStyle.Bold),
                BackColor = ColorTranslator.FromHtml(palette.Header),
                ForeColor = ColorTranslator.FromHtml(palette.Text)
            };
            _titleLabel = title;
            Controls.Add(title);
            title.SendToBack();
            Text = "Students";
            dataGrid.Text = "Students";
            dataGrid.AriaLabel = "Students";

            components ??= new System.ComponentModel.Container();
            _studentMenu = new ContextMenuStrip(components);
            _studentMenu.Items.Add("Edit...", null, (_, _) =>
            {
                if (_contextStudent != null) EditStudent(_contextStudent);
            });
            InitializeFeatureWorkspace();
        }

        private readonly ContextMenuStrip _studentMenu;
        private readonly bool _verificationMode;
        private Student? _contextStudent;

        private void ShowStudentContextMenu(Student student, Point location)
        {
            _contextStudent = student;
            _studentMenu.Show(dataGrid, location);
        }

        private async void EditStudent(Student student)
        {
            if (_editingDialogOpen) return;
            _editingDialogOpen = true;
            try
            {
                var session = dataGrid.BeginRowEdit(student);
                using var dialog = new StudentEditDialog(session, dataGrid.Columns);
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    session.Cancel();
                    return;
                }
                await dataGrid.RefreshDataAsync();
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "Unable to refresh students", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { _editingDialogOpen = false; UpdateWorkspaceStatus(); }
        }

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (_verificationMode) return;
            try
            {
                await PopulateGridAsync(GetData());
                _baselineLayout = dataGrid.CreateLayoutProfile("Students");
                UpdateWorkspaceStatus();
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "Unable to load students", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task PopulateGridAsync(List<Student> data)
        {
            dataGrid.EnableVirtualization = true;
            dataGrid.EnableMasterDetail = true;
            dataGrid.EnableSorting = true;
            dataGrid.EnableMultiColumnSorting = true;
            dataGrid.EnableFiltering = true;
            dataGrid.EnableQuickFilterRow = true;
            dataGrid.EnableGrouping = true;
            dataGrid.EnableAlternateRows = true;
            dataGrid.EnableSearchPanel = true;
            dataGrid.SearchMode = GridSearchMode.Highlight;
            dataGrid.SearchOptions = new GridSearchOptions { IncludeHierarchy = true, ExpandToReveal = true };
            dataGrid.ShowFilterRow = true;
            dataGrid.ShowGroupPanel = true;
            dataGrid.ShowSearchPanel = true;
            dataGrid.ShowGroupFooters = true;
            dataGrid.ShowColumnSummaries = true;
            dataGrid.ShowSummaryFooter = true;
            dataGrid.ShowPager = true;
            dataGrid.AllowRowDrag = true;
            dataGrid.AllowRowDrop = true;
            dataGrid.SelectionMode = GridSelectionMode.MultipleRows;
            dataGrid.ClipboardCopyMode = GridClipboardCopyMode.CellsWithHeaders;
            dataGrid.PageSizeOptions = new[] { 10, 25, 50, 100 };
            dataGrid.EditMode = GridEditMode.Form;
            dataGrid.AllowRowDelete = false;
            dataGrid.Columns.Clear();
            dataGrid.Bands.Clear();
            dataGrid.Relations.Clear();
            dataGrid.Columns.Add(new GridColumn { Field = nameof(Student.Id), Title = "Student ID", Width = 145, ReadOnly = true });
            dataGrid.Columns.Add(new GridColumn { Field = nameof(Student.Name), Title = "Name", Width = 200 });
            dataGrid.Columns.Add(new GridColumn { Field = nameof(Student.Email), Title = "Email", Width = 300 });
            dataGrid.Columns.Add(new GridColumn { Field = "Address.City", Title = "City", Width = 150 });
            dataGrid.Columns.Add(new GridColumn { Field = "Address.Country", Title = "Country", Width = 150 });
            dataGrid.Columns.Add(new GridColumn { Field = nameof(Student.Department), Title = "Department", Width = 150, ColumnType = GridColumnType.ComboBox, ComboItems = new object[] { "Engineering", "Business", "Science", "Arts" } });
            dataGrid.Columns.Add(new GridColumn { Field = nameof(Student.EnrolledOn), Title = "Enrolled", Width = 130, ColumnType = GridColumnType.Date, FormatString = "d" });
            dataGrid.Columns.Add(new GridColumn { Field = nameof(Student.IsActive), Title = "Active", Width = 80, ColumnType = GridColumnType.Boolean });
            var credits = new GridColumn { Field = nameof(Student.Credits), Title = "Credits", Width = 145, ColumnType = GridColumnType.Numeric, Alignment = ColumnTextAlignment.Right };
            credits.ValidationRules.Add(new RangeRule(0, 240));
            credits.FooterSummaries.Add(new GridColumnSummary(AggregateFunction.Sum, "Total: {0:N0}"));
            dataGrid.Columns.Add(credits);
            var gpa = new GridColumn { Field = nameof(Student.Gpa), Title = "GPA", Width = 100, ColumnType = GridColumnType.Numeric, FormatString = "N2", Alignment = ColumnTextAlignment.Right };
            gpa.ValidationRules.Add(new RangeRule(0m, 4m));
            gpa.FooterSummaries.Add(new GridColumnSummary(AggregateFunction.Average, "Avg: {0:N2}"));
            dataGrid.Columns.Add(gpa);
            dataGrid.Columns.Add(new GridColumn { Field = nameof(Student.Completion), Title = "Completion", Width = 140, ColumnType = GridColumnType.Progress, FormatString = "0'%'", ReadOnly = true });
            dataGrid.Columns.Add(new GridColumn { Field = "RemainingCredits", Title = "Remaining", Width = 110, Expression = "240 - [Credits]", Alignment = ColumnTextAlignment.Right });
            dataGrid.Columns.Add(new GridColumn { Field = "RunningCredits", Title = "Running credits", Width = 140, RunningTotalOf = nameof(Student.Credits), Alignment = ColumnTextAlignment.Right });
            dataGrid.Columns.Add(new GridColumn { Field = nameof(Student.ProfileUrl), Title = "Profile", Width = 250, ColumnType = GridColumnType.Hyperlink, ReadOnly = true });
            dataGrid.Columns[0].PinPosition = ColumnPinPosition.Left;
            dataGrid.Columns[0].FooterSummaries.Add(new GridColumnSummary(AggregateFunction.Count, "Students: {0:N0}"));
            dataGrid.FindColumn(nameof(Student.Name))!.ValidationRules.Add(new RequiredRule());
            dataGrid.FindColumn(nameof(Student.Name))!.ValidationRules.Add(new LengthRule(2, 100));
            dataGrid.FindColumn(nameof(Student.Email))!.ValidationRules.Add(new RequiredRule());
            dataGrid.FindColumn(nameof(Student.Email))!.ValidationRules.Add(new RegexRule(@"^[^@\s]+@[^@\s]+$"));
            dataGrid.FindColumn(nameof(Student.Email))!.ValidationRules.Add(new DelegateRule(context => System.Net.Mail.MailAddress.TryCreate(Convert.ToString(context.NewValue), out _) ? null : "Enter a valid email address."));
            dataGrid.RowValidators.Clear();
            dataGrid.RowValidators.Add(student => student.EnrolledOn.Date > DateTime.Today && student.Credits > 0
                ? new[] { new GridValidationError(student, nameof(Student.EnrolledOn), "Future enrollment cannot have earned credits.") }
                : Array.Empty<GridValidationError>());
            dataGrid.AsyncRowValidators.Clear();
            dataGrid.AsyncRowValidators.Add((student, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult<IEnumerable<GridValidationError>>(student.Email.EndsWith(".invalid", StringComparison.OrdinalIgnoreCase)
                    ? new[] { new GridValidationError(student, nameof(Student.Email), "Email domain is reserved.") }
                    : Array.Empty<GridValidationError>());
            });
            dataGrid.GroupSummaries.Clear();
            dataGrid.GroupSummaries.Add(new GroupSummaryDescriptor(nameof(Student.Credits), AggregateFunction.Sum, "Credits: {0:N0}"));
            dataGrid.GroupSummaries.Add(new GroupSummaryDescriptor(nameof(Student.Gpa), AggregateFunction.Average, "GPA: {0:N2}"));
            dataGrid.FormatRules.Clear();
            dataGrid.FormatRules.Add(new GridFormatRule { Name = "Low GPA", ColumnField = nameof(Student.Gpa), Operator = FormatConditionOperator.LessThan, Value1 = 2m, BackgroundColorHex = "#6E2525", ForegroundColorHex = "#FFFFFF" });
            dataGrid.FormatRules.Add(new GridFormatRule { Name = "Credits data bar", ColumnField = nameof(Student.Credits), RuleType = FormatRuleType.DataBar, Value1 = 0, Value2 = 240, BackgroundColorHex = "#219F8F" });
            dataGrid.FormatRules.Add(new GridFormatRule { Name = "GPA scale", ColumnField = nameof(Student.Gpa), RuleType = FormatRuleType.ColorScale, Value1 = 2m, Value2 = 4m, BackgroundColorHex = "#E8F4EA", ForegroundColorHex = "#172A1B" });
            dataGrid.Columns.Add(new GridCommandColumn
            {
                Field = "DeleteStudent",
                Title = "Actions",
                Expression = "'Delete'",
                Width = 110,
                MinWidth = 110,
                MaxWidth = 110,
                PinPosition = ColumnPinPosition.Right,
                Reorderable = false,
                Resizable = false,
                CanHide = false,
                AllowColumnChooser = false,
                Searchable = false,
                Tooltip = "Delete student"
            });

            _students = new ObservableCollection<Student>(data);
            dataGrid.DataSource = _students;
            ConfigureHierarchy(_hierarchyMode);
            ConfigureSource();
            SyncSearchModeControl();
            await dataGrid.RefreshDataAsync();
        }

        private ObservableCollection<Student> _students = new();

        private async void DataGridCellClick(object? sender, GridCellClickEventArgs<Student> e)
        {
            if (e.Column.Field == nameof(Student.ProfileUrl))
            {
                if (Uri.TryCreate(e.Item.ProfileUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
                return;
            }
            if (e.Column.Field != "DeleteStudent") return;
            await RunGridOperationAsync(() => DeleteStudentsAsync(new[] { e.Item }));
        }

        public List<Student> GetData(int count = 100, CancellationToken cancellationToken = default)
        {
            var departments = new[] { "Engineering", "Business", "Science", "Arts" };
            var cities = new[] { "Helsinki", "London", "Berlin", "Paris" };
            var countries = new[] { "Finland", "UK", "Germany", "France" };
            List<Student> data = new List<Student>(count);
            for (int index = 0; index < count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var student = new Student
                {
                    Id = index,
                    AddressId = 1000 + index,
                    Name = $"Name {index}",
                    Email = $"student{index}@example.com",
                    Department = departments[index % 4],
                    EnrolledOn = new DateTime(2024, 1, 1).AddDays(index % 730),
                    IsActive = index % 5 != 0,
                    Credits = index % 241,
                    Gpa = Math.Round((index % 41) / 10m, 2),
                    ParentStudentId = index % 10 == 0 ? null : index - index % 10,
                    ProfileUrl = $"https://example.com/students/{index}"
                };
                student.Address = new Address
                {
                    Id = student.AddressId,
                    City = cities[index % 4],
                    Country = countries[index % 4],
                    Contacts = new[]
                    {
                        new AddressContact { AddressId = student.AddressId, Kind = "Phone", Value = $"+358 555 {index:D6}" },
                        new AddressContact { AddressId = student.AddressId, Kind = "Office", Value = $"Room {index % 50 + 1}" }
                    }
                };
                data.Add(student);
            }
            return data;
        }
    }

    public class Student : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _email = string.Empty;
        private string _department = "Engineering";
        private DateTime _enrolledOn = DateTime.Today;
        private bool _isActive;
        private int _credits;
        private decimal _gpa;

        public int Id { get; set; }
        public string Name { get => _name; set => SetField(ref _name, value); }
        public string Email { get => _email; set => SetField(ref _email, value); }
        public string Department { get => _department; set => SetField(ref _department, value); }
        public DateTime EnrolledOn { get => _enrolledOn; set => SetField(ref _enrolledOn, value); }
        public bool IsActive { get => _isActive; set => SetField(ref _isActive, value); }
        public int Credits { get => _credits; set { SetField(ref _credits, value); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Completion))); } }
        public decimal Gpa { get => _gpa; set => SetField(ref _gpa, value); }
        public int Completion => (int)Math.Round(Credits / 240d * 100);
        public string ProfileUrl { get; set; } = string.Empty;
        public int? ParentStudentId { get; set; }
        public int AddressId { get; set; }
        public Address Address { get; set; } = new Address();

        public event PropertyChangedEventHandler? PropertyChanged;

        private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class Address
    {
        public int Id { get; set; }
        public string City { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public IReadOnlyList<AddressContact> Contacts { get; set; } = Array.Empty<AddressContact>();
    }

    public class AddressContact
    {
        public int AddressId { get; set; }
        public string Kind { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}
