using System.Text.Json;
using TestDataGrid.Models;

namespace TestDataGrid.Services;

public sealed class SchoolStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, MaxDepth = 32 };
    public string FilePath { get; }
    public SchoolData Data { get; private set; }
    public event EventHandler? Changed;

    public SchoolStore(string? filePath = null)
    {
        FilePath = filePath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NorthstarSchool", "school.json");
        Data = File.Exists(FilePath) ? Parse(File.ReadAllText(FilePath)) : Seed();
        SchoolOperations.ValidateSnapshot(Data);
        if (!File.Exists(FilePath)) Save(Data);
    }

    public void Transaction(Action<SchoolData> change)
    {
        var candidate = Clone(Data);
        change(candidate);
        SchoolOperations.ValidateSnapshot(candidate);
        Save(candidate);
        Data = candidate;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Backup(string destination) => File.WriteAllText(destination, JsonSerializer.Serialize(Data, JsonOptions));

    public void Restore(string source)
    {
        var candidate = Parse(File.ReadAllText(source));
        SchoolOperations.ValidateSnapshot(candidate);
        Save(candidate);
        Data = candidate;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, JsonOptions), JsonOptions)!;

    private static SchoolData Parse(string json)
    {
        if (json.Length > 20000000) throw new InvalidDataException("School data file is too large.");
        var data = JsonSerializer.Deserialize<SchoolData>(json, JsonOptions) ?? throw new InvalidDataException("School data is empty.");
        if (data.SchemaVersion != 1) throw new InvalidDataException("Unsupported school data schema.");
        return data;
    }

    private void Save(SchoolData data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(FilePath))!);
        var temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(data, JsonOptions));
            File.Move(temporary, FilePath, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static SchoolData Seed()
    {
        var data = new SchoolData();
        var departments = new[] { "Science", "Business", "Engineering", "Arts" };
        var firstNames = new[] { "Amelia", "Noah", "Sofia", "Oliver", "Aisha", "Lucas", "Maya", "Ethan", "Isla", "Leo" };
        var lastNames = new[] { "Anderson", "Khan", "Martin", "Wilson", "Patel", "Garcia", "Chen", "Taylor" };
        for (var index = 1; index <= 12; index++)
            data.Staff.Add(new StaffRecord { Id = index, FullName = $"{firstNames[(index + 3) % firstNames.Length]} {lastNames[index % lastNames.Length]}", Email = $"teacher{index}@northstar.example", Department = departments[index % 4], Position = index > 8 ? "Coordinator" : "Teacher" });
        var titles = new[] { "Applied Mathematics", "Introduction to Physics", "Software Engineering", "Business Strategy", "Visual Communication", "Environmental Science", "Digital Systems", "Creative Writing" };
        for (var index = 1; index <= titles.Length; index++)
            data.Courses.Add(new CourseRecord { Id = index, Code = $"NS-{100 + index}", Title = titles[index - 1], Department = departments[index % 4], TeacherId = index, Credits = 3 + index % 3, Capacity = 60 });
        for (var index = 1; index <= 160; index++)
        {
            data.Students.Add(new StudentRecord { Id = index, StudentCode = $"ST-{index:D4}", FullName = $"{firstNames[index % 10]} {lastNames[index % 8]}", Email = $"student{index}@northstar.example", Department = departments[index % 4], Year = index % 4 + 1, EnrolledOn = DateTime.Today.AddDays(-60 - index), Active = index % 17 != 0 });
            for (var offset = 0; offset < 2; offset++)
            {
                var course = (index + offset * 3) % 8 + 1;
                data.Enrollments.Add(new EnrollmentRecord { Id = data.Enrollments.Count + 1, StudentId = index, CourseId = course, EnrolledOn = DateTime.Today.AddDays(-45), Grade = 60 + index % 41, Feedback = (index + offset) % 6 == 0 ? 0 : 3m + (index + offset) % 5 * 0.5m });
                for (var day = 0; day < 7; day++)
                    data.Attendance.Add(new AttendanceRecord { Id = data.Attendance.Count + 1, StudentId = index, CourseId = course, Date = DateTime.Today.AddDays(-day), Status = (index + day) % 11 == 0 ? "Absent" : (index + day) % 13 == 0 ? "Late" : "Present" });
            }
            var amount = 850m + index % 4 * 150m;
            var paid = index % 3 == 0 ? amount : index % 3 == 1 ? amount / 2 : 0;
            data.Invoices.Add(new InvoiceRecord { Id = index, Number = $"INV-2026-{index:D4}", StudentId = index, Amount = amount, Paid = paid, DueOn = DateTime.Today.AddDays(index % 40 - 15) });
            if (paid > 0) data.Payments.Add(new PaymentRecord { Id = data.Payments.Count + 1, InvoiceId = index, Amount = paid, PaidAt = DateTime.Today.AddDays(-10), Reference = $"SEED-{index:D4}" });
        }
        for (var index = 1; index <= 24; index++)
            data.Tasks.Add(new TaskRecord { Id = index, Title = new[] { "Review assessment papers", "Prepare laboratory session", "Contact guardians", "Update course materials", "Plan student orientation", "Publish term report" }[index % 6], OwnerId = index % 12 + 1, DueOn = DateTime.Today.AddDays(index % 20 - 4), Priority = index % 5 == 0 ? "High" : "Normal", Status = new[] { "To do", "In progress", "Done" }[index % 3], Progress = index % 3 == 2 ? 100 : index % 3 == 1 ? 50 : 0 });
        for (var index = 1; index <= 12; index++)
        {
            var start = DateTime.Today.AddDays(index / 4).AddHours(9 + index % 4 * 2);
            data.Lessons.Add(new LessonRecord { Id = index, CourseId = index % 8 + 1, StartsAt = start, EndsAt = start.AddHours(1), Room = $"{(char)('A' + index % 3)}{100 + index % 5}" });
        }
        data.Audit.Add(new AuditEntry { Action = "Initialize", Entity = "School", Detail = "Starter school records created." });
        return data;
    }
}