using EnterpriseUI.Scheduling;
using TestDataGrid.Models;
using TestDataGrid.Services;

namespace TestDataGrid.Views;

public sealed class ScheduleView : RecordWorkspace<LessonRecord>
{
    private readonly EnterpriseScheduler _schedule = new();
    private readonly MonthCalendar _calendar;
    public ScheduleView(SchoolStore store) : base(store, "Timetable")
    {
        _calendar = new MonthCalendar { Dock = DockStyle.Top, MaxSelectionCount = 1, CalendarDimensions = new Size(1, 1) };
        UseFacets(new[] { "Room", "CourseIdLabel" });
        _calendar.DateSelected += (_, e) =>
        {
            _schedule.SelectedDate = e.Start;
            Grid.FilterBy("StartsAt", EnterpriseUI.Data.FilterOperator.IsBetween, e.Start.Date, e.Start.Date.AddDays(1).AddTicks(-1));
        };
        var calendarPanel = new Panel { Dock = DockStyle.Right, Width = 230, Padding = new Padding(8, 10, 8, 10) };
        calendarPanel.Controls.Add(_calendar);
        Controls.Add(calendarPanel); calendarPanel.SendToBack();
        AddCommand("All Lessons", () => Grid.ClearFilters());
        AddCommand("This Week", () => Grid.FilterBy("StartsAt", EnterpriseUI.Data.FilterOperator.IsBetween, DateTime.Today, DateTime.Today.AddDays(7)));
        Store.Changed += Changed;
        SyncSchedule();
    }
    private void Changed(object? sender, EventArgs e) => SyncSchedule();
    private void SyncSchedule()
    {
        _schedule.Appointments.Clear();
        foreach (var lesson in Store.Data.Lessons)
            _schedule.Appointments.Add(new Appointment { Id = lesson.Id.ToString(), Subject = Store.Data.Courses.Single(item => item.Id == lesson.CourseId).Title, Start = lesson.StartsAt, End = lesson.EndsAt, Location = lesson.Room });
        _calendar.BoldedDates = Store.Data.Lessons.Select(item => item.StartsAt.Date).Distinct().ToArray();
    }
    protected override void Dispose(bool disposing) { if (disposing) Store.Changed -= Changed; base.Dispose(disposing); }
}