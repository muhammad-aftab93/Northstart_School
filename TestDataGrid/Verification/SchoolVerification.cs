using EnterpriseUI.Exporting;
using TestDataGrid.Dialogs;
using TestDataGrid.Models;
using TestDataGrid.Services;
using TestDataGrid.Views;

namespace TestDataGrid.Verification;

internal static class SchoolVerification
{
    internal static int Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), "NorthstarSchool-Verification");
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, "school-" + Guid.NewGuid().ToString("N") + ".json");
        using var log = new StreamWriter(Path.Combine(directory, "verification.txt")) { AutoFlush = true };
        var checks = 0;
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            log.WriteLine("PASS: " + message); checks++;
        }
        try
        {
            var store = new SchoolStore(file);
            Check(store.Data.Students.Count == 160 && store.Data.Enrollments.Count == 320, "Coherent starter student/enrollment records");
            Check(new SchoolStore(file).Data.Students.Count == 160, "Persistent data reloads");
            var student = new StudentRecord { StudentCode = "ST-VERIFY", FullName = "Verified Student", Email = "verified@example.com", Department = "Science" };
            SchoolOperations.SaveRecord(store, student);
            Check(store.Data.Students.Count == 161, "Student creation persists");
            var added = store.Data.Students.Single(item => item.StudentCode == "ST-VERIFY");
            var draft = SchoolStore.Clone(added); draft.FullName = "Edited Student"; SchoolOperations.SaveRecord(store, draft);
            Check(new SchoolStore(file).Data.Students.Single(item => item.Id == added.Id).FullName == "Edited Student", "Student update survives restart");
            var before = File.ReadAllText(file);
            try { SchoolOperations.SaveRecord(store, new StudentRecord { StudentCode = "ST-VERIFY", FullName = "Duplicate", Email = "duplicate@example.com" }); Check(false, "Duplicate student codes reject"); }
            catch (InvalidOperationException) { Check(File.ReadAllText(file) == before && store.Data.Students.Count == 161, "Rejected changes leave file and memory unchanged"); }
            try { SchoolOperations.DeleteRecord(store, store.Data.Students[0]); Check(false, "Referenced student protection"); }
            catch (InvalidOperationException) { Check(true, "Students with related records cannot be hard-deleted"); }
            var invoice = store.Data.Invoices.First(item => item.Balance > 0);
            var balance = invoice.Balance;
            SchoolOperations.RecordPayment(store, invoice.Id, 10m, "VERIFICATION");
            Check(store.Data.Invoices.Single(item => item.Id == invoice.Id).Balance == balance - 10m, "Payments update invoice balance and ledger");
            before = File.ReadAllText(file);
            try { SchoolOperations.RecordPayment(store, invoice.Id, 100000m, "OVERPAYMENT"); Check(false, "Overpayments reject"); }
            catch (InvalidOperationException) { Check(File.ReadAllText(file) == before, "Overpayment cannot alter persisted data"); }
            var enrollment = SchoolStore.Clone(store.Data.Enrollments[0]); enrollment.Id = 0;
            try { SchoolOperations.SaveRecord(store, enrollment); Check(false, "Duplicate enrollment rejects"); }
            catch (InvalidOperationException) { Check(true, "Duplicate enrollments reject"); }
            var attendance = SchoolStore.Clone(store.Data.Attendance[0]); attendance.Id = 0;
            try { SchoolOperations.SaveRecord(store, attendance); Check(false, "Duplicate attendance rejects"); }
            catch (InvalidOperationException) { Check(true, "Duplicate daily attendance rejects"); }
            var lesson = SchoolStore.Clone(store.Data.Lessons[0]); lesson.Id = 0;
            try { SchoolOperations.SaveRecord(store, lesson); Check(false, "Room/teacher conflicts reject"); }
            catch (InvalidOperationException) { Check(true, "Room and teacher booking conflicts reject"); }
            var backup = Path.Combine(directory, "backup.json"); store.Backup(backup);
            SchoolOperations.DeleteRecord(store, store.Data.Students.Single(item => item.Id == added.Id));
            store.Restore(backup);
            Check(store.Data.Students.Count == 161, "Backup and restore recover complete records");
            var invalidBackup = Path.Combine(directory, "invalid.json"); File.WriteAllText(invalidBackup, "{\"SchemaVersion\":99}");
            before = File.ReadAllText(file);
            try { store.Restore(invalidBackup); Check(false, "Invalid backups reject"); }
            catch (InvalidDataException) { Check(File.ReadAllText(file) == before, "Invalid backup cannot replace saved data"); }
            store.Transaction(data => data.Settings.SchoolName = "Northstar Verified");
            Check(new SchoolStore(file).Data.Settings.SchoolName == "Northstar Verified", "School settings persist");
            var attendanceCount = store.Data.Attendance.Count;
            SchoolOperations.MarkAttendance(store, DateTime.Today.AddDays(-10));
            Check(store.Data.Attendance.Count > attendanceCount, "Roll call creates missing active-student attendance");
            attendanceCount = store.Data.Attendance.Count;
            SchoolOperations.MarkAttendance(store, DateTime.Today.AddDays(-10));
            Check(store.Data.Attendance.Count == attendanceCount, "Repeated roll call does not duplicate attendance");

            foreach (var kind in new[] { "Outstanding Fees", "Attendance Summary", "Course Results", "Activity Log" })
            {
                var document = SchoolReports.Build(store.Data, kind);
                Check(document.Sheets.Count == 1 && document.Sheets[0].Rows.Any(), kind + " report uses live school data");
                var path = Path.Combine(directory, kind.Replace(' ', '-') + ".pdf");
                ExportService.Default.ExportToFileAsync(document, "pdf", path).GetAwaiter().GetResult();
                Check(new FileInfo(path).Length > 0, kind + " PDF report exports");
            }

            using var main = new MainForm(store);
            main.Shown += async (_, _) =>
            {
                try
                {
                    foreach (var page in main.PageNames)
                    {
                        main.Navigate(page); main.PerformLayout(); await Task.Delay(40);
                        using var image = new Bitmap(main.Width, main.Height);
                        main.DrawToBitmap(image, new Rectangle(Point.Empty, main.Size));
                        image.Save(Path.Combine(directory, page.Replace(' ', '-') + ".png"), System.Drawing.Imaging.ImageFormat.Png);
                        Check(main.CurrentView.Width > 500 && main.CurrentView.Height > 300, page + " screen opens and renders");
                    }
                    using var dialog = new SchoolRecordDialog<StudentRecord>(store, store.Data.Students[0], "Edit student");
                    dialog.Show(main); await Task.Delay(30);
                    FindControls(dialog).OfType<TextBox>().Single(input => input.AccessibleName == "Full name").Text = "Cancelled name";
                    dialog.CancelButton!.PerformClick(); dialog.Close();
                    Check(store.Data.Students[0].FullName != "Cancelled name", "Cancel leaves the stored student unchanged");
                    using (var edit = new SchoolRecordDialog<StudentRecord>(store, store.Data.Students[0], "Edit student"))
                    {
                        edit.Show(main); await Task.Delay(30);
                        FindControls(edit).OfType<TextBox>().Single(input => input.AccessibleName == "Full name").Text = "Saved through modal";
                        Check(edit.SaveDraft(), "Custom modal saves valid records");
                        edit.Close();
                        Check(new SchoolStore(file).Data.Students[0].FullName == "Saved through modal", "Modal edits persist to disk");
                    }
                    store.Transaction(data => { data.Tasks[0].Status = "Done"; data.Tasks[0].Progress = 100; });
                    Check(new SchoolStore(file).Data.Tasks[0].Status == "Done", "Task status transitions persist");
                    var currentTheme = EnterpriseUI.Theming.ThemeManager.Instance.CurrentTheme;
                    foreach (var theme in new[] { "Fluent-Dark", "HighContrast-Black", "Fluent-Light" })
                    {
                        EnterpriseUI.Theming.ThemeManager.Instance.ApplyTheme(theme); main.Navigate("Students"); await Task.Delay(30);
                        Check(main.CurrentView.Width > 500, theme + " management screen updates");
                    }
                    EnterpriseUI.Theming.ThemeManager.Instance.ApplyTheme(currentTheme);
                    main.Navigate("Tasks");
                    var taskToolbar = FindControls(main.CurrentView).OfType<ToolStrip>().Single();
                    taskToolbar.Items.Cast<ToolStripItem>().Single(item => item.Text == "Board / List").PerformClick();
                    await Task.Delay(30);
                    Check(!((TasksView)main.CurrentView).Grid.Visible, "Board view hides the list grid");
                    Check(FindControls(main.CurrentView).OfType<Button>().Any(button => button.Text.Contains("Prepare laboratory", StringComparison.Ordinal)), "Task board displays persisted task cards");
                    using (var image = new Bitmap(main.Width, main.Height)) { main.DrawToBitmap(image, new Rectangle(Point.Empty, main.Size)); image.Save(Path.Combine(directory, "Task-Board.png"), System.Drawing.Imaging.ImageFormat.Png); }
                    main.Navigate("Attendance");
                    var attendanceToolbar = FindControls(main.CurrentView).OfType<ToolStrip>().Single();
                    Check(attendanceToolbar.Items.Cast<ToolStripItem>().Any(item => item.Text == "Mark Today Present"), "Attendance roll-call command is available");
                    main.ClientSize = new Size(1050, 650); main.Navigate("Students"); main.PerformLayout(); await Task.Delay(30);
                    Check(main.CurrentView.Width > 500 && main.CurrentView.Height > 300, "Compact desktop layout remains usable");
                    log.WriteLine($"Verified {checks} school checks.");
                }
                catch (Exception exception) { log.WriteLine("FAIL: " + exception); Environment.ExitCode = 1; }
                finally { main.Close(); }
            };
            Application.Run(main);
            return Environment.ExitCode;
        }
        catch (Exception exception) { log.WriteLine("FAIL: " + exception); return 1; }
    }

    private static IEnumerable<Control> FindControls(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var descendant in FindControls(child)) yield return descendant;
        }
    }
}