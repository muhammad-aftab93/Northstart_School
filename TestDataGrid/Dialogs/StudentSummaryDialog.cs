using EnterpriseUI.Editors.Interactivity;
using EnterpriseUI.WinForms;
using TestDataGrid.Models;
using TestDataGrid.Services;
using TestDataGrid.Views;

namespace TestDataGrid.Dialogs;

/// <summary>Read-only profile of one student: courses with grade and course rating, attendance and tuition paid.</summary>
public sealed class StudentSummaryDialog : Form
{
    public StudentSummaryDialog(SchoolStore store, StudentRecord student)
    {
        var data = store.Data;
        Text = student.FullName;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        Font = SchoolUi.BodyFont;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(620, 440);

        var marks = data.Attendance.Where(item => item.StudentId == student.Id).ToList();
        var attendance = marks.Count == 0 ? "no attendance recorded" : $"{100.0 * marks.Count(item => item.Status != "Absent") / marks.Count:0}% attendance";
        var header = new EnterpriseLabelControl
        {
            Dock = DockStyle.Top, Height = 92, MarkupMode = LabelMarkupMode.Markdown, AccessibleName = "Student",
            Text = $"**{student.FullName}** ({student.StudentCode}){(student.Active ? string.Empty : "  ~~archived~~")}\n{student.Email}\n{student.Department} \u00b7 Year {student.Year} \u00b7 {attendance}"
        };

        var rows = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoScroll = true, Padding = new Padding(4, 4, 4, 0) };
        rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        rows.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        foreach (var enrollment in data.Enrollments.Where(item => item.StudentId == student.Id).OrderBy(item => item.CourseId))
        {
            var course = data.Courses.Single(item => item.Id == enrollment.CourseId);
            rows.Controls.Add(new EnterpriseLabelControl { Text = $"**{course.Code}** {course.Title}\n{enrollment.Status} \u00b7 grade {enrollment.Grade:0}%", MarkupMode = LabelMarkupMode.Markdown, Dock = DockStyle.Fill, Height = 46, AccessibleName = course.Title });
            rows.Controls.Add(new EnterpriseStarRatingControl { ReadOnly = true, Precision = RatingPrecision.Half, SymbolSize = 20, Value = (double)enrollment.Feedback, Height = 46, Width = 190, AccessibleName = course.Title + " rating", Descriptions = new[] { "Poor", "Fair", "Good", "Very good", "Excellent" } });
        }

        var invoices = data.Invoices.Where(item => item.StudentId == student.Id).ToList();
        var billed = invoices.Sum(item => item.Amount);
        var paid = invoices.Sum(item => item.Paid);
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 104, Padding = new Padding(4, 6, 4, 6) };
        footer.Controls.Add(new EnterpriseLabelControl { Dock = DockStyle.Fill, MarkupMode = LabelMarkupMode.Markdown, Text = $"Billed {billed:N2} {data.Settings.Currency} \u00b7 paid {paid:N2} \u00b7 **outstanding {billed - paid:N2} {data.Settings.Currency}**" });
        footer.Controls.Add(new EnterpriseProgressBarControl { Dock = DockStyle.Top, Height = 24, Maximum = 100, Value = billed == 0 ? 100 : (int)Math.Round(paid / billed * 100), TextFormat = "{0:0}% of tuition paid", AccessibleName = "Tuition paid" });
        var close = new EnterpriseButtonControl { Text = "Close", Width = 100, Height = 34, DialogResult = DialogResult.OK, Dock = DockStyle.Bottom };
        var closeRow = new Panel { Dock = DockStyle.Bottom, Height = 52, Padding = new Padding(0, 8, 14, 8) };
        close.Dock = DockStyle.Right;
        closeRow.Controls.Add(close);

        Controls.Add(rows);
        Controls.Add(footer);
        Controls.Add(closeRow);
        Controls.Add(header);
        AcceptButton = close;
        CancelButton = close;
        SchoolUi.Apply(this);
    }
}
