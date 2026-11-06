using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Theming;
using TestDataGrid.Models;
using TestDataGrid.Services;
using TestDataGrid.Views;

namespace TestDataGrid;

public sealed class MainForm : Form
{
    public SchoolStore Store { get; }
    private readonly Panel _workspace = new() { Dock = DockStyle.Fill };
    private readonly FlowLayoutPanel _navigation = new() { Dock = DockStyle.Left, Width = 215, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(12) };
    private readonly Label _identity = new() { Dock = DockStyle.Top, Height = 62, Padding = new Padding(24, 0, 12, 0), TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 17f, FontStyle.Bold) };
    private readonly ToolStripStatusLabel _status = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Dictionary<string, Func<Control>> _pages = new();
    private readonly Dictionary<string, Button> _buttons = new();
    private readonly HashSet<string> _visited = new();
    private string _currentPage = string.Empty;

    public MainForm(SchoolStore? store = null)
    {
        Store = store ?? new SchoolStore();
        if (ThemeManager.Instance.AvailableThemes.Any(theme => theme.Name == Store.Data.Settings.Theme)) ThemeManager.Instance.ApplyTheme(Store.Data.Settings.Theme);
        Text = Store.Data.Settings.SchoolName;
        Icon = SystemIcons.Application;
        ClientSize = new Size(1400, 850); MinimumSize = new Size(1050, 650);
        StartPosition = FormStartPosition.CenterScreen; Font = SchoolUi.BodyFont;
        AutoScaleMode = AutoScaleMode.Dpi;
        _identity.Text = Store.Data.Settings.SchoolName + "   /   " + Store.Data.Settings.AcademicYear;
        AddPage("Overview", () => new DashboardView(Store));
        AddPage("Students", () => new StudentsView(Store));
        AddPage("Staff", () => new RecordWorkspace<StaffRecord>(Store, "Staff"));
        AddPage("Courses", () => new RecordWorkspace<CourseRecord>(Store, "Courses"));
        AddPage("Enrollments", () => new RecordWorkspace<EnrollmentRecord>(Store, "Enrollments"));
        AddPage("Attendance", () => new AttendanceView(Store));
        AddPage("Tasks", () => new TasksView(Store));
        AddPage("Timetable", () => new ScheduleView(Store));
        AddPage("Finance", () => new FinanceView(Store));
        AddPage("Reports", () => new ReportsView(Store));
        AddPage("Settings", () => new SettingsView(Store));
        AddPage("Control Explorer", () => new ControlWorkshopView());
        var workshop = new Button { Text = "Grid Workshop", Width = 185, Height = 38, FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 20, 0, 0) };
        workshop.Click += (_, _) => { using var form = new Form1(); form.ShowDialog(this); };
        _navigation.Controls.Add(workshop);
        var status = new StatusStrip(); status.Items.Add(_status);
        Controls.Add(_workspace); Controls.Add(_navigation); Controls.Add(_identity); Controls.Add(status);
        ThemeManager.Instance.ThemeChanged += ThemeChanged;
        Store.Changed += StoreChanged;
        FormClosed += (_, _) => { ThemeManager.Instance.ThemeChanged -= ThemeChanged; Store.Changed -= StoreChanged; };
        ApplyTheme();
        Navigate("Overview");
    }

    private void AddPage(string name, Func<Control> factory)
    {
        _pages[name] = factory;
        var button = new Button { Text = name, Width = 185, Height = 40, FlatStyle = FlatStyle.Flat, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(12, 0, 0, 0), Margin = new Padding(0, 3, 0, 3) };
        button.FlatAppearance.BorderSize = 0;
        button.Click += (_, _) =>
        {
            try { Navigate(name); }
            catch (Exception exception) { MessageBox.Show(this, exception.Message, "Unable to Open " + name, MessageBoxButtons.OK, MessageBoxIcon.Error); }
        };
        _navigation.Controls.Add(button); _buttons[name] = button;
    }
    public IReadOnlyCollection<string> PageNames => _pages.Keys;
    public Control CurrentView => _workspace.Controls[0];

    public void Navigate(string page)
    {
        try
        {
            var view = _pages[page]();
            view.Dock = DockStyle.Fill;
            foreach (Control old in _workspace.Controls.Cast<Control>().ToArray()) old.Dispose();
            _workspace.Controls.Add(view);
            _currentPage = page;
            _visited.Add(page);
            ApplyTheme();
            _status.Text = $"{Store.Data.Settings.SchoolName}   ·   {Store.Data.Students.Count:N0} students   ·   {Store.Data.Settings.AcademicYear}   ·   Local data saved";
        }
            catch (Exception exception) { throw new InvalidOperationException("Unable to open " + page, exception); }
    }
    private void ThemeChanged(object? sender, ITheme theme) => ApplyTheme();
    private void StoreChanged(object? sender, EventArgs e)
    {
        _identity.Text = Store.Data.Settings.SchoolName + "   /   " + Store.Data.Settings.AcademicYear;
        Text = Store.Data.Settings.SchoolName;
        _status.Text = "Changes saved   ·   " + DateTime.Now.ToString("HH:mm:ss");
    }
    private void ApplyTheme()
    {
        SchoolUi.Apply(this); _identity.BackColor = SchoolUi.Header;
        var palette = EnterpriseUI.Grids.GridPalette.FromTheme(ThemeManager.Instance.CurrentTheme);
        foreach (var (name, button) in _buttons) { button.UseVisualStyleBackColor = false; button.BackColor = name == _currentPage ? SchoolUi.Accent : SchoolUi.Surface; button.ForeColor = name == _currentPage ? ColorTranslator.FromHtml(palette.PrimaryText) : SchoolUi.Text; }
    }
}