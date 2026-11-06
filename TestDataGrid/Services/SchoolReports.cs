using EnterpriseUI.Exporting;
using TestDataGrid.Models;

namespace TestDataGrid.Services;

public static class SchoolReports
{
    public static ExportDocument Build(SchoolData data, string kind)
    {
        var document = new ExportDocument { Title = data.Settings.SchoolName + " · " + kind, Author = data.Settings.SchoolName };
        IReadOnlyList<ExportColumn> columns;
        IEnumerable<ExportRow> rows;
        if (kind == "Outstanding Fees")
        {
            columns = new[] { new ExportColumn("Number", "Invoice"), new("Student", "Student"), new("Due", "Due date"), new("Amount", "Amount") { Format = "N2" }, new("Paid", "Paid") { Format = "N2" }, new("Balance", "Balance") { Format = "N2" } };
            rows = data.Invoices.Where(item => item.Balance > 0).OrderBy(item => item.DueOn).Select(item => new ExportRow(ExportRowKind.Data, 0, new object?[] { item.Number, data.Students.Single(student => student.Id == item.StudentId).FullName, item.DueOn, item.Amount, item.Paid, item.Balance }));
        }
        else if (kind == "Attendance Summary")
        {
            columns = new[] { new ExportColumn("Student", "Student"), new("Present", "Present"), new("Absent", "Absent"), new("Late", "Late"), new("Rate", "Attendance %") { Format = "N1" } };
            rows = data.Students.Select(student =>
            {
                var marks = data.Attendance.Where(item => item.StudentId == student.Id).ToArray();
                return new ExportRow(ExportRowKind.Data, 0, new object?[] { student.FullName, marks.Count(item => item.Status == "Present"), marks.Count(item => item.Status == "Absent"), marks.Count(item => item.Status == "Late"), marks.Length == 0 ? 0 : marks.Count(item => item.Status is "Present" or "Late" or "Excused") * 100m / marks.Length });
            });
        }
        else if (kind == "Course Results")
        {
            columns = new[] { new ExportColumn("Code", "Course"), new("Title", "Title"), new("Teacher", "Teacher"), new("Enrolled", "Enrolled"), new("Grade", "Average grade") { Format = "N1" } };
            rows = data.Courses.Select(course => new ExportRow(ExportRowKind.Data, 0, new object?[] { course.Code, course.Title, data.Staff.Single(staff => staff.Id == course.TeacherId).FullName, data.Enrollments.Count(item => item.CourseId == course.Id), data.Enrollments.Where(item => item.CourseId == course.Id).Select(item => item.Grade).DefaultIfEmpty().Average() }));
        }
        else
        {
            columns = new[] { new ExportColumn("At", "Time"), new("Action", "Action"), new("Entity", "Entity"), new("Id", "Record"), new("Detail", "Detail") };
            rows = data.Audit.OrderByDescending(item => item.At).Select(item => new ExportRow(ExportRowKind.Data, 0, new object?[] { item.At.ToLocalTime(), item.Action, item.Entity, item.RecordId, item.Detail }));
        }
        document.Sheets.Add(new ExportSheet(kind, columns, rows.ToList()) { Title = document.Title });
        return document;
    }
}