using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace TestDataGrid.Models;

public abstract class SchoolRecord
{
    public int Id { get; set; }
}

public sealed class StudentRecord : SchoolRecord
{
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Department { get; set; } = "Science";
    public int Year { get; set; } = 1;
    public DateTime EnrolledOn { get; set; } = DateTime.Today;
    public bool Active { get; set; } = true;
}

public sealed class StaffRecord : SchoolRecord
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Department { get; set; } = "Science";
    public string Position { get; set; } = "Teacher";
    public bool Active { get; set; } = true;
}

public sealed class CourseRecord : SchoolRecord
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Department { get; set; } = "Science";
    public int TeacherId { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string TeacherIdLabel { get; set; } = string.Empty;
    public int Credits { get; set; } = 3;
    public int Capacity { get; set; } = 30;
    public bool Active { get; set; } = true;
}

public sealed class EnrollmentRecord : SchoolRecord
{
    public int StudentId { get; set; }
    public int CourseId { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string StudentIdLabel { get; set; } = string.Empty;
    [System.Text.Json.Serialization.JsonIgnore]
    public string CourseIdLabel { get; set; } = string.Empty;
    public DateTime EnrolledOn { get; set; } = DateTime.Today;
    public string Status { get; set; } = "Enrolled";
    public decimal Grade { get; set; }

    /// <summary>The student's rating of the course from 0 (not rated) to 5 in half steps.</summary>
    public decimal Feedback { get; set; }
}

public sealed class AttendanceRecord : SchoolRecord
{
    public int StudentId { get; set; }
    public int CourseId { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string StudentIdLabel { get; set; } = string.Empty;
    [System.Text.Json.Serialization.JsonIgnore]
    public string CourseIdLabel { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.Today;
    public string Status { get; set; } = "Present";
    public string Notes { get; set; } = string.Empty;
}

public sealed class TaskRecord : SchoolRecord
{
    public string Title { get; set; } = string.Empty;
    public int OwnerId { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string OwnerIdLabel { get; set; } = string.Empty;
    public DateTime DueOn { get; set; } = DateTime.Today.AddDays(7);
    public string Priority { get; set; } = "Normal";
    public string Status { get; set; } = "To do";
    public int Progress { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public sealed class LessonRecord : SchoolRecord
{
    public int CourseId { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string CourseIdLabel { get; set; } = string.Empty;
    public DateTime StartsAt { get; set; } = DateTime.Today.AddHours(9);
    public DateTime EndsAt { get; set; } = DateTime.Today.AddHours(10);
    public string Room { get; set; } = "A101";
    public string Notes { get; set; } = string.Empty;
}

public sealed class InvoiceRecord : SchoolRecord
{
    public string Number { get; set; } = string.Empty;
    public int StudentId { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string StudentIdLabel { get; set; } = string.Empty;
    public string Description { get; set; } = "Term tuition";
    public decimal Amount { get; set; }
    public decimal Paid { get; set; }
    public DateTime DueOn { get; set; } = DateTime.Today.AddDays(30);
    public decimal Balance => Amount - Paid;
    public string Status => Balance <= 0 ? "Paid" : DueOn.Date < DateTime.Today ? "Overdue" : Paid > 0 ? "Part paid" : "Open";
}

public sealed class PaymentRecord : SchoolRecord
{
    public int InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaidAt { get; set; } = DateTime.Now;
    public string Reference { get; set; } = string.Empty;
}

public sealed class AuditEntry
{
    public DateTime At { get; set; } = DateTime.UtcNow;
    public string Action { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
    public int RecordId { get; set; }
    public string Detail { get; set; } = string.Empty;
}

public sealed class SchoolSettings
{
    [Display(Name = "School name", GroupName = "School", Order = 1, Description = "Shown in the title bar, the navigation header and printed reports.")]
    [Required, StringLength(80, MinimumLength = 3)]
    public string SchoolName { get; set; } = "Northstar School";

    [Display(Name = "Academic year", GroupName = "School", Order = 2, Prompt = "2026-2027")]
    [Required, StringLength(20, MinimumLength = 4)]
    public string AcademicYear { get; set; } = "2026-2027";

    [Browsable(false)]
    public string Theme { get; set; } = "Fluent-Light";

    [Display(Name = "Rows per page", GroupName = "Lists", Order = 3, Description = "Page size of every list. Between 5 and 500.")]
    [Range(5, 500)]
    public int PageSize { get; set; } = 25;

    [Display(Name = "Currency code", GroupName = "Lists", Order = 4, Description = "Three letters, for example EUR.")]
    [Required, StringLength(3, MinimumLength = 3)]
    public string Currency { get; set; } = "EUR";

    [Display(Name = "Show page tips", GroupName = "Lists", Order = 5, Description = "Shows a short tip the first time each screen is opened.")]
    public bool ShowTips { get; set; } = true;
}

public sealed class SchoolData
{
    public int SchemaVersion { get; set; } = 1;
    public SchoolSettings Settings { get; set; } = new();
    public List<StudentRecord> Students { get; set; } = new();
    public List<StaffRecord> Staff { get; set; } = new();
    public List<CourseRecord> Courses { get; set; } = new();
    public List<EnrollmentRecord> Enrollments { get; set; } = new();
    public List<AttendanceRecord> Attendance { get; set; } = new();
    public List<TaskRecord> Tasks { get; set; } = new();
    public List<LessonRecord> Lessons { get; set; } = new();
    public List<InvoiceRecord> Invoices { get; set; } = new();
    public List<PaymentRecord> Payments { get; set; } = new();
    public List<AuditEntry> Audit { get; set; } = new();

    public List<T> Records<T>() where T : SchoolRecord => typeof(T) switch
    {
        var type when type == typeof(StudentRecord) => (List<T>)(object)Students,
        var type when type == typeof(StaffRecord) => (List<T>)(object)Staff,
        var type when type == typeof(CourseRecord) => (List<T>)(object)Courses,
        var type when type == typeof(EnrollmentRecord) => (List<T>)(object)Enrollments,
        var type when type == typeof(AttendanceRecord) => (List<T>)(object)Attendance,
        var type when type == typeof(TaskRecord) => (List<T>)(object)Tasks,
        var type when type == typeof(LessonRecord) => (List<T>)(object)Lessons,
        var type when type == typeof(InvoiceRecord) => (List<T>)(object)Invoices,
        _ => throw new NotSupportedException(typeof(T).Name)
    };
}