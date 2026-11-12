using EnterpriseUI.Data;
using EnterpriseUI.Editors.Interactivity;
using EnterpriseUI.Grids;
using EnterpriseUI.WinForms;
using TestDataGrid.Services;

namespace TestDataGrid.Views;

/// <summary>Cross-tab, heat maps and a tree map over the live school records.</summary>
public sealed class AnalyticsView : UserControl
{
    private sealed record EnrollmentFact(string Department, string Course, string Status, decimal Grade, string Student);
    private sealed record AttendanceFact(string Course, string Status);
    private sealed record FeeFact(string Department, string Student, double Balance, double OverdueDays);

    private readonly SchoolStore _store;
    private readonly Panel _content = new() { Dock = DockStyle.Fill };

    /// <summary>Only enrollments with a grade inside this range reach the pivot.</summary>
    public EnterpriseTrackBarControl GradeRange { get; } = new() { Minimum = 0, Maximum = 100, Mode = TrackSelectionMode.Range, Step = 5, LargeChange = 10, MinimumGap = 5, TickStyle = TrackTickStyle.BottomRight, TickFrequency = 25, ValueFormat = "{0:0}%", Dock = DockStyle.Fill, AccessibleName = "Grade range" };

    /// <summary>Attendance marks older than this many days are left out of the attendance heat map.</summary>
    public EnterpriseTrackBarControl AttendanceWindow { get; } = new() { Minimum = 1, Maximum = 14, Step = 1, SmallChange = 1, LargeChange = 3, Value = 7, TickStyle = TrackTickStyle.BottomRight, TickFrequency = 1, ValueFormat = "{0:0} days", Dock = DockStyle.Fill, AccessibleName = "Attendance window" };

    public AnalyticsView(SchoolStore store)
    {
        _store = store; Dock = DockStyle.Fill; Font = SchoolUi.BodyFont;
        GradeRange.ValueLow = 0; GradeRange.ValueHigh = 100;
        var filters = new TableLayoutPanel { Dock = DockStyle.Top, Height = 66, ColumnCount = 4, RowCount = 1, Padding = new Padding(20, 4, 20, 0) };
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120)); filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170)); filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        filters.Controls.Add(new EnterpriseLabelControl { Text = "&Grade range", LabelFor = GradeRange, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        filters.Controls.Add(GradeRange, 1, 0);
        filters.Controls.Add(new EnterpriseLabelControl { Text = "Attendance &window", LabelFor = AttendanceWindow, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 2, 0);
        filters.Controls.Add(AttendanceWindow, 3, 0);
        GradeRange.ValueCommitted += (_, _) => Build();
        AttendanceWindow.ValueCommitted += (_, _) => Build();
        Controls.Add(_content); Controls.Add(filters); Controls.Add(SchoolUi.Heading("Analytics"));
        Build();
        store.Changed += Changed;
    }

    public EnterprisePivotGridControl? Pivot { get; private set; }
    public EnterpriseHeatMapControl? AttendanceMap { get; private set; }
    public EnterpriseTreeMapControl? FeeMap { get; private set; }
    public EnterpriseHeatMapControl? RoomMap { get; private set; }

    private void Changed(object? sender, EventArgs e) => Build();

    /// <summary>Rebuilds the charts for the current slider positions.</summary>
    public void ApplyFilters() => Build();

    private static Panel Frame(string caption, Control content)
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 6, 10, 10) };
        content.Dock = DockStyle.Fill;
        panel.Controls.Add(content);
        panel.Controls.Add(new Label { Text = caption, Dock = DockStyle.Top, Height = 30, Font = new Font("Segoe UI", 11f, FontStyle.Bold) });
        return panel;
    }

    private void Build()
    {
        foreach (Control control in _content.Controls.Cast<Control>().ToArray()) control.Dispose();
        var data = _store.Data;
        var courses = data.Courses.ToDictionary(item => item.Id);
        var students = data.Students.ToDictionary(item => item.Id);

        var low = (decimal)GradeRange.ValueLow; var high = (decimal)GradeRange.ValueHigh;
        var enrollments = data.Enrollments.Where(item => item.Grade >= low && item.Grade <= high).Select(item => new EnrollmentFact(courses[item.CourseId].Department, courses[item.CourseId].Code, item.Status, item.Grade, students[item.StudentId].FullName)).ToList();
        Pivot = new EnterprisePivotGridControl { LabelWidth = 120, ColumnWidth = 96, AccessibleName = "Enrollments by department, course and status" };
        Pivot.RowFields.Add("Department"); Pivot.RowFields.Add("Course"); Pivot.ColumnFields.Add("Status");
        Pivot.Values.Add(new PivotValueField("Student", AggregateFunction.Count, "Students", "N0"));
        Pivot.Values.Add(new PivotValueField("Grade", AggregateFunction.Average, "Avg grade", "N1"));
        Pivot.DataSource = enrollments;

        AttendanceMap = new EnterpriseHeatMapControl { Title = "Attendance marks", ValueFormat = "N0", AccessibleName = "Attendance marks per course and status" };
        var since = DateTime.Today.AddDays(1 - (int)AttendanceWindow.Value);
        AttendanceMap.Bind(data.Attendance.Where(item => item.Date.Date >= since).Select(item => new AttendanceFact(courses[item.CourseId].Code, item.Status)).ToList(), "Course", "Status", null, HeatMapAggregate.Count);

        var fees = data.Invoices.Where(item => item.Balance > 0)
            .Select(item => new FeeFact(students[item.StudentId].Department, students[item.StudentId].FullName, (double)item.Balance, Math.Max(0, (DateTime.Today - item.DueOn.Date).TotalDays))).ToList();
        FeeMap = new EnterpriseTreeMapControl { ValueFormat = "N0", ColorMode = TreeMapColorMode.Sequential, RootCaption = "All departments", AccessibleName = "Outstanding fees by department and student" };
        FeeMap.Bind(fees, "Student", "Balance", "OverdueDays", "Department");

        var days = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday };
        var rooms = data.Lessons.Select(item => item.Room).Distinct().OrderBy(room => room).ToList();
        var hours = new double?[rooms.Count, days.Length];
        foreach (var lesson in data.Lessons)
        {
            var row = rooms.IndexOf(lesson.Room);
            var column = Array.IndexOf(days, lesson.StartsAt.DayOfWeek);
            hours[row, column] = (hours[row, column] ?? 0) + (lesson.EndsAt - lesson.StartsAt).TotalHours;
        }

        RoomMap = new EnterpriseHeatMapControl { Title = "Hours booked", ValueFormat = "0.#", AccessibleName = "Room bookings per weekday" };
        RoomMap.SetMatrix(rooms, days.Select(day => day.ToString()[..3]), hours);

        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(10) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55)); grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50)); grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        grid.Controls.Add(Frame("Enrollments by department and course", Pivot), 0, 0);
        grid.Controls.Add(Frame("Attendance by course", AttendanceMap), 1, 0);
        grid.Controls.Add(Frame("Outstanding fees (color = days overdue)", FeeMap), 0, 1);
        grid.Controls.Add(Frame("Room bookings", RoomMap), 1, 1);
        _content.Controls.Add(grid); SchoolUi.Apply(this);
    }

    protected override void Dispose(bool disposing) { if (disposing) _store.Changed -= Changed; base.Dispose(disposing); }
}

/// <summary>The activity log in a virtual grid: rows are read from the store on demand.</summary>
public sealed class AuditLogView : UserControl
{
    private readonly SchoolStore _store;
    private readonly EnterpriseVirtualGridControl _grid;
    private List<Models.AuditEntry> _rows = new();

    public AuditLogView(SchoolStore store)
    {
        _store = store; Dock = DockStyle.Fill; Font = SchoolUi.BodyFont;
        _grid = new EnterpriseVirtualGridControl { Dock = DockStyle.Fill, UseSampleData = false, AllowEdit = false, ColumnCount = 5, ColumnWidth = 190, AccessibleName = "Activity log" };
        _grid.ColumnTitles.AddRange(new[] { "Time", "Action", "Entity", "Record", "Detail" });
        _grid.CellValueNeeded += (_, e) =>
        {
            if (e.RowIndex >= _rows.Count) { e.Value = string.Empty; return; }
            var entry = _rows[e.RowIndex];
            e.Value = e.ColumnIndex switch { 0 => entry.At.ToLocalTime().ToString("g"), 1 => entry.Action, 2 => entry.Entity, 3 => entry.RecordId == 0 ? string.Empty : entry.RecordId.ToString(), _ => entry.Detail };
        };
        Controls.Add(_grid); Controls.Add(SchoolUi.Heading("Audit Log"));
        Reload();
        store.Changed += Changed;
        SchoolUi.Apply(this);
    }

    public EnterpriseVirtualGridControl Grid => _grid;
    private void Changed(object? sender, EventArgs e) => Reload();
    private void Reload() { _rows = _store.Data.Audit.OrderByDescending(item => item.At).ToList(); _grid.RowCount = _rows.Count; _grid.Invalidate(); }
    protected override void Dispose(bool disposing) { if (disposing) _store.Changed -= Changed; base.Dispose(disposing); }
}
