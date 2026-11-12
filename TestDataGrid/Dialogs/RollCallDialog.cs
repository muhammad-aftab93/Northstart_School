using EnterpriseUI.WinForms;
using TestDataGrid.Models;
using TestDataGrid.Services;
using TestDataGrid.Views;

namespace TestDataGrid.Dialogs;

/// <summary>Takes the register of one course for one day: pick the course, untick the students who are absent, save.</summary>
public sealed class RollCallDialog : Form
{
    private sealed record Choice(int Id, string Label);

    private readonly SchoolStore _store;
    private readonly List<DateTime> _dates = Enumerable.Range(0, 14).Select(offset => DateTime.Today.AddDays(-offset)).ToList();
    private readonly List<Choice> _students = new();
    private readonly Label _summary = new() { AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Bottom };
    private readonly Label _error = new() { Dock = DockStyle.Bottom, Height = 28, ForeColor = Color.Firebrick, AutoEllipsis = true };

    public EnterpriseListControl CourseList { get; } = new() { DisplayMember = "Label", ItemHeight = 36, Dock = DockStyle.Fill, AccessibleName = "Courses", EmptyText = "No active courses" };
    public EnterpriseCheckedListBoxControl StudentList { get; } = new() { DisplayMember = "Label", ItemHeight = 34, Dock = DockStyle.Fill, AccessibleName = "Students present", EmptyText = "No enrolled students" };
    public EnterpriseDropDownControl DateChoice { get; } = new() { Width = 240, Height = 34, AccessibleName = "Register date" };

    public RollCallDialog(SchoolStore store)
    {
        _store = store;
        Text = "Roll Call";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        Font = SchoolUi.BodyFont;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(820, 540);

        DateChoice.SetItems(_dates.Select(date => date == DateTime.Today ? "Today \u00b7 " + date.ToString("ddd dd MMM yyyy") : date.ToString("ddd dd MMM yyyy")));
        DateChoice.SelectedIndex = 0;
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 58, Padding = new Padding(18, 12, 18, 6), WrapContents = false };
        top.Controls.Add(new Label { Text = "Register date", AutoSize = true, Margin = new Padding(0, 8, 12, 0) });
        top.Controls.Add(DateChoice);

        var split = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42)); split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        var left = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18, 0, 6, 8) };
        var right = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6, 0, 18, 8) };
        split.Controls.Add(left, 0, 0); split.Controls.Add(right, 1, 0);
        left.Controls.Add(CourseList);
        left.Controls.Add(new Label { Text = "Course", Dock = DockStyle.Top, Height = 26, Font = new Font(Font, FontStyle.Bold) });
        right.Controls.Add(StudentList);
        right.Controls.Add(new Label { Text = "Tick the students who are present", Dock = DockStyle.Top, Height = 26, Font = new Font(Font, FontStyle.Bold) });

        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 58, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(18, 10, 18, 10) };
        var cancel = new EnterpriseButtonControl { Text = "Cancel", Variant = EnterpriseButtonVariant.Outline, Width = 100, Height = 34, DialogResult = DialogResult.Cancel };
        var save = new EnterpriseButtonControl { Text = "Save roll call", Glyph = "\u2714", Variant = EnterpriseButtonVariant.Success, Width = 160, Height = 34 };
        var none = new EnterpriseButtonControl { Text = "Untick all", Variant = EnterpriseButtonVariant.Outline, Width = 110, Height = 34, Margin = new Padding(24, 3, 3, 3) };
        var all = new EnterpriseButtonControl { Text = "Tick all", Variant = EnterpriseButtonVariant.Outline, Width = 110, Height = 34 };
        save.Click += (_, _) => Save();
        none.Click += (_, _) => StudentList.UncheckAll();
        all.Click += (_, _) => StudentList.CheckAll();
        bar.Controls.AddRange(new Control[] { cancel, save, none, all });
        var summaryBar = new Panel { Dock = DockStyle.Bottom, Height = 30, Padding = new Padding(22, 0, 0, 0) };
        _summary.Location = new Point(22, 5);
        summaryBar.Controls.Add(_summary);

        Controls.Add(split);
        Controls.Add(summaryBar);
        Controls.Add(_error);
        Controls.Add(bar);
        Controls.Add(top);
        AcceptButton = save;
        CancelButton = cancel;

        CourseList.DataSource = _store.Data.Courses.Where(course => course.Active).OrderBy(course => course.Code).Select(course => new Choice(course.Id, course.Code + " \u00b7 " + course.Title)).ToList();
        CourseList.SelectionChanged += (_, _) => LoadStudents();
        DateChoice.SelectionChanged += (_, _) => LoadStudents();
        StudentList.ItemCheck += (_, _) => { if (IsHandleCreated) BeginInvoke(new Action(UpdateSummary)); };
        if (CourseList.Count > 0) CourseList.SelectedIndex = 0;
        SchoolUi.Apply(this);
        _error.ForeColor = Color.Firebrick;
    }

    private DateTime SelectedDate => _dates[Math.Max(0, DateChoice.SelectedIndex)];
    private Choice? SelectedCourse => CourseList.SelectedItem as Choice;

    private void LoadStudents()
    {
        _students.Clear();
        StudentList.ClearItems();
        _error.Text = string.Empty;
        if (SelectedCourse is not { } course) { UpdateSummary(); return; }

        var data = _store.Data;
        var date = SelectedDate.Date;
        var enrolled = data.Enrollments.Where(item => item.CourseId == course.Id && item.Status == "Enrolled")
            .Join(data.Students.Where(student => student.Active), item => item.StudentId, student => student.Id, (item, student) => student)
            .OrderBy(student => student.FullName).ToList();
        foreach (var student in enrolled) _students.Add(new Choice(student.Id, student.StudentCode + " \u00b7 " + student.FullName));
        StudentList.AddRange(_students.Cast<object>());
        for (var index = 0; index < enrolled.Count; index++)
        {
            var mark = data.Attendance.FirstOrDefault(item => item.StudentId == enrolled[index].Id && item.CourseId == course.Id && item.Date.Date == date);
            StudentList.SetItemChecked(index, mark == null || mark.Status is "Present" or "Late" or "Excused");
        }

        UpdateSummary();
    }

    private void UpdateSummary() => _summary.Text = $"{StudentList.CheckedIndices.Count} of {StudentList.Count} present";

    public bool Save()
    {
        try
        {
            var course = SelectedCourse ?? throw new InvalidOperationException("Select a course first.");
            var date = SelectedDate.Date;
            var marks = _students.Select((student, index) => (student.Id, Present: StudentList.GetItemChecked(index))).ToList();
            _store.Transaction(data =>
            {
                var next = SchoolOperations.NextId<AttendanceRecord>(data);
                foreach (var (studentId, present) in marks)
                {
                    var existing = data.Attendance.FirstOrDefault(item => item.StudentId == studentId && item.CourseId == course.Id && item.Date.Date == date);
                    if (existing == null) data.Attendance.Add(new AttendanceRecord { Id = next++, StudentId = studentId, CourseId = course.Id, Date = date, Status = present ? "Present" : "Absent" });
                    else if (!present) existing.Status = "Absent";
                    else if (existing.Status == "Absent") existing.Status = "Present";
                }

                data.Audit.Add(new AuditEntry { Action = "Roll call", Entity = "Attendance", RecordId = course.Id, Detail = $"{course.Label}, {date:d}: {marks.Count(mark => mark.Present)}/{marks.Count} present" });
            });
            DialogResult = DialogResult.OK;
            return true;
        }
        catch (Exception exception) { _error.Text = exception.Message; return false; }
    }
}
