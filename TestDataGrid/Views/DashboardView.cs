using EnterpriseUI.Charts;
using EnterpriseUI.WinForms;
using TestDataGrid.Services;

namespace TestDataGrid.Views;

public sealed class DashboardView : UserControl
{
    private readonly SchoolStore _store;
    public DashboardView(SchoolStore store)
    {
        _store = store; Dock = DockStyle.Fill; Font = SchoolUi.BodyFont;
        Build(); store.Changed += Changed;
    }
    private void Changed(object? sender, EventArgs e) => Build();
    private void Build()
    {
        foreach (Control control in Controls.Cast<Control>().ToArray()) control.Dispose();
        var data = _store.Data;
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), RowCount = 3, ColumnCount = 2 };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56)); body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 120)); body.RowStyles.Add(new RowStyle(SizeType.Percent, 55)); body.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        var metrics = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1 };
        foreach (var metric in new[] { ("ACTIVE STUDENTS", data.Students.Count(item => item.Active).ToString("N0")), ("OPEN TASKS", data.Tasks.Count(item => item.Status != "Done").ToString("N0")), ("TODAY'S LESSONS", data.Lessons.Count(item => item.StartsAt.Date == DateTime.Today).ToString()), ("OUTSTANDING", data.Invoices.Sum(item => item.Balance).ToString("N0") + " " + data.Settings.Currency) })
        {
            metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            var panel = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 14, 0) };
            panel.Controls.Add(new Label { Text = metric.Item2, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 25f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft });
            panel.Controls.Add(new Label { Text = metric.Item1, Dock = DockStyle.Top, Height = 30 }); metrics.Controls.Add(panel);
        }
        body.Controls.Add(metrics, 0, 0); body.SetColumnSpan(metrics, 2);
        var chart = new EnterpriseChart { Title = "Students by department" };
        chart.Series.Add(new ChartSeries { Name = "Students", DataPoints = data.Students.GroupBy(item => item.Department).Select(group => new ChartDataPoint { Category = group.Key, Value = group.Count() }).ToList() });
        body.Controls.Add(new SchoolChart { Model = chart, Dock = DockStyle.Fill }, 0, 1);
        var collection = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18) };
        var invoices = data.Invoices.Sum(item => item.Amount);
        var paid = data.Invoices.Sum(item => item.Paid);
        collection.Controls.Add(new EnterpriseBulletGraphControl { Title = "Tuition collected (%)", FeaturedMeasure = invoices == 0 ? 0 : (double)(paid / invoices * 100), ComparativeMeasure = 90, Minimum = 0, Maximum = 100, Dock = DockStyle.Top, Height = 100 });
        collection.Controls.Add(new Label { Text = $"Collected {paid:N2} {data.Settings.Currency}\r\nInvoiced {invoices:N2} {data.Settings.Currency}\r\n\r\n{data.Invoices.Count(item => item.Status == "Overdue")} overdue invoices", Dock = DockStyle.Bottom, Height = 120, Font = new Font("Segoe UI", 12f) });
        body.Controls.Add(collection, 1, 1);
        var tasks = new EnterpriseListViewControl { Dock = DockStyle.Fill, SelectionMode = ListSelectionMode.One, AccessibleName = "Upcoming work", Margin = new Padding(0, 0, 14, 0) };
        tasks.AddColumn("Upcoming work", 280); tasks.AddColumn("Due", 100); tasks.AddColumn("Priority", 100);
        foreach (var task in data.Tasks.Where(item => item.Status != "Done").OrderBy(item => item.DueOn).Take(7)) tasks.Items.Add(task.Title, task.DueOn.ToString("dd MMM"), task.Priority);
        body.Controls.Add(tasks, 0, 2);
        var activity = new EnterpriseListViewControl { Dock = DockStyle.Fill, SelectionMode = ListSelectionMode.One, AccessibleName = "Recent activity" };
        activity.AddColumn("Recent activity", 280); activity.AddColumn("Time", 150);
        foreach (var entry in data.Audit.OrderByDescending(item => item.At).Take(8)) activity.Items.Add(entry.Action + " · " + entry.Entity, entry.At.ToLocalTime().ToString("g"));
        body.Controls.Add(activity, 1, 2);
        Controls.Add(body); Controls.Add(SchoolUi.Heading("School Overview")); SchoolUi.Apply(this);
    }
    protected override void Dispose(bool disposing) { if (disposing) _store.Changed -= Changed; base.Dispose(disposing); }
}

public sealed class SchoolChart : Control
{
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public EnterpriseChart Model { get; set; } = new();
    public SchoolChart() { DoubleBuffered = true; }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); e.Graphics.Clear(SchoolUi.Surface);
        using var text = new SolidBrush(SchoolUi.Text);
        using var bar = new SolidBrush(SchoolUi.Accent);
        using var title = new Font("Segoe UI", 12f, FontStyle.Bold);
        e.Graphics.DrawString(Model.Title, title, text, 18, 12);
        var points = Model.Series.FirstOrDefault()?.DataPoints;
        if (points == null || points.Count == 0) return;
        var max = Math.Max(1, points.Max(point => point.Value));
        var available = Math.Max(100, Width - 180);
        for (var index = 0; index < points.Count; index++)
        {
            var y = 55 + index * 44;
            e.Graphics.DrawString(Convert.ToString(points[index].Category), Font, text, 18, y + 6);
            e.Graphics.FillRectangle(bar, 120, y, (float)(points[index].Value / max * available), 28);
            e.Graphics.DrawString(points[index].Value.ToString("N0"), Font, text, Width - 44, y + 6);
        }
    }
}