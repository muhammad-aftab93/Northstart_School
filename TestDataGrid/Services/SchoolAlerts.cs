using EnterpriseUI.Editors.Interactivity;
using TestDataGrid.Models;

namespace TestDataGrid.Services;

public sealed record SchoolAlert(string Title, string Message, NotificationSeverity Severity, string Group, string Page);

/// <summary>Conditions the front office should know about, derived from the live records.</summary>
public static class SchoolAlerts
{
    public static IReadOnlyList<SchoolAlert> Build(SchoolData data)
    {
        var alerts = new List<SchoolAlert>();
        var overdue = data.Invoices.Where(item => item.Status == "Overdue").ToList();
        if (overdue.Count > 0)
            alerts.Add(new SchoolAlert("Overdue tuition", $"{overdue.Count} invoices are overdue ({overdue.Sum(item => item.Balance):N0} {data.Settings.Currency}).", NotificationSeverity.Warning, "overdue", "Finance"));

        var late = data.Tasks.Count(item => item.Status != "Done" && item.DueOn.Date < DateTime.Today);
        var soon = data.Tasks.Count(item => item.Status != "Done" && item.DueOn.Date >= DateTime.Today && item.DueOn.Date <= DateTime.Today.AddDays(2));
        if (late + soon > 0)
            alerts.Add(new SchoolAlert("Tasks need attention", $"{late} past due and {soon} due within two days.", late > 0 ? NotificationSeverity.Warning : NotificationSeverity.Info, "tasks", "Tasks"));

        var today = data.Lessons.Count(item => item.StartsAt.Date == DateTime.Today);
        if (today > 0)
            alerts.Add(new SchoolAlert("Today's timetable", $"{today} lessons are scheduled today.", NotificationSeverity.Info, "lessons", "Timetable"));

        var unmarked = data.Enrollments.Count(item => item.Status == "Enrolled" && !data.Attendance.Any(mark => mark.StudentId == item.StudentId && mark.CourseId == item.CourseId && mark.Date.Date == DateTime.Today));
        if (today > 0 && unmarked > 0)
            alerts.Add(new SchoolAlert("Roll call pending", $"Attendance is not recorded yet for {unmarked} enrollments today.", NotificationSeverity.Info, "rollcall", "Attendance"));
        return alerts;
    }

    /// <summary>One toast for each audit entry written since <paramref name="seen"/> entries were known, for actions people should hear about.</summary>
    public static IEnumerable<SchoolAlert> FromAudit(SchoolData data, int seen)
    {
        foreach (var entry in data.Audit.Skip(Math.Max(0, seen)))
        {
            switch (entry.Action)
            {
                case "Payment": yield return new SchoolAlert("Payment recorded", entry.Detail, NotificationSeverity.Success, "overdue", "Finance"); break;
                case "Roll call": yield return new SchoolAlert("Roll call saved", entry.Detail, NotificationSeverity.Success, "rollcall", "Attendance"); break;
                case "Admit": yield return new SchoolAlert("Student admitted", entry.Detail, NotificationSeverity.Success, "admit", "Students"); break;
            }
        }
    }
}
