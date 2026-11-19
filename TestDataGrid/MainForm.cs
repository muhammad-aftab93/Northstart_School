using System.Diagnostics;
using System.Runtime.InteropServices;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Editors.Interactivity;
using EnterpriseUI.Notifications;
using EnterpriseUI.Theming;
using EnterpriseUI.WinForms;
using TestDataGrid.Dialogs;
using TestDataGrid.Models;
using TestDataGrid.Services;
using TestDataGrid.Views;

namespace TestDataGrid;

public sealed class MainForm : EnterpriseToolbarForm
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
    private readonly EnterpriseNavigationPane _nav = new() { Dock = DockStyle.Left, ExpandedWidth = 230, Title = "Menu", AutoCompactBreakpoint = 1000, DisplayMode = EnterpriseNavigationDisplayMode.Auto, AccessibleName = "Navigation" };
    private static readonly Dictionary<string, string> Sections = new()
    {
        ["Overview"] = "Insights", ["Analytics"] = "Insights", ["Students"] = "People", ["Staff"] = "People", ["Courses"] = "Academics", ["Enrollments"] = "Academics",
        ["Attendance"] = "Academics", ["Tasks"] = "Operations", ["Timetable"] = "Operations", ["Finance"] = "Operations", ["Reports"] = "Operations", ["Assistant"] = "Operations", ["Audit Log"] = "Operations"
    };
    private readonly Label _identity = new() { Dock = DockStyle.Top, Height = 62, Padding = new Padding(24, 0, 12, 0), TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 17f, FontStyle.Bold) };
    private readonly EnterpriseStatusStrip _statusBar = new();
    private readonly EnterpriseOverlay _overlay = new() { Indicator = EnterpriseOverlayIndicator.Ring, Opacity = 0.3 };
    private readonly Dictionary<string, Func<Control>> _pages = new();
    private readonly Dictionary<string, EnterpriseNavigationItem> _navItems = new();
    private readonly HashSet<string> _visited = new();
    private readonly HashSet<string> _tipsShown = new();
    private readonly EnterpriseNotifyIcon _tray;
    private readonly EnterpriseToastNotification? _toast;
    private readonly EnterpriseTaskbarButton? _taskbar;
    private readonly EnterpriseToolbarItem _alertsItem;
    private readonly EnterpriseVirtualKeyboardControl _keyboard = new() { Dock = DockStyle.Fill, KeyClickSound = false, HideOnClose = false, AccessibleName = "Touch keyboard" };
    private readonly Panel _keyboardPanel = new() { Dock = DockStyle.Bottom, Height = 270, Visible = false, Padding = new Padding(8) };
    private readonly System.Windows.Forms.Timer _focusTimer = new() { Interval = 300 };
    private readonly EnterpriseNavigationItem _touchItem = new("Touch keyboard", "\u2328") { Key = "__touch", IsCommand = true, ToolTip = "Show the on-screen keyboard" };
    private bool _touchOn;
    private string _lastSection = string.Empty;
    private readonly ToolStripMenuItem _themeMenu = new("Theme");
    private EnterpriseCalloutControl? _tip;
    private EnterpriseFlyout? _flyout;
    private WorkspaceWindow? _workspaceWindow;
    private LessonWidget? _widget;
    private string _currentPage = string.Empty;
    private int _busyCount;
    private int _auditSeen;

    [DllImport("user32.dll")]
    private static extern IntPtr GetFocus();

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    public MainForm(SchoolStore? store = null, bool interactive = true)
    {
        Store = store ?? new SchoolStore();
        _interactive = interactive;
        if (ThemeManager.Instance.AvailableThemes.Any(theme => theme.Name == Store.Data.Settings.Theme)) ThemeManager.Instance.ApplyTheme(Store.Data.Settings.Theme);
        Text = Store.Data.Settings.SchoolName;
        Icon = SystemIcons.Application;
        ClientSize = new Size(1400, 850 + CaptionHeight); MinimumSize = new Size(1050, 650);
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

        _nav.FooterItems.Add(new EnterpriseNavigationItem("Grid Workshop", "\u25A6") { Key = "__workshop", IsCommand = true, ToolTip = "Open the grid workshop" });
        _nav.FooterItems.Add(_touchItem);
        _nav.ItemInvoked += (_, e) => OnNavigationInvoked(e.Item);

        _keyboardPanel.Controls.Add(_keyboard);
        _keyboard.CloseRequested += (_, _) => TouchKeyboardOn = false;
        _focusTimer.Tick += (_, _) => FollowFocus();

        _statusBar.AddPanel("status", string.Empty, spring: true);
        _statusBar.AddPanel("records", string.Empty, 170);
        _statusBar.AddPanel("alerts", "No new alerts", 140, kind: EnterpriseStatusPanelKind.Link).ToolTip = "Open the notification centre";
        _statusBar.AddPanel("theme", string.Empty, 150, kind: EnterpriseStatusPanelKind.Link).ToolTip = "Change the theme";
        _statusBar.PanelClick += (_, e) =>
        {
            if (e.Panel.Name == "alerts") ToggleNotifications();
            else if (e.Panel.Name == "theme") Navigate("Settings");
        };

        Controls.Add(_workspace); Controls.Add(_keyboardPanel); Controls.Add(_nav); Controls.Add(_identity); Controls.Add(_statusBar);

        _tray = new EnterpriseNotifyIcon { Icon = SystemIcons.Application, Text = Store.Data.Settings.SchoolName, Visible = interactive, ToastMode = interactive ? NotifyToastMode.Custom : NotifyToastMode.None };
        _tray.ContextMenuStrip = BuildTrayMenu();
        _tray.NotificationClicked += (_, e) => OpenNotification(e.Notification);
        _tray.ActionInvoked += (_, e) => OpenNotification(e.Notification);
        _tray.DoubleClick += (_, _) => RestoreWindow();
        _tray.Center.Changed += (_, _) => UpdateUnread();
        _auditSeen = Store.Data.Audit.Count;

        _alertsItem = BuildToolbar();
        if (interactive)
        {
            _toast = new EnterpriseToastNotification { Placement = ToastPlacement.BottomRight, MaxVisibleToasts = 2 };
            _toast.ActionInvoked += (_, e) => ToastAction(e.ActionId, e.Notification.Tag);
            _taskbar = new EnterpriseTaskbarButton { Form = this };
            BuildTaskbarButtons();
        }

        _overlay.Host = _workspace;
        ThemeManager.Instance.ThemeChanged += ThemeChanged;
        Store.Changed += StoreChanged;
        FormClosed += (_, _) =>
        {
            ThemeManager.Instance.ThemeChanged -= ThemeChanged; Store.Changed -= StoreChanged;
            _focusTimer.Dispose(); _tip?.Dispose(); _tray.Dispose(); _toast?.Dispose(); _taskbar?.Dispose(); _overlay.Dispose(); _widget?.Dispose();
        };
        Shown += (_, _) => { if (_interactive) RaiseStartupAlerts(); };
        ApplySchoolColors();
        UpdateUnread();
        Navigate("Overview");
    }

    private void AddPage(string name, Func<Control> factory)
    {
        _pages[name] = factory;
        var item = new EnterpriseNavigationItem(name, Glyphs.GetValueOrDefault(name)) { Key = name };
        _navItems[name] = item;
        if (Sections.TryGetValue(name, out var section))
        {
            if (_lastSection != section)
            {
                _nav.Items.Add(EnterpriseNavigationItem.Header(section));
                _lastSection = section;
            }

            _nav.Items.Add(item);
        }
        else
        {
            _nav.FooterItems.Add(item);
        }
    }

    private void OnNavigationInvoked(EnterpriseNavigationItem item)
    {
        switch (item.Key)
        {
            case "__workshop":
                using (var form = new Form1()) form.ShowDialog(this);
                break;
            case "__touch":
                TouchKeyboardOn = !TouchKeyboardOn;
                break;
            case { } name when _pages.ContainsKey(name) && _currentPage != name:
                try { Navigate(name); }
                catch (Exception exception) { SchoolUi.Report(this, exception.Message, "Unable to Open " + name); }
                break;
        }
    }

    /// <summary>Shows or hides the on-screen keyboard; the navigation shows a badge while it is on.</summary>
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool TouchKeyboardOn
    {
        get => _touchOn;
        set
        {
            if (_touchOn == value) return;
            _touchOn = value;
            _touchItem.Badge = value ? "On" : null;
            SetTouchKeyboard(value);
        }
    }

    public EnterpriseNavigationPane Navigation => _nav;

    public IReadOnlyCollection<string> PageNames => _pages.Keys;
    public Control CurrentView => _workspace.Controls[0];
    public EnterpriseNotifyIcon Tray => _tray;
    public EnterpriseVirtualKeyboardControl Keyboard => _keyboard;
    public EnterpriseStatusStrip StatusBar => _statusBar;
    public EnterpriseTaskbarButton? Taskbar => _taskbar;
    public EnterpriseFlyout? NotificationFlyout => _flyout;
    public bool IsBusy => _busyCount > 0;

    /// <summary>A new, independent instance of a screen, for the multi-window workspace.</summary>
    public Control CreatePage(string page) => _pages[page]();

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
            _nav.SelectedItem = _navItems[page];
            MarkRead(page);
            ApplySchoolColors();
            SetStatus(StatusText());
            ShowTip(page);
        }
        catch (Exception exception) { throw new InvalidOperationException("Unable to open " + page, exception); }
    }

    private string StatusText() => $"{Store.Data.Settings.SchoolName}   \u00b7   {Store.Data.Settings.AcademicYear}   \u00b7   Local data saved";

    private void SetStatus(string text)
    {
        _statusBar.SetText("status", text);
        _statusBar.SetText("records", $"{Store.Data.Students.Count:N0} students");
        _statusBar.SetText("theme", ThemeManager.Instance.CurrentTheme.Name);
    }

    /// <summary>Dims and blocks the screen and shows the busy state on the taskbar until the scope is disposed; nested scopes share it.</summary>
    public IDisposable BeginBusy(string text)
    {
        _busyCount++;
        SetStatus(text);
        if (_interactive && IsHandleCreated && Visible)
        {
            _overlay.Caption = text;
            _overlay.Show(_workspace);
            _taskbar?.SetProgress(0, EnterpriseTaskbarProgressState.Indeterminate);
        }

        return new BusyScope(this);
    }

    private void EndBusy()
    {
        if (--_busyCount > 0) return;
        if (_overlay.IsVisible) _overlay.Hide();
        if (_taskbar != null) _taskbar.ProgressState = EnterpriseTaskbarProgressState.None;
        SetStatus(StatusText());
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

    /// <summary>Brief confirmation that does not stay in the notification history; <paramref name="path"/> adds Open and Show in folder actions.</summary>
    public void Toast(string title, string message, NotificationSeverity severity = NotificationSeverity.Success, string? path = null)
    {
        if (_toast == null) { SetStatus(title + " \u00b7 " + message); return; }
        var actions = path == null ? Array.Empty<NotificationAction>() : new[] { new NotificationAction("file", "Open", true), new NotificationAction("folder", "Show in folder") };
        _toast.Show(title, message, severity, TimeSpan.FromSeconds(7), actions).Tag = path;
    }

    private static void ToastAction(string? actionId, object? tag)
    {
        if (tag is not string path) return;
        try
        {
            if (actionId == "folder") Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"") { UseShellExecute = true });
            else if (actionId == "file") Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException) { }
    }

    private EnterpriseToolbarItem BuildToolbar()
    {
        var file = new ContextMenuStrip();
        file.Items.Add("Admit student\u2026", null, (_, _) => AdmitStudent());
        file.Items.Add("Roll call\u2026", null, (_, _) => RollCall());
        file.Items.Add("New workspace window", null, (_, _) => OpenWorkspaceWindow(_currentPage));
        file.Items.Add(new ToolStripSeparator());
        file.Items.Add("Settings", null, (_, _) => Navigate("Settings"));
        file.Items.Add("Exit", null, (_, _) => Close());
        AddMenu("file", "File", file);

        var view = new ContextMenuStrip();
        var keyboard = new ToolStripMenuItem("Touch keyboard") { CheckOnClick = true };
        keyboard.Click += (_, _) => TouchKeyboardOn = keyboard.Checked;
        var widget = new ToolStripMenuItem("Lesson widget") { CheckOnClick = true };
        widget.Click += (_, _) => ToggleLessonWidget(widget.Checked);
        view.Items.Add(keyboard);
        view.Items.Add(widget);
        view.Items.Add(_themeMenu);
        view.Opening += (_, _) => { keyboard.Checked = TouchKeyboardOn; widget.Checked = _widget is { IsDisposed: false }; RebuildThemeMenu(); };
        AddMenu("view", "View", view);

        var help = new ContextMenuStrip();
        help.Items.Add("What's new", null, (_, _) => ShowWhatsNew());
        help.Items.Add("About", null, (_, _) => ShowAbout());
        AddMenu("help", "Help", help);

        Toolbar.AddSeparator();
        AddToolbarItem("admit", "Admit", "\u2795", "Admit a student");
        AddToolbarItem("rollcall", "Roll call", "\u2714", "Take today's register");
        var alerts = AddToolbarItem("alerts", string.Empty, "\U0001F514", "Notification centre");
        ShowSearch = true;
        Toolbar.SearchPlaceholder = "Ask the assistant";
        Toolbar.SearchWidth = 230;
        ToolbarItemClick += (_, e) =>
        {
            switch (e.Item.Id)
            {
                case "admit": AdmitStudent(); break;
                case "rollcall": RollCall(); break;
                case "alerts": ToggleNotifications(); break;
            }
        };
        SearchSubmitted += (_, text) => AskAssistant(text);
        return alerts;
    }

    private void RebuildThemeMenu()
    {
        _themeMenu.DropDownItems.Clear();
        foreach (var theme in ThemeManager.Instance.AvailableThemes)
        {
            var name = theme.Name;
            var item = new ToolStripMenuItem(name) { Checked = name == ThemeManager.Instance.CurrentTheme.Name };
            item.Click += (_, _) => { ThemeManager.Instance.ApplyTheme(name); Store.Transaction(data => data.Settings.Theme = name); };
            _themeMenu.DropDownItems.Add(item);
        }
    }

    private void BuildTaskbarButtons()
    {
        if (_taskbar == null) return;
        _taskbar.ThumbnailButtons.Add(new EnterpriseTaskbarThumbnailButton("overview", SystemIcons.Application, "Overview"));
        _taskbar.ThumbnailButtons.Add(new EnterpriseTaskbarThumbnailButton("rollcall", SystemIcons.Information, "Roll call"));
        _taskbar.ThumbnailButtons.Add(new EnterpriseTaskbarThumbnailButton("assistant", SystemIcons.Question, "Assistant"));
        _taskbar.ThumbnailButtonClick += (_, e) =>
        {
            RestoreWindow();
            switch (e.Button.Id)
            {
                case "overview": Navigate("Overview"); break;
                case "rollcall": RollCall(); break;
                case "assistant": Navigate("Assistant"); break;
            }
        };
    }

    public void AdmitStudent()
    {
        using var wizard = new AdmissionWizard(Store);
        wizard.ShowDialog(this);
    }

    public void RollCall()
    {
        using var dialog = new RollCallDialog(Store);
        dialog.ShowDialog(this);
    }

    public void AskAssistant(string question)
    {
        if (string.IsNullOrWhiteSpace(question)) return;
        Navigate("Assistant");
        if (CurrentView is AssistantView assistant) assistant.Chat.SendText(question.Trim());
    }

    /// <summary>Opens a screen in the tabbed workspace window; tabs can be dragged out to a second monitor.</summary>
    public WorkspaceWindow OpenWorkspaceWindow(string? page = null)
    {
        if (_workspaceWindow == null || _workspaceWindow.IsDisposed)
        {
            _workspaceWindow = new WorkspaceWindow(this);
            _workspaceWindow.Show(this);
        }

        _workspaceWindow.OpenPage(string.IsNullOrEmpty(page) ? "Overview" : page);
        _workspaceWindow.Activate();
        return _workspaceWindow;
    }

    private void ToggleLessonWidget(bool on)
    {
        if (!on)
        {
            _widget?.Close();
            _widget = null;
            return;
        }

        _widget = new LessonWidget(Store);
        _widget.FormClosed += (_, _) => _widget = null;
        _widget.Show(this);
    }

    private void ShowWhatsNew() =>
        EnterpriseMarkupDialog.ShowMarkup("**Highlights**\n\n- A title bar with menus, search and notifications\n- A **multi-window workspace**: open any screen in tabs, drag a tab out to use a second monitor\n- A status bar with live unread alerts\n- A start-of-day briefing for fees, tasks and lessons\n\nSee the [documentation](https://github.com/muhammad-aftab93/EnterpriseUI) for details.", "What's new", MessageBoxButtons.OK, MessageBoxIcon.Information, owner: this);

    private void ShowAbout()
    {
        using var about = new EnterpriseAboutBox();
        about.LoadFromAssembly(typeof(MainForm).Assembly);
        about.ProductName = Store.Data.Settings.SchoolName;
        about.Description = "Offline school operations: students, courses, attendance, timetable, finance and reports, built with Windows Forms and EnterpriseUI.";
        about.LinkUrl = "https://github.com/muhammad-aftab93/EnterpriseUI";
        about.Text = "About " + Store.Data.Settings.SchoolName;
        about.ShowDialog(this);
    }

    public void ToggleNotifications()
    {
        if (_flyout is { IsDisposed: false, IsOpen: true }) { _flyout.HideFlyout(); return; }
        var panel = new NotificationCenterPanel(_tray.Center, notification => { _flyout?.HideFlyout(); OpenNotification(notification); }) { Dock = DockStyle.Fill };
        var flyout = new EnterpriseFlyout { Text = "Notifications", Edge = EnterpriseFlyoutEdge.Right, FlyoutSize = 420, IsModal = false, LightDismiss = true };
        flyout.Content.Controls.Add(panel);
        flyout.FlyoutClosed += (_, _) => { panel.Dispose(); flyout.Dispose(); if (_flyout == flyout) _flyout = null; };
        _flyout = flyout;
        flyout.ShowFlyout(_workspace);
    }

    private void UpdateUnread()
    {
        var unread = _tray.UnreadCount;
        _alertsItem.Text = unread > 0 ? unread.ToString() : string.Empty;
        _statusBar.SetText("alerts", unread == 0 ? "No new alerts" : unread == 1 ? "1 new alert" : unread + " new alerts");
        if (_taskbar == null) return;
        if (unread == 0) { _taskbar.ClearOverlay(); return; }
        using var badge = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(badge))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.FillEllipse(Brushes.Firebrick, 0, 0, 31, 31);
            using var font = new Font("Segoe UI", unread > 9 ? 11f : 15f, FontStyle.Bold, GraphicsUnit.Pixel);
            TextRenderer.DrawText(g, unread > 9 ? "9+" : unread.ToString(), font, new Rectangle(0, 0, 32, 32), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        var handle = badge.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(handle);
            _taskbar.SetOverlay((Icon)icon.Clone(), unread + " unread notifications");
        }
        finally { DestroyIcon(handle); }
    }

    /// <summary>Quietly adds today's alerts to the history and shows one briefing alert that links to each of them.</summary>
    private void RaiseStartupAlerts()
    {
        var alerts = SchoolAlerts.Build(Store.Data).Take(3).ToList();
        if (alerts.Count == 0) return;
        var mode = _tray.ToastMode;
        _tray.ToastMode = NotifyToastMode.None;
        foreach (var alert in alerts) Notify(alert.Title, alert.Message, alert.Severity, alert.Page, alert.Group);
        _tray.ToastMode = mode;
        if (!Store.Data.Settings.ShowBriefing) return;

        var hour = DateTime.Now.Hour;
        var model = new EnterpriseDesktopAlert
        {
            CaptionText = (hour < 12 ? "Good morning" : hour < 18 ? "Good afternoon" : "Good evening") + " \u00b7 " + alerts.Count + (alerts.Count == 1 ? " item needs" : " items need") + " attention",
            ContentText = string.Join(Environment.NewLine, alerts.Select(alert => "\u2022 " + alert.Message)),
            AutoCloseDelaySeconds = 14
        };
        var window = new EnterpriseDesktopAlertWindow(model) { Severity = alerts.Any(alert => alert.Severity == NotificationSeverity.Warning) ? NotificationSeverity.Warning : NotificationSeverity.Info };
        foreach (var alert in alerts) window.Actions.Add(new NotificationAction(alert.Page, alert.Page));
        window.ActionClicked += (_, e) => { RestoreWindow(); if (_pages.ContainsKey(e.Action.Id)) Navigate(e.Action.Id); };
        var menu = new ContextMenuStrip();
        menu.Items.Add("Do not show the briefing at start", null, (_, _) => Store.Transaction(data => data.Settings.ShowBriefing = false));
        menu.Items.Add("Mark all notifications read", null, (_, _) => _tray.MarkAllRead());
        window.OptionsMenu = menu;
        window.AlertClosed += (_, _) => menu.Dispose();
        window.ShowAlert();
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

    private void ThemeChanged(object? sender, ITheme theme)
    {
        ApplySchoolColors();
        _statusBar.SetText("theme", theme.Name);
    }

    private void StoreChanged(object? sender, EventArgs e)
    {
        _identity.Text = Store.Data.Settings.SchoolName + "   /   " + Store.Data.Settings.AcademicYear;
        Text = Store.Data.Settings.SchoolName;
        _statusBar.SetText("records", $"{Store.Data.Students.Count:N0} students");
        if (!IsBusy) _statusBar.SetText("status", "Changes saved   \u00b7   " + DateTime.Now.ToString("HH:mm:ss"));
        if (Store.Data.Audit.Count < _auditSeen) _auditSeen = Store.Data.Audit.Count;
        foreach (var alert in SchoolAlerts.FromAudit(Store.Data, _auditSeen)) Notify(alert.Title, alert.Message, alert.Severity, alert.Page, alert.Group);
        _auditSeen = Store.Data.Audit.Count;
    }

    protected override void OnThemeApplied()
    {
        base.OnThemeApplied();
        ApplySchoolColors();
    }

    private void ApplySchoolColors()
    {
        SchoolUi.Apply(this);
        _identity.BackColor = SchoolUi.Header;
    }
}
