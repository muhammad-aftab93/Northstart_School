using System.Runtime.InteropServices;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Editors.Interactivity;
using EnterpriseUI.Theming;
using EnterpriseUI.WinForms;
using TestDataGrid.Models;
using TestDataGrid.Services;
using TestDataGrid.Views;

namespace TestDataGrid;

public sealed class MainForm : Form
{
    private static readonly Dictionary<string, string> Glyphs = new()
    {
        ["Overview"] = "\u2302", ["Analytics"] = "\u25D4", ["Students"] = "\u263A", ["Staff"] = "\u265F", ["Courses"] = "\u2630", ["Enrollments"] = "\u271A",
        ["Attendance"] = "\u2714", ["Tasks"] = "\u2611", ["Timetable"] = "\u25F7", ["Finance"] = "\u00A4", ["Reports"] = "\u25A4", ["Assistant"] = "\u2726",
        ["Audit Log"] = "\u2261", ["Settings"] = "\u2699", ["Control Explorer"] = "\u25EB"
    };

    private static readonly Dictionary<string, string> Tips = new()
    {
        ["Analytics"] = "Drag the **grade range** slider to filter the pivot, and the **attendance window** to change how many days the heat map covers.",
        ["Students"] = "Double-click a row to edit it. **Admit Student\u2026** enrols a new student in courses and creates the tuition invoice in one go.",
        ["Courses"] = "Turn on **Showcase** to browse courses as a gallery. Press Enter on a course to jump to it in the list.",
        ["Attendance"] = "**Roll Call\u2026** takes a register for one course. The Voice button next to the search box searches by speech.",
        ["Tasks"] = "Switch on **Board / List** to drag tasks between the To do, In progress and Done lanes.",
        ["Finance"] = "Pick an invoice and use **Record Payment**. Overdue invoices also appear as notifications in the tray.",
        ["Assistant"] = "Ask a question in the chat, or write a fee reminder with the prompt on the right. Use a student name or code such as ST-0007.",
        ["Settings"] = "Untick **Show page tips** below if you no longer want these hints."
    };

    public SchoolStore Store { get; }
    private readonly bool _interactive;
    private readonly Panel _workspace = new() { Dock = DockStyle.Fill };
    private readonly FlowLayoutPanel _navigation = new() { Dock = DockStyle.Left, Width = 215, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(12) };
    private readonly Label _identity = new() { Dock = DockStyle.Top, Height = 62, Padding = new Padding(24, 0, 12, 0), TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 17f, FontStyle.Bold) };
    private readonly ToolStripStatusLabel _status = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly EnterpriseWaitingBarControl _busy = new() { Animation = WaitingBarAnimation.Marquee, IsWaiting = false, Width = 180, Height = 20, AccessibleName = "Working" };
    private readonly ToolStripControlHost _busyHost;
    private readonly Dictionary<string, Func<Control>> _pages = new();
    private readonly Dictionary<string, EnterpriseToggleButtonControl> _buttons = new();
    private readonly HashSet<string> _visited = new();
    private readonly HashSet<string> _tipsShown = new();
    private readonly EnterpriseNotifyIcon _tray;
    private readonly EnterpriseVirtualKeyboardControl _keyboard = new() { Dock = DockStyle.Fill, KeyClickSound = false, HideOnClose = false, AccessibleName = "Touch keyboard" };
    private readonly Panel _keyboardPanel = new() { Dock = DockStyle.Bottom, Height = 270, Visible = false, Padding = new Padding(8) };
    private readonly System.Windows.Forms.Timer _focusTimer = new() { Interval = 300 };
    private readonly EnterpriseToggleButtonControl _touchToggle = new() { Text = "Touch keyboard", Glyph = "\u2328", Width = 185, Height = 38, Margin = new Padding(0, 6, 0, 0), AccessibleName = "Touch keyboard" };
    private EnterpriseCalloutControl? _tip;
    private string _currentPage = string.Empty;
    private int _busyCount;
    private int _auditSeen;

    [DllImport("user32.dll")]
    private static extern IntPtr GetFocus();

    public MainForm(SchoolStore? store = null, bool interactive = true)
    {
        Store = store ?? new SchoolStore();
        _interactive = interactive;
        if (ThemeManager.Instance.AvailableThemes.Any(theme => theme.Name == Store.Data.Settings.Theme)) ThemeManager.Instance.ApplyTheme(Store.Data.Settings.Theme);
        Text = Store.Data.Settings.SchoolName;
        Icon = SystemIcons.Application;
        ClientSize = new Size(1400, 850); MinimumSize = new Size(1050, 650);
        StartPosition = FormStartPosition.CenterScreen; Font = SchoolUi.BodyFont;
        AutoScaleMode = AutoScaleMode.Dpi;
        _identity.Text = Store.Data.Settings.SchoolName + "   /   " + Store.Data.Settings.AcademicYear;
        AddPage("Overview", () => new DashboardView(Store));
        AddPage("Analytics", () => new AnalyticsView(Store));
        AddPage("Students", () => new StudentsView(Store));
        AddPage("Staff", () => new StaffView(Store));
        AddPage("Courses", () => new CoursesView(Store));
        AddPage("Enrollments", () => new EnrollmentsView(Store));
        AddPage("Attendance", () => new AttendanceView(Store));
        AddPage("Tasks", () => new TasksView(Store));
        AddPage("Timetable", () => new ScheduleView(Store));
        AddPage("Finance", () => new FinanceView(Store));
        AddPage("Reports", () => new ReportsView(Store));
        AddPage("Assistant", () => new AssistantView(Store));
        AddPage("Audit Log", () => new AuditLogView(Store));
        AddPage("Settings", () => new SettingsView(Store));
        AddPage("Control Explorer", () => new ControlWorkshopView());

        var workshop = new EnterpriseButtonControl { Text = "Grid Workshop", Variant = EnterpriseButtonVariant.Outline, Width = 185, Height = 38, Margin = new Padding(0, 20, 0, 0) };
        workshop.Click += (_, _) => { using var form = new Form1(); form.ShowDialog(this); };
        _navigation.Controls.Add(workshop);
        _touchToggle.CheckedChanged += (_, _) => SetTouchKeyboard(_touchToggle.Checked);
        _navigation.Controls.Add(_touchToggle);

        _keyboardPanel.Controls.Add(_keyboard);
        _keyboard.CloseRequested += (_, _) => _touchToggle.Checked = false;
        _focusTimer.Tick += (_, _) => FollowFocus();

        _busyHost = new ToolStripControlHost(_busy) { Visible = false, Margin = new Padding(0, 2, 8, 2) };
        var status = new StatusStrip(); status.Items.Add(_status); status.Items.Add(_busyHost);

        Controls.Add(_workspace); Controls.Add(_keyboardPanel); Controls.Add(_navigation); Controls.Add(_identity); Controls.Add(status);

        _tray = new EnterpriseNotifyIcon { Icon = SystemIcons.Application, Text = Store.Data.Settings.SchoolName, Visible = interactive, ToastMode = interactive ? NotifyToastMode.Custom : NotifyToastMode.None };
        _tray.ContextMenuStrip = BuildTrayMenu();
        _tray.NotificationClicked += (_, e) => OpenNotification(e.Notification);
        _tray.ActionInvoked += (_, e) => OpenNotification(e.Notification);
        _tray.DoubleClick += (_, _) => RestoreWindow();
        _auditSeen = Store.Data.Audit.Count;

        ThemeManager.Instance.ThemeChanged += ThemeChanged;
        Store.Changed += StoreChanged;
        FormClosed += (_, _) =>
        {
            ThemeManager.Instance.ThemeChanged -= ThemeChanged; Store.Changed -= StoreChanged;
            _focusTimer.Dispose(); _tip?.Dispose(); _tray.Dispose();
        };
        Shown += (_, _) => { if (_interactive) RaiseStartupAlerts(); };
        ApplyTheme();
        Navigate("Overview");
    }

    private void AddPage(string name, Func<Control> factory)
    {
        _pages[name] = factory;
        var button = new EnterpriseToggleButtonControl { Text = name, Glyph = Glyphs.GetValueOrDefault(name), GroupName = "navigation", Width = 185, Height = 40, Margin = new Padding(0, 3, 0, 3), AccessibleName = name };
        button.CheckedChanged += (_, _) =>
        {
            if (!button.Checked || _currentPage == name) return;
            try { Navigate(name); }
            catch (Exception exception) { MessageBox.Show(this, exception.Message, "Unable to Open " + name, MessageBoxButtons.OK, MessageBoxIcon.Error); }
        };
        _navigation.Controls.Add(button); _buttons[name] = button;
    }

    public IReadOnlyCollection<string> PageNames => _pages.Keys;
    public Control CurrentView => _workspace.Controls[0];
    public EnterpriseNotifyIcon Tray => _tray;
    public EnterpriseVirtualKeyboardControl Keyboard => _keyboard;
    public bool IsBusy => _busyCount > 0;

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
            if (!_buttons[page].Checked) _buttons[page].Checked = true;
            MarkRead(page);
            ApplyTheme();
            _status.Text = StatusText();
            ShowTip(page);
        }
        catch (Exception exception) { throw new InvalidOperationException("Unable to open " + page, exception); }
    }

    private string StatusText() => $"{Store.Data.Settings.SchoolName}   \u00b7   {Store.Data.Students.Count:N0} students   \u00b7   {Store.Data.Settings.AcademicYear}   \u00b7   Local data saved";

    /// <summary>Shows the busy indicator in the status bar until the scope is disposed; nested scopes share it.</summary>
    public IDisposable BeginBusy(string text)
    {
        _busyCount++;
        _status.Text = text;
        _busyHost.Visible = true;
        _busy.StartWaiting();
        return new BusyScope(this);
    }

    private void EndBusy()
    {
        if (--_busyCount > 0) return;
        _busy.StopWaiting();
        _busyHost.Visible = false;
        _status.Text = StatusText();
    }

    private sealed class BusyScope : IDisposable
    {
        private MainForm? _owner;
        public BusyScope(MainForm owner) => _owner = owner;
        public void Dispose() { _owner?.EndBusy(); _owner = null; }
    }

    /// <summary>Adds a notification to the tray icon (and shows a toast); <paramref name="page"/> is opened when it is clicked.</summary>
    public AppNotification Notify(string title, string message, NotificationSeverity severity, string? page, string? group = null)
    {
        var actions = page == null ? Array.Empty<NotificationAction>() : new[] { new NotificationAction("open", "Open " + page, true) };
        var notification = _tray.Center.Notify(title, message, severity, group, actions);
        notification.Tag = page;
        return notification;
    }

    private void RaiseStartupAlerts()
    {
        foreach (var alert in SchoolAlerts.Build(Store.Data).Take(3)) Notify(alert.Title, alert.Message, alert.Severity, alert.Page, alert.Group);
    }

    private void OpenNotification(AppNotification notification)
    {
        RestoreWindow();
        if (notification.Tag is string page && _pages.ContainsKey(page)) Navigate(page);
    }

    private void MarkRead(string page)
    {
        foreach (var item in _tray.Center.Items.Where(item => !item.IsRead && item.Tag is string tag && tag == page).ToList()) _tray.Center.MarkRead(item.Id);
    }

    private void RestoreWindow()
    {
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
    }

    private ContextMenuStrip BuildTrayMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open " + Store.Data.Settings.SchoolName, null, (_, _) => RestoreWindow());
        menu.Items.Add("Mark all notifications read", null, (_, _) => _tray.MarkAllRead());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Close());
        return menu;
    }

    private void ShowTip(string page)
    {
        if (!_interactive || !IsHandleCreated || !Visible || !Store.Data.Settings.ShowTips || !Tips.TryGetValue(page, out var tip) || !_tipsShown.Add(page)) return;
        BeginInvoke(() =>
        {
            if (IsDisposed || _currentPage != page) return;
            _tip?.Close();
            var callout = new EnterpriseCalloutControl { Title = "Tip", Message = tip, MarkupMode = LabelMarkupMode.Markdown, Severity = NotificationSeverity.Info, Placement = CalloutPlacement.Bottom, MaxWidth = 340, AutoCloseInterval = 9000, CloseOnClickOutside = true };
            callout.Actions.Add(new NotificationAction("off", "Stop showing tips"));
            callout.ActionInvoked += (_, e) => { if (e.ActionId == "off") Store.Transaction(data => data.Settings.ShowTips = false); };
            callout.Closed += (_, _) => callout.Dispose();
            _tip = callout;
            callout.Show(_workspace, new Rectangle(28, 6, 200, 44));
        });
    }

    private void SetTouchKeyboard(bool on)
    {
        _keyboardPanel.Visible = on;
        _focusTimer.Enabled = on;
        if (on) FollowFocus();
    }

    /// <summary>Points the touch keyboard at the text box that last had the focus in this window.</summary>
    private void FollowFocus()
    {
        if (Control.FromHandle(GetFocus()) is not TextBoxBase box || box.FindForm() != this || _keyboardPanel.Contains(box) || box == _keyboard.Target) return;
        _keyboard.Kind = box.Parent is NumericUpDown ? VirtualKeyboardKind.Numeric : VirtualKeyboardKind.Qwerty;
        _keyboard.Target = box;
    }

    private void ThemeChanged(object? sender, ITheme theme) => ApplyTheme();

    private void StoreChanged(object? sender, EventArgs e)
    {
        _identity.Text = Store.Data.Settings.SchoolName + "   /   " + Store.Data.Settings.AcademicYear;
        Text = Store.Data.Settings.SchoolName;
        if (!IsBusy) _status.Text = "Changes saved   \u00b7   " + DateTime.Now.ToString("HH:mm:ss");
        if (Store.Data.Audit.Count < _auditSeen) _auditSeen = Store.Data.Audit.Count;
        foreach (var alert in SchoolAlerts.FromAudit(Store.Data, _auditSeen)) Notify(alert.Title, alert.Message, alert.Severity, alert.Page, alert.Group);
        _auditSeen = Store.Data.Audit.Count;
    }

    private void ApplyTheme()
    {
        SchoolUi.Apply(this);
        _identity.BackColor = SchoolUi.Header;
    }
}
