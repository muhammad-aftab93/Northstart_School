using System.Globalization;
using System.Text;
using EnterpriseUI.Editors.Interactivity;
using TestDataGrid.Models;

namespace TestDataGrid.Services;

public sealed record AssistantReply(string Text, string[] QuickReplies);

/// <summary>Offline assistant over the school records: drafts letters for the prompt control and answers questions for the chat.</summary>
public sealed class SchoolAssistant : IAIPromptProvider
{
    public const string FeeReminder = "fee-reminder", CourseSummary = "course-summary", AttendanceNote = "attendance-note", Announcement = "announcement";

    public static IReadOnlyList<AIPromptCommand> Commands { get; } = new[]
    {
        new AIPromptCommand(FeeReminder, "Fee reminder", "Draft a tuition reminder for: {input}", "Student name or code, or an invoice number; empty picks the largest overdue balance", "✉", "Letters"),
        new AIPromptCommand(AttendanceNote, "Attendance note", "Draft an attendance follow-up for: {input}", "Student name or code", "✔", "Letters"),
        new AIPromptCommand(CourseSummary, "Course summary", "Summarize the course: {input}", "Course code or title", "≡", "Reports"),
        new AIPromptCommand(Announcement, "Announcement", "Write a school announcement from these notes: {input}", "Rough notes become a clean announcement", "✎", "Letters")
    };

    private readonly SchoolStore _store;

    public SchoolAssistant(SchoolStore store) => _store = store;

    public int StreamDelayMilliseconds { get; set; } = 20;

    public async Task<AIPromptResponse> GetResponseAsync(AIPromptRequest request, IProgress<string>? partial, CancellationToken cancellationToken)
    {
        var text = Draft(_store.Data, request.CommandId, request.Input);
        if (partial != null && StreamDelayMilliseconds > 0)
        {
            var words = text.Split(' ');
            var builder = new StringBuilder();
            for (var index = 0; index < words.Length; index += 5)
            {
                cancellationToken.ThrowIfCancellationRequested();
                builder.Append(string.Join(' ', words.Skip(index).Take(5))).Append(' ');
                partial.Report(builder.ToString().TrimEnd());
                await Task.Delay(StreamDelayMilliseconds, cancellationToken).ConfigureAwait(true);
            }
        }

        return new AIPromptResponse(text, null, "northstar-local");
    }

    public static string Draft(SchoolData data, string? commandId, string input) => commandId switch
    {
        FeeReminder => FeeLetter(data, input),
        AttendanceNote => AttendanceLetter(data, input),
        CourseSummary => CourseReport(data, input),
        Announcement => AnnouncementText(data, input),
        null => SchoolAnswers.Reply(data, input).Text,
        _ => SampleAIPromptProvider.Transform(commandId, input)
    };

    public static StudentRecord? FindStudent(SchoolData data, string text) =>
        data.Students.FirstOrDefault(student => text.Contains(student.StudentCode, StringComparison.OrdinalIgnoreCase))
        ?? data.Students.FirstOrDefault(student => text.Contains(student.FullName, StringComparison.OrdinalIgnoreCase))
        ?? data.Invoices.Where(invoice => text.Contains(invoice.Number, StringComparison.OrdinalIgnoreCase)).Select(invoice => data.Students.FirstOrDefault(student => student.Id == invoice.StudentId)).FirstOrDefault();

    private static string Money(SchoolData data, decimal amount) => amount.ToString("N2", CultureInfo.CurrentCulture) + " " + data.Settings.Currency;

    private static string FeeLetter(SchoolData data, string input)
    {
        var student = FindStudent(data, input);
        var invoices = student != null
            ? data.Invoices.Where(invoice => invoice.StudentId == student.Id && invoice.Balance > 0).OrderBy(invoice => invoice.DueOn).ToList()
            : data.Invoices.Where(invoice => invoice.Status == "Overdue").OrderByDescending(invoice => invoice.Balance).Take(1).ToList();
        if (invoices.Count == 0) return student == null ? "No overdue invoices were found, so there is nobody to remind." : $"{student.FullName} has no outstanding balance.";
        student ??= data.Students.First(item => item.Id == invoices[0].StudentId);
        var total = invoices.Sum(invoice => invoice.Balance);
        var overdue = invoices.Any(invoice => invoice.Status == "Overdue");
        return $"Subject: Tuition {(overdue ? "payment overdue" : "reminder")} for {student.FullName} ({student.StudentCode})\n\n"
            + $"Dear parent or guardian of {student.FullName},\n\n"
            + $"Our records show an outstanding balance of {Money(data, total)} on {string.Join(", ", invoices.Select(invoice => invoice.Number))}, "
            + $"{(overdue ? "which was due on" : "due on")} {invoices[0].DueOn:d}. Please arrange payment or contact the school office if you would like to agree a payment plan.\n\n"
            + $"Kind regards,\n{data.Settings.SchoolName} finance office";
    }

    private static string AttendanceLetter(SchoolData data, string input)
    {
        var student = FindStudent(data, input);
        if (student == null) return "Name a student or give a student code, for example ST-0007.";
        var since = DateTime.Today.AddDays(-14);
        var marks = data.Attendance.Where(item => item.StudentId == student.Id && item.Date.Date >= since).ToList();
        if (marks.Count == 0) return $"There are no attendance marks for {student.FullName} in the last 14 days.";
        var absent = marks.Count(item => item.Status == "Absent");
        var late = marks.Count(item => item.Status == "Late");
        return $"Subject: Attendance update for {student.FullName}\n\nDear parent or guardian of {student.FullName},\n\n"
            + $"In the last 14 days {student.FullName} was marked absent {absent} time(s) and late {late} time(s) out of {marks.Count} sessions ({100.0 * (marks.Count - absent) / marks.Count:0}% attendance). "
            + (absent + late > 0 ? "Please let us know if there is anything we can do to help." : "Thank you for your support.")
            + $"\n\nKind regards,\n{data.Settings.SchoolName}";
    }

    private static string CourseReport(SchoolData data, string input)
    {
        var course = data.Courses.FirstOrDefault(item => input.Contains(item.Code, StringComparison.OrdinalIgnoreCase)) ?? data.Courses.FirstOrDefault(item => input.Contains(item.Title, StringComparison.OrdinalIgnoreCase));
        if (course == null) return "Name a course by code or title. Available: " + string.Join(", ", data.Courses.OrderBy(item => item.Code).Select(item => item.Code));
        var enrolled = data.Enrollments.Where(item => item.CourseId == course.Id).ToList();
        var teacher = data.Staff.FirstOrDefault(item => item.Id == course.TeacherId)?.FullName ?? "unassigned";
        var marks = data.Attendance.Where(item => item.CourseId == course.Id).ToList();
        var rated = enrolled.Where(item => item.Feedback > 0).ToList();
        return $"{course.Code} · {course.Title} ({course.Department})\nTeacher: {teacher}\n"
            + $"Enrolled: {enrolled.Count(item => item.Status == "Enrolled")} of {course.Capacity} seats\n"
            + (enrolled.Count > 0 ? $"Average grade: {enrolled.Average(item => item.Grade):0.#}%\n" : string.Empty)
            + (marks.Count > 0 ? $"Attendance: {100.0 * marks.Count(item => item.Status != "Absent") / marks.Count:0}% present\n" : string.Empty)
            + (rated.Count > 0 ? $"Student rating: {rated.Average(item => (double)item.Feedback):0.0} / 5 from {rated.Count} students" : "No student ratings yet");
    }

    private static string AnnouncementText(SchoolData data, string input)
    {
        var body = SampleAIPromptProvider.Transform(SampleAIPromptProvider.Rewrite, input);
        return $"{data.Settings.SchoolName} announcement\n\n{body}\n\nThank you,\nSchool office";
    }
}

/// <summary>Question answering for the chat: keyword routing over live records, with Markdown links to the screens.</summary>
public static class SchoolAnswers
{
    public static readonly string[] StartingReplies = { "Overdue fees", "Today's lessons", "Open tasks", "Attendance this week" };

    public static AssistantReply Reply(SchoolData data, string question)
    {
        var text = question.Trim();
        var lower = text.ToLowerInvariant();
        var currency = data.Settings.Currency;
        var student = SchoolAssistant.FindStudent(data, text);
        if (student != null) return StudentReply(data, student);

        if (lower.Contains("overdue") || lower.Contains("fee") || lower.Contains("owe") || lower.Contains("outstanding"))
        {
            var overdue = data.Invoices.Where(invoice => invoice.Status == "Overdue").OrderByDescending(invoice => invoice.Balance).ToList();
            var builder = new StringBuilder($"**{overdue.Count} overdue invoices** totalling {overdue.Sum(invoice => invoice.Balance):N2} {currency}; {data.Invoices.Sum(invoice => invoice.Balance):N2} {currency} is outstanding overall.");
            foreach (var invoice in overdue.Take(3))
                builder.Append($"\n- {data.Students.First(item => item.Id == invoice.StudentId).FullName}: {invoice.Balance:N2} {currency} ({invoice.Number})");
            builder.Append("\n\n[Open Finance](page:Finance)");
            return new AssistantReply(builder.ToString(), new[] { "Open tasks", "Today's lessons" });
        }

        if (lower.Contains("lesson") || lower.Contains("today") || lower.Contains("timetable") || lower.Contains("schedule") || lower.Contains("room"))
        {
            var lessons = data.Lessons.Where(item => item.StartsAt.Date == DateTime.Today).OrderBy(item => item.StartsAt).ToList();
            if (lessons.Count == 0)
            {
                var next = data.Lessons.Where(item => item.StartsAt > DateTime.Now).OrderBy(item => item.StartsAt).Take(3).ToList();
                return new AssistantReply("No lessons are scheduled today." + (next.Count > 0 ? "\n\nComing up:\n" + string.Join("\n", next.Select(item => $"- {item.StartsAt:ddd HH:mm} {Course(data, item.CourseId)} in {item.Room}")) : string.Empty) + "\n\n[Open Timetable](page:Timetable)", StartingReplies);
            }

            return new AssistantReply($"**{lessons.Count} lessons today**\n" + string.Join("\n", lessons.Select(item => $"- {item.StartsAt:HH:mm}–{item.EndsAt:HH:mm} {Course(data, item.CourseId)} in {item.Room}")) + "\n\n[Open Timetable](page:Timetable)", new[] { "Attendance this week", "Open tasks" });
        }

        if (lower.Contains("task") || lower.Contains("todo") || lower.Contains("work"))
        {
            var open = data.Tasks.Where(item => item.Status != "Done").OrderBy(item => item.DueOn).ToList();
            var late = open.Count(item => item.DueOn.Date < DateTime.Today);
            return new AssistantReply($"**{open.Count} open tasks**, {late} past due.\n" + string.Join("\n", open.Take(4).Select(item => $"- {item.Title} — due {item.DueOn:dd MMM} ({item.Priority})")) + "\n\n[Open Tasks](page:Tasks)", new[] { "Overdue fees", "Today's lessons" });
        }

        if (lower.Contains("attend") || lower.Contains("absent") || lower.Contains("present"))
        {
            var week = data.Attendance.Where(item => item.Date.Date > DateTime.Today.AddDays(-7)).ToList();
            if (week.Count == 0) return new AssistantReply("No attendance has been recorded this week.\n\n[Open Attendance](page:Attendance)", StartingReplies);
            var worst = week.GroupBy(item => item.CourseId).Select(group => (Course: Course(data, group.Key), Rate: 100.0 * group.Count(item => item.Status != "Absent") / group.Count())).OrderBy(item => item.Rate).Take(2);
            return new AssistantReply($"**{100.0 * week.Count(item => item.Status != "Absent") / week.Count:0}% attendance** this week over {week.Count} marks.\nLowest: " + string.Join(", ", worst.Select(item => $"{item.Course} ({item.Rate:0}%)")) + "\n\n[Open Attendance](page:Attendance)", new[] { "Overdue fees", "Open tasks" });
        }

        if (lower.Contains("course") || lower.Contains("seat") || lower.Contains("capacity") || lower.Contains("rating"))
        {
            var rows = data.Courses.Where(item => item.Active).Select(course => (course, enrolled: data.Enrollments.Count(item => item.CourseId == course.Id && item.Status == "Enrolled"))).OrderByDescending(item => item.course.Capacity - item.enrolled).Take(3);
            return new AssistantReply("**Most open seats**\n" + string.Join("\n", rows.Select(item => $"- {item.course.Code} {item.course.Title}: {item.course.Capacity - item.enrolled} free of {item.course.Capacity}")) + "\n\n[Open Courses](page:Courses)", StartingReplies);
        }

        return new AssistantReply("I can answer questions about fees, lessons, tasks, attendance, courses and individual students (use a student name or code such as ST-0007).", StartingReplies);
    }

    private static string Course(SchoolData data, int id) => data.Courses.FirstOrDefault(item => item.Id == id)?.Title ?? "Course " + id;

    private static AssistantReply StudentReply(SchoolData data, StudentRecord student)
    {
        var enrollments = data.Enrollments.Where(item => item.StudentId == student.Id).ToList();
        var balance = data.Invoices.Where(item => item.StudentId == student.Id).Sum(item => item.Balance);
        var marks = data.Attendance.Where(item => item.StudentId == student.Id).ToList();
        return new AssistantReply($"**{student.FullName}** ({student.StudentCode}) — {student.Department}, year {student.Year}{(student.Active ? string.Empty : ", archived")}\n"
            + string.Join("\n", enrollments.Select(item => $"- {Course(data, item.CourseId)}: {item.Status}, {item.Grade:0}%"))
            + (marks.Count > 0 ? $"\nAttendance {100.0 * marks.Count(item => item.Status != "Absent") / marks.Count:0}%" : string.Empty)
            + $"\nOutstanding {balance:N2} {data.Settings.Currency}\n\n[Open Students](page:Students)", new[] { "Course seats", "Overdue fees" });
    }
}
