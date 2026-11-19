using System.Globalization;
using EnterpriseUI.Documents;
using EnterpriseUI.Exporting;
using EnterpriseUI.Office;
using EnterpriseUI.Pdf;
using TestDataGrid.Models;

namespace TestDataGrid.Services;

/// <summary>Builds the Word, PDF and Excel documents the school hands out: transcripts, report packs, grade books and the attendance ledger.</summary>
public static class SchoolDocuments
{
    public static readonly string[] ReportKinds = { "Outstanding Fees", "Attendance Summary", "Course Results", "Activity Log" };

    public static string FormatCell(ExportColumn column, object? value) => value switch
    {
        null => string.Empty,
        IFormattable formattable when !string.IsNullOrEmpty(column.Format) => formattable.ToString(column.Format, null),
        DateTime date => date.ToString("g"),
        _ => Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty
    };

    public static WordDocument BuildTranscript(SchoolData data, StudentRecord student)
    {
        var document = new WordDocument { Title = "Transcript - " + student.FullName, Author = data.Settings.SchoolName };
        document.AddParagraph(data.Settings.SchoolName, WordParagraphStyle.Heading1);
        document.AddParagraph("Student transcript, academic year " + data.Settings.AcademicYear, WordParagraphStyle.Heading2);

        var identity = document.AddParagraph(string.Empty);
        identity.Runs.Clear();
        identity.Runs.Add(new WordRun(student.FullName, bold: true));
        identity.Runs.Add(new WordRun($" ({student.StudentCode})\n{student.Email}\n{student.Department}, year {student.Year}, enrolled {student.EnrolledOn:d}"));

        var rows = data.Enrollments.Where(item => item.StudentId == student.Id).OrderBy(item => item.CourseId)
            .Select(item => (Enrollment: item, Course: data.Courses.Single(course => course.Id == item.CourseId))).ToList();
        var table = new List<string[]> { new[] { "Course", "Title", "Credits", "Status", "Grade" } };
        table.AddRange(rows.Select(row => new[] { row.Course.Code, row.Course.Title, row.Course.Credits.ToString(CultureInfo.InvariantCulture), row.Enrollment.Status, row.Enrollment.Grade.ToString("0", CultureInfo.CurrentCulture) + "%" }));
        document.AddTable(table);

        var marks = data.Attendance.Where(item => item.StudentId == student.Id).ToList();
        var average = rows.Count == 0 ? "n/a" : rows.Average(row => row.Enrollment.Grade).ToString("0.0", CultureInfo.CurrentCulture) + "%";
        var attendance = marks.Count == 0 ? "no attendance recorded" : (100.0 * marks.Count(item => item.Status != "Absent") / marks.Count).ToString("0", CultureInfo.CurrentCulture) + "% attendance";
        var summary = document.AddParagraph(string.Empty);
        summary.Runs.Clear();
        summary.Runs.Add(new WordRun("Average grade: ", bold: true));
        summary.Runs.Add(new WordRun($"{average}   "));
        summary.Runs.Add(new WordRun("Credits: ", bold: true));
        summary.Runs.Add(new WordRun($"{rows.Sum(row => row.Course.Credits)}   "));
        summary.Runs.Add(new WordRun("Attendance: ", bold: true));
        summary.Runs.Add(new WordRun(attendance));
        var stamp = document.AddParagraph("Generated " + DateTime.Now.ToString("f", CultureInfo.CurrentCulture));
        stamp.Runs[0].Italic = true;
        return document;
    }

    public static WordDocument BuildWordReport(ExportDocument report)
    {
        var document = new WordDocument { Title = report.Title, Author = report.Author };
        document.AddParagraph(report.Title, WordParagraphStyle.Heading1);
        foreach (var sheet in report.Sheets)
        {
            document.AddParagraph(sheet.Name, WordParagraphStyle.Heading2);
            var rows = new List<string[]> { sheet.Columns.Select(column => column.Title).ToArray() };
            rows.AddRange(sheet.Rows.Select(row => row.Cells.Select((value, index) => FormatCell(sheet.Columns[index], value)).ToArray()));
            document.AddTable(rows);
        }
        return document;
    }

    /// <summary>Every report as text pages in one merged PDF.</summary>
    public static PdfDocument BuildReportPack(SchoolData data)
    {
        var parts = new List<PdfDocument>();
        foreach (var kind in ReportKinds)
        {
            var report = SchoolReports.Build(data, kind);
            var sheet = report.Sheets[0];
            var lines = new List<string> { report.Title, string.Empty, string.Join(" | ", sheet.Columns.Select(column => column.Title)), new string('-', 60) };
            lines.AddRange(sheet.Rows.Select(row => string.Join(" | ", row.Cells.Select((value, index) => FormatCell(sheet.Columns[index], value)))));
            parts.Add(EnterprisePdfEngine.CreateFromLines(report.Title, lines, maxCharsPerLine: 100));
        }
        var pack = EnterprisePdfEngine.Merge(parts, data.Settings.SchoolName + " report pack");
        pack.Author = data.Settings.SchoolName;
        return pack;
    }

    /// <summary>One sheet per course with live formulas for the average, highest, lowest and count, plus a summary sheet.</summary>
    public static SpreadsheetWorkbook BuildGradeBook(SchoolData data)
    {
        var book = new SpreadsheetWorkbook();
        var summary = new SpreadsheetWorksheet { Name = "Summary" };
        SetRow(summary, 1, "Course", "Title", "Enrolled", "Average grade");
        var summaryRow = 2;

        foreach (var course in data.Courses.Where(item => item.Active).OrderBy(item => item.Code))
        {
            var grades = data.Enrollments.Where(item => item.CourseId == course.Id && item.Status != "Withdrawn")
                .Join(data.Students, item => item.StudentId, student => student.Id, (item, student) => (student.StudentCode, student.FullName, item.Grade))
                .OrderBy(item => item.FullName).ToList();
            if (grades.Count == 0) continue;

            var sheet = new SpreadsheetWorksheet { Name = course.Code };
            SetRow(sheet, 1, "Student code", "Student", "Grade");
            for (var i = 0; i < grades.Count; i++) SetRow(sheet, i + 2, grades[i].StudentCode, grades[i].FullName, grades[i].Grade);
            var last = grades.Count + 1;
            string range = $"C2:C{last}";
            SetRow(sheet, last + 2, null, "Average", null).GetOrCreateCell($"C{last + 2}").Formula = $"=ROUND(AVERAGE({range}),1)";
            SetRow(sheet, last + 3, null, "Highest", null).GetOrCreateCell($"C{last + 3}").Formula = $"=MAX({range})";
            SetRow(sheet, last + 4, null, "Lowest", null).GetOrCreateCell($"C{last + 4}").Formula = $"=MIN({range})";
            SetRow(sheet, last + 5, null, "Students", null).GetOrCreateCell($"C{last + 5}").Formula = $"=COUNT({range})";
            book.Worksheets.Add(sheet);

            SetRow(summary, summaryRow++, course.Code, course.Title, grades.Count, Math.Round(grades.Average(item => item.Grade), 1));
        }

        book.Worksheets.Insert(0, summary);
        return book;
    }

    /// <summary>Streams every attendance mark, oldest first, without holding the sheet in memory.</summary>
    public static int WriteAttendanceLedger(SchoolData data, Stream output)
    {
        var students = data.Students.ToDictionary(item => item.Id);
        var courses = data.Courses.ToDictionary(item => item.Id);
        using var writer = new EnterpriseSheetStreamWriter(output, leaveOpen: true);
        writer.BeginSheet("Attendance");
        writer.WriteRow("Date", "Student code", "Student", "Course", "Status", "Notes");
        var rows = 0;
        foreach (var mark in data.Attendance.OrderBy(item => item.Date).ThenBy(item => item.StudentId))
        {
            students.TryGetValue(mark.StudentId, out var student);
            courses.TryGetValue(mark.CourseId, out var course);
            writer.WriteRow(mark.Date.Date, student?.StudentCode ?? string.Empty, student?.FullName ?? string.Empty, course?.Code ?? string.Empty, mark.Status, mark.Notes);
            rows++;
        }
        return rows;
    }

    private static SpreadsheetWorksheet SetRow(SpreadsheetWorksheet sheet, int row, params object?[] values)
    {
        for (var column = 0; column < values.Length; column++)
            if (values[column] is { } value) sheet.GetOrCreateCell(SheetReference.Format(row, column + 1)).Value = value;
        return sheet;
    }
}
