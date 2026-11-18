using System.Runtime.InteropServices;
using EnterpriseUI.Editors.Interactivity;
using EnterpriseUI.WinForms;
using TestDataGrid.Models;
using TestDataGrid.Services;
using TestDataGrid.Views;

namespace TestDataGrid.Dialogs;

/// <summary>Small floating window with the lesson in progress and the next one; stays on top and can be dragged anywhere.</summary>
public sealed class LessonWidget : EnterpriseShapedForm
{
    private const int WmNcLButtonDown = 0xA1, HtCaption = 2;
    private readonly SchoolStore _store;
    private readonly EnterpriseLabelControl _text = new() { MarkupMode = LabelMarkupMode.Markdown, Location = new Point(22, 16), Size = new Size(300, 86), AccessibleName = "Current and next lesson" };
    private readonly System.Windows.Forms.Timer _clock = new() { Interval = 30000 };

    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam);

    public LessonWidget(SchoolStore store)
    {
        _store = store;
        Shape = EnterpriseFormShape.RoundedRectangle;
        CornerRadius = 26;
        ClientSize = new Size(344, 118);
        TopMost = true;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
        Location = new Point(area.Right - Width - 24, area.Top + 24);
        Font = SchoolUi.BodyFont;
        AccessibleName = "Lesson widget";

        var close = new EnterpriseButtonControl { Text = "\u2715", Size = new Size(28, 28), Variant = EnterpriseButtonVariant.Outline, Location = new Point(ClientSize.Width - 44, 12), AccessibleName = "Close lesson widget" };
        close.Click += (_, _) => Close();
        _text.Width = ClientSize.Width - 70;
        _text.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, WmNcLButtonDown, (IntPtr)HtCaption, IntPtr.Zero); } };
        Controls.Add(_text);
        Controls.Add(close);
        _clock.Tick += (_, _) => Refresh_();
        _clock.Start();
        Refresh_();
    }

    public string Summary => _text.PlainText;

    private void Refresh_() => _text.Text = Describe(_store.Data, DateTime.Now);

    /// <summary>The lesson in progress and the next one at <paramref name="now"/>, as Markdown.</summary>
    public static string Describe(SchoolData data, DateTime now)
    {
        string Label(LessonRecord lesson)
        {
            var course = data.Courses.FirstOrDefault(item => item.Id == lesson.CourseId);
            return $"**{course?.Code ?? "Lesson"}** {course?.Title} \u00b7 room {lesson.Room}";
        }

        var current = data.Lessons.Where(item => item.StartsAt <= now && item.EndsAt > now).OrderBy(item => item.StartsAt).FirstOrDefault();
        var next = data.Lessons.Where(item => item.StartsAt > now).OrderBy(item => item.StartsAt).FirstOrDefault();
        var now1 = current == null ? "No lesson in progress" : $"Now: {Label(current)}, {(int)Math.Ceiling((current.EndsAt - now).TotalMinutes)} min left";
        var next1 = next == null ? "Nothing scheduled next" : $"Next: {Label(next)} at {next.StartsAt:ddd HH:mm}";
        return now1 + "\n" + next1;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _clock.Dispose();
        base.Dispose(disposing);
    }
}
