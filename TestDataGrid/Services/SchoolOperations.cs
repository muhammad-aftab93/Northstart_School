using System.Net.Mail;
using TestDataGrid.Models;

namespace TestDataGrid.Services;

public static class SchoolOperations
{
    public static void MarkAttendance(SchoolStore store, DateTime date)
    {
        store.Transaction(data =>
        {
            var next = NextId<AttendanceRecord>(data);
            foreach (var enrollment in data.Enrollments.Where(item => item.Status == "Enrolled" && data.Students.Any(student => student.Id == item.StudentId && student.Active)))
                if (!data.Attendance.Any(item => item.StudentId == enrollment.StudentId && item.CourseId == enrollment.CourseId && item.Date.Date == date.Date))
                    data.Attendance.Add(new AttendanceRecord { Id = next++, StudentId = enrollment.StudentId, CourseId = enrollment.CourseId, Date = date.Date });
            data.Audit.Add(new AuditEntry { Action = "Roll call", Entity = "Attendance", Detail = date.ToShortDateString() });
        });
    }

    public static int NextId<T>(SchoolData data) where T : SchoolRecord => data.Records<T>().Select(record => record.Id).DefaultIfEmpty().Max() + 1;

    public static void SaveRecord<T>(SchoolStore store, T record) where T : SchoolRecord
    {
        store.Transaction(data =>
        {
            var records = data.Records<T>();
            var existing = records.FindIndex(item => item.Id == record.Id);
            var candidate = SchoolStore.Clone(record);
            if (candidate.Id == 0) candidate.Id = NextId<T>(data);
            if (existing < 0) records.Add(candidate); else records[existing] = candidate;
            data.Audit.Add(new AuditEntry { Action = existing < 0 ? "Create" : "Update", Entity = typeof(T).Name, RecordId = candidate.Id });
        });
    }

    public static void DeleteRecord<T>(SchoolStore store, T record) where T : SchoolRecord
    {
        store.Transaction(data =>
        {
            if (record is StudentRecord && (data.Enrollments.Any(item => item.StudentId == record.Id) || data.Invoices.Any(item => item.StudentId == record.Id)))
                throw new InvalidOperationException("This student has enrollment or financial records. Archive the student instead.");
            if (record is StaffRecord && (data.Courses.Any(item => item.TeacherId == record.Id) || data.Tasks.Any(item => item.OwnerId == record.Id)))
                throw new InvalidOperationException("Reassign courses and tasks before deleting this staff member.");
            if (record is CourseRecord && (data.Enrollments.Any(item => item.CourseId == record.Id) || data.Lessons.Any(item => item.CourseId == record.Id)))
                throw new InvalidOperationException("This course has enrollments or lessons. Deactivate it instead.");
            if (record is EnrollmentRecord enrollment && data.Attendance.Any(item => item.StudentId == enrollment.StudentId && item.CourseId == enrollment.CourseId))
                throw new InvalidOperationException("Attendance is recorded for this enrollment. Change its status instead.");
            if (record is InvoiceRecord invoice && invoice.Paid > 0) throw new InvalidOperationException("Paid invoices cannot be deleted.");
            data.Records<T>().RemoveAll(item => item.Id == record.Id);
            data.Audit.Add(new AuditEntry { Action = "Delete", Entity = typeof(T).Name, RecordId = record.Id });
        });
    }

    public static void RecordPayment(SchoolStore store, int invoiceId, decimal amount, string reference)
    {
        store.Transaction(data =>
        {
            var invoice = data.Invoices.Single(item => item.Id == invoiceId);
            if (amount <= 0 || amount > invoice.Balance || string.IsNullOrWhiteSpace(reference)) throw new InvalidOperationException("Payment must be positive, within the outstanding balance, and have a reference.");
            invoice.Paid += amount;
            data.Payments.Add(new PaymentRecord { Id = data.Payments.Select(item => item.Id).DefaultIfEmpty().Max() + 1, InvoiceId = invoiceId, Amount = amount, Reference = reference.Trim() });
            data.Audit.Add(new AuditEntry { Action = "Payment", Entity = nameof(InvoiceRecord), RecordId = invoiceId, Detail = $"{amount:N2} / {reference}" });
        });
    }

    public static void ValidateSnapshot(SchoolData data)
    {
        if (data.SchemaVersion != 1) throw new InvalidDataException("Unsupported data schema.");
        void Unique<T>(IEnumerable<T> records, string label) where T : SchoolRecord
        {
            var items = records.ToArray();
            if (items.Any(item => item.Id <= 0) || items.Select(item => item.Id).Distinct().Count() != items.Length)
                throw new InvalidOperationException($"{label} IDs must be positive and unique.");
        }
        Unique(data.Students, "Student"); Unique(data.Staff, "Staff"); Unique(data.Courses, "Course"); Unique(data.Enrollments, "Enrollment");
        Unique(data.Attendance, "Attendance"); Unique(data.Tasks, "Task"); Unique(data.Lessons, "Lesson"); Unique(data.Invoices, "Invoice"); Unique(data.Payments, "Payment");
        if (string.IsNullOrWhiteSpace(data.Settings.SchoolName) || data.Settings.PageSize is < 5 or > 500) throw new InvalidOperationException("School name and a page size between 5 and 500 are required.");
        foreach (var student in data.Students)
            if (string.IsNullOrWhiteSpace(student.FullName) || string.IsNullOrWhiteSpace(student.StudentCode) || !MailAddress.TryCreate(student.Email, out _) || student.Year is < 1 or > 6)
                throw new InvalidOperationException("Students need a code, name, valid email, and year from 1 to 6.");
        if (data.Students.GroupBy(item => item.StudentCode, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1)) throw new InvalidOperationException("Student codes must be unique.");
        foreach (var staff in data.Staff)
            if (string.IsNullOrWhiteSpace(staff.FullName) || !MailAddress.TryCreate(staff.Email, out _)) throw new InvalidOperationException("Staff need a name and valid email.");
        foreach (var course in data.Courses)
            if (string.IsNullOrWhiteSpace(course.Code) || string.IsNullOrWhiteSpace(course.Title) || course.Capacity < 1 || course.Credits is < 1 or > 30 || !data.Staff.Any(item => item.Id == course.TeacherId))
                throw new InvalidOperationException("Courses need a code, title, teacher, positive capacity and 1-30 credits.");
        if (data.Courses.GroupBy(item => item.Code, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1)) throw new InvalidOperationException("Course codes must be unique.");
        foreach (var enrollment in data.Enrollments)
            if (!data.Students.Any(item => item.Id == enrollment.StudentId) || !data.Courses.Any(item => item.Id == enrollment.CourseId) || enrollment.Grade is < 0 or > 100 || enrollment.Status is not ("Enrolled" or "Completed" or "Withdrawn"))
                throw new InvalidOperationException("Enrollments need valid student/course references and grades from 0 to 100.");
        if (data.Enrollments.GroupBy(item => (item.StudentId, item.CourseId)).Any(group => group.Count() > 1)) throw new InvalidOperationException("A student can enroll in a course only once.");
        foreach (var course in data.Courses)
            if (data.Enrollments.Count(item => item.CourseId == course.Id && item.Status == "Enrolled") > course.Capacity) throw new InvalidOperationException($"{course.Code} has reached its capacity.");
        foreach (var attendance in data.Attendance)
            if (!data.Enrollments.Any(item => item.StudentId == attendance.StudentId && item.CourseId == attendance.CourseId) || attendance.Date.Date > DateTime.Today || attendance.Status is not ("Present" or "Absent" or "Late" or "Excused"))
                throw new InvalidOperationException("Attendance must reference an enrollment and cannot be in the future.");
        if (data.Attendance.GroupBy(item => (item.StudentId, item.CourseId, item.Date.Date)).Any(group => group.Count() > 1)) throw new InvalidOperationException("Attendance is already recorded for this student, course and date.");
        foreach (var task in data.Tasks)
            if (string.IsNullOrWhiteSpace(task.Title) || !data.Staff.Any(item => item.Id == task.OwnerId) || task.Progress is < 0 or > 100
                || task.Status is not ("To do" or "In progress" or "Done") || (task.Status == "Done" && task.Progress != 100))
                throw new InvalidOperationException("Tasks need a title, staff owner and progress from 0 to 100.");
        foreach (var lesson in data.Lessons)
        {
            if (!data.Courses.Any(item => item.Id == lesson.CourseId) || lesson.EndsAt <= lesson.StartsAt || string.IsNullOrWhiteSpace(lesson.Room)) throw new InvalidOperationException("Lessons need a course, room and valid start/end times.");
            var teacher = data.Courses.Single(item => item.Id == lesson.CourseId).TeacherId;
            if (data.Lessons.Any(other => other.Id != lesson.Id && other.StartsAt < lesson.EndsAt && other.EndsAt > lesson.StartsAt
                && (other.Room.Equals(lesson.Room, StringComparison.OrdinalIgnoreCase) || data.Courses.Single(item => item.Id == other.CourseId).TeacherId == teacher)))
                throw new InvalidOperationException("Lesson overlaps another booking for this room or teacher.");
        }
        foreach (var invoice in data.Invoices)
            if (!data.Students.Any(item => item.Id == invoice.StudentId) || string.IsNullOrWhiteSpace(invoice.Number) || invoice.Amount <= 0 || invoice.Paid < 0 || invoice.Paid > invoice.Amount)
                throw new InvalidOperationException("Invoices need a student, number, positive amount and valid payment balance.");
        if (data.Invoices.GroupBy(item => item.Number, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1)) throw new InvalidOperationException("Invoice numbers must be unique.");
        foreach (var payment in data.Payments)
            if (payment.Amount <= 0 || !data.Invoices.Any(item => item.Id == payment.InvoiceId)) throw new InvalidOperationException("Invalid payment reference or amount.");
        foreach (var invoice in data.Invoices)
            if (data.Payments.Where(item => item.InvoiceId == invoice.Id).Sum(item => item.Amount) != invoice.Paid) throw new InvalidOperationException("Invoice payments do not match the payment ledger.");
    }
}