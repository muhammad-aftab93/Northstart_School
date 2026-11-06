using EnterpriseUI.Grids;
using EnterpriseUI.Theming;
using TestDataGrid.Models;

namespace TestDataGrid.Views;

public static class SchoolUi
{
    public static Color Surface => ColorTranslator.FromHtml(GridPalette.FromTheme(ThemeManager.Instance.CurrentTheme).Surface);
    public static Color Text => ColorTranslator.FromHtml(GridPalette.FromTheme(ThemeManager.Instance.CurrentTheme).Text);
    public static Color Header => ColorTranslator.FromHtml(GridPalette.FromTheme(ThemeManager.Instance.CurrentTheme).Header);
    public static Color Accent => ColorTranslator.FromHtml(GridPalette.FromTheme(ThemeManager.Instance.CurrentTheme).Primary);
    public static Font BodyFont => new("Segoe UI", 10f);

    public static Label Heading(string title) => new() { Text = title, Dock = DockStyle.Top, Height = 56, Padding = new Padding(20, 0, 10, 0), TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 17f, FontStyle.Bold), ForeColor = Text, BackColor = Surface };

    public static void Apply(Control root)
    {
        root.BackColor = Surface;
        root.ForeColor = Text;
        foreach (Control control in root.Controls)
        {
            if (control is Button button) { button.UseVisualStyleBackColor = true; button.ForeColor = SystemColors.ControlText; }
            else if (control is TextBox or ComboBox or NumericUpDown) { control.BackColor = Surface; control.ForeColor = Text; }
            else Apply(control);
        }
    }
}

public sealed record LookupOption(int Id, string Label);
public sealed record RecordField(string Name, string Label, int Width = 150, bool ReadOnly = false, string[]? Choices = null, Func<SchoolData, IEnumerable<LookupOption>>? Lookup = null, bool Multiline = false);

public static class SchoolSchema
{
    private static IEnumerable<LookupOption> Students(SchoolData data) => data.Students.Select(item => new LookupOption(item.Id, item.StudentCode + " · " + item.FullName));
    private static IEnumerable<LookupOption> Courses(SchoolData data) => data.Courses.Select(item => new LookupOption(item.Id, item.Code + " · " + item.Title));
    private static IEnumerable<LookupOption> Staff(SchoolData data) => data.Staff.Select(item => new LookupOption(item.Id, item.FullName));
    private static readonly string[] Departments = { "Science", "Business", "Engineering", "Arts" };

    public static RecordField[] Fields<T>() where T : SchoolRecord => typeof(T) switch
    {
        var type when type == typeof(StudentRecord) => new[] { new RecordField("StudentCode", "Student code", 120), new("FullName", "Full name", 220), new("Email", "Email", 250), new("Department", "Department", 140, Choices: Departments), new("Year", "Year", 80), new("EnrolledOn", "Enrolled", 120), new("Active", "Active", 80) },
        var type when type == typeof(StaffRecord) => new[] { new RecordField("FullName", "Full name", 230), new("Email", "Email", 250), new("Department", "Department", 140, Choices: Departments), new("Position", "Position", 150, Choices: new[] { "Teacher", "Coordinator", "Administrator" }), new("Active", "Active", 80) },
        var type when type == typeof(CourseRecord) => new[] { new RecordField("Code", "Course code", 120), new("Title", "Course title", 260), new("Department", "Department", 150, Choices: Departments), new("TeacherId", "Teacher ID", 100, Lookup: Staff), new("Credits", "Credits", 90), new("Capacity", "Capacity", 100), new("Active", "Active", 80) },
        var type when type == typeof(EnrollmentRecord) => new[] { new RecordField("StudentId", "Student ID", 100, Lookup: Students), new("CourseId", "Course ID", 100, Lookup: Courses), new("EnrolledOn", "Enrolled", 140), new("Status", "Status", 150, Choices: new[] { "Enrolled", "Completed", "Withdrawn" }), new("Grade", "Grade %", 120) },
        var type when type == typeof(AttendanceRecord) => new[] { new RecordField("StudentId", "Student ID", 100, Lookup: Students), new("CourseId", "Course ID", 100, Lookup: Courses), new("Date", "Date", 140), new("Status", "Status", 140, Choices: new[] { "Present", "Absent", "Late", "Excused" }), new("Notes", "Notes", 300, Multiline: true) },
        var type when type == typeof(TaskRecord) => new[] { new RecordField("Title", "Task", 300), new("OwnerId", "Owner ID", 110, Lookup: Staff), new("DueOn", "Due date", 140), new("Priority", "Priority", 120, Choices: new[] { "Low", "Normal", "High", "Urgent" }), new("Status", "Status", 150, Choices: new[] { "To do", "In progress", "Done" }), new("Progress", "Progress %", 140), new("Notes", "Notes", 250, Multiline: true) },
        var type when type == typeof(LessonRecord) => new[] { new RecordField("CourseId", "Course ID", 120, Lookup: Courses), new("StartsAt", "Starts", 180), new("EndsAt", "Ends", 180), new("Room", "Room", 120), new("Notes", "Notes", 260, Multiline: true) },
        var type when type == typeof(InvoiceRecord) => new[] { new RecordField("Number", "Invoice number", 170), new("StudentId", "Student ID", 110, Lookup: Students), new("Description", "Description", 240), new("Amount", "Amount", 140), new("Paid", "Paid", 140, ReadOnly: true), new("Balance", "Balance", 140, ReadOnly: true), new("DueOn", "Due date", 140), new("Status", "Status", 130, ReadOnly: true) },
        _ => throw new NotSupportedException(typeof(T).Name)
    };
}