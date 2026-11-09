using EnterpriseUI.Grids;
using EnterpriseUI.WinForms;
using TestDataGrid.Dialogs;
using TestDataGrid.Models;
using TestDataGrid.Services;

namespace TestDataGrid.Views;

public sealed class StudentsView : RecordWorkspace<StudentRecord>
{
    public StudentsView(SchoolStore store) : base(store, "Students")
    {
        Grid.EnableMasterDetail = true;
        RebuildDetails();
        UseCards("FullName", "Email", "Department", ("StudentCode", "Student code", null), ("Year", "Year", null), ("EnrolledOn", "Enrolled", "d"), ("Active", "Active", null));
        UseFacets(new[] { "Department", "Year", "Active" });
        Store.Changed += DetailsChanged;
        AddCommand("Archive / Activate", () =>
        {
            if (Grid.FocusedRecord is not { } student) return;
            Store.Transaction(data => { var record = data.Students.Single(item => item.Id == student.Id); record.Active = !record.Active; data.Audit.Add(new AuditEntry { Action = "Toggle active", Entity = "Student", RecordId = student.Id }); });
        });
        AddCommand("Student Summary", () =>
        {
            if (Grid.FocusedRecord is not { } student) return;
            using var dialog = new Form { Text = student.FullName, StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(580, 360) };
            var enrollments = Store.Data.Enrollments.Where(item => item.StudentId == student.Id).Select(item => $"{Store.Data.Courses.Single(course => course.Id == item.CourseId).Title}: {item.Status}, {item.Grade:N0}%");
            var balance = Store.Data.Invoices.Where(item => item.StudentId == student.Id).Sum(item => item.Balance);
            dialog.Controls.Add(new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, Text = $"{student.StudentCode}  {student.FullName}\r\n{student.Email}\r\n{student.Department} · Year {student.Year}\r\n\r\n{string.Join("\r\n", enrollments)}\r\n\r\nOutstanding: {balance:N2} {Store.Data.Settings.Currency}" });
            dialog.ShowDialog(FindForm());
        });
    }
    private void DetailsChanged(object? sender, EventArgs e) => RebuildDetails();
    private void RebuildDetails()
    {
        Grid.Relations.Clear();
        var relation = Grid.AddRelation("Enrollments", "Id", "StudentId", Store.Data.Enrollments, typeof(EnrollmentRecord));
        relation.ChildColumns.Add(new GridColumn { Field = "CourseId", Title = "Course ID", Width = 100 });
        relation.ChildColumns.Add(new GridColumn { Field = "Status", Title = "Status", Width = 150 });
        relation.ChildColumns.Add(new GridColumn { Field = "Grade", Title = "Grade %", Width = 100 });
    }
    protected override void Dispose(bool disposing) { if (disposing) Store.Changed -= DetailsChanged; base.Dispose(disposing); }
}

public sealed class AttendanceView : RecordWorkspace<AttendanceRecord>
{
    public AttendanceView(SchoolStore store) : base(store, "Attendance")
    {
        UseFacets(new[] { "Status", "CourseIdLabel" });
        AddCommand("Roll Call...", () => { using var dialog = new RollCallDialog(Store); dialog.ShowDialog(FindForm()); });
        AddCommand("Mark Today Present", () =>
        {
            if (MessageBox.Show(FindForm(), "Mark today's enrolled students as present? Existing marks will be retained.", "Roll Call", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            SchoolOperations.MarkAttendance(Store, DateTime.Today);
        });
        AddCommand("Today", () => Grid.FilterBy("Date", EnterpriseUI.Data.FilterOperator.IsEqualTo, DateTime.Today));
    }
}

public sealed class FinanceView : RecordWorkspace<InvoiceRecord>
{
    public FinanceView(SchoolStore store) : base(store, "Finance")
    {
        UseFacets(new[] { "Status" }, new[] { "Amount", "Balance" });
        AddCommand("Record Payment", () =>
        {
            if (Grid.FocusedRecord is not { } invoice) return;
            using var dialog = new PaymentDialog(Store, invoice);
            dialog.ShowDialog(FindForm());
        });
        AddCommand("Outstanding", () => Grid.FilterBy("Balance", EnterpriseUI.Data.FilterOperator.IsGreaterThan, 0m));
    }
}

public sealed class PaymentDialog : Form
{
    private readonly NumericUpDown _amount;
    private readonly TextBox _reference;
    private readonly Label _error;
    public PaymentDialog(SchoolStore store, InvoiceRecord invoice)
    {
        Text = "Record Payment · " + invoice.Number;
        StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false; ClientSize = new Size(430, 250); Font = SchoolUi.BodyFont;
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Padding = new Padding(22) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _amount = new NumericUpDown { DecimalPlaces = 2, Maximum = Math.Max(0, invoice.Balance), Value = Math.Max(0, invoice.Balance), Dock = DockStyle.Fill };
        _reference = new TextBox { Dock = DockStyle.Fill, Text = "BANK-" + DateTime.Now.ToString("yyyyMMddHHmm") };
        table.Controls.Add(new Label { Text = "Outstanding", AutoSize = true }, 0, 0);
        table.Controls.Add(new Label { Text = $"{invoice.Balance:N2} {store.Data.Settings.Currency}", AutoSize = true }, 1, 0);
        table.Controls.Add(new Label { Text = "Payment", AutoSize = true }, 0, 1); table.Controls.Add(_amount, 1, 1);
        table.Controls.Add(new Label { Text = "Reference", AutoSize = true }, 0, 2); table.Controls.Add(_reference, 1, 2);
        _error = new Label { Dock = DockStyle.Bottom, Height = 40, Padding = new Padding(18, 0, 0, 0) };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(14, 8, 14, 8) };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
        var save = new Button { Text = "Post Payment" };
        save.Click += (_, _) =>
        {
            try { SchoolOperations.RecordPayment(store, invoice.Id, _amount.Value, _reference.Text); DialogResult = DialogResult.OK; }
            catch (Exception exception) { _error.Text = exception.Message; }
        };
        buttons.Controls.Add(cancel); buttons.Controls.Add(save);
        Controls.Add(table); Controls.Add(_error); Controls.Add(buttons); AcceptButton = save; CancelButton = cancel; SchoolUi.Apply(this); _error.ForeColor = Color.Firebrick;
    }
}

public sealed class StaffView : RecordWorkspace<StaffRecord>
{
    public StaffView(SchoolStore store) : base(store, "Staff")
    {
        UseCards("FullName", "Position", "Department", ("Email", "Email", null), ("Active", "Active", null));
        UseFacets(new[] { "Department", "Position", "Active" });
    }
}

public sealed class EnrollmentsView : RecordWorkspace<EnrollmentRecord>
{
    public EnrollmentsView(SchoolStore store) : base(store, "Enrollments")
    {
        UseFacets(new[] { "Status", "CourseIdLabel" }, new[] { "Grade" });
    }
}

/// <summary>Courses with a department / course tree that filters the list.</summary>
public sealed class CoursesView : RecordWorkspace<CourseRecord>
{
    private readonly EnterpriseTreeViewControl _tree = new() { Dock = DockStyle.Left, Width = 280, AccessibleName = "Course catalog" };

    public CoursesView(SchoolStore store) : base(store, "Courses")
    {
        UseCards("Title", "Code", "Department", ("TeacherIdLabel", "Teacher", null), ("Credits", "Credits", null), ("Capacity", "Capacity", null), ("Active", "Active", null));
        Controls.Add(_tree);
        Controls.SetChildIndex(_tree, Controls.GetChildIndex(Grid) + 1);
        _tree.AfterSelect += (_, e) => ApplySelection(e.Node);
        BuildTree();
        Store.Changed += TreeChanged;
        SchoolUi.Apply(this);
    }

    private void TreeChanged(object? sender, EventArgs e) => BuildTree();

    private void BuildTree()
    {
        _tree.BeginUpdate();
        _tree.Nodes.Clear();
        var all = _tree.Nodes.Add("All courses (" + Store.Data.Courses.Count + ")");
        foreach (var department in Store.Data.Courses.GroupBy(item => item.Department).OrderBy(group => group.Key))
        {
            var node = all.Nodes.Add($"{department.Key} ({department.Count()})");
            node.Tag = department.Key;
            foreach (var course in department.OrderBy(item => item.Code))
            {
                var enrolled = Store.Data.Enrollments.Count(item => item.CourseId == course.Id && item.Status == "Enrolled");
                node.Nodes.Add($"{course.Code} · {course.Title} ({enrolled}/{course.Capacity})").Tag = course;
            }
        }

        all.Expanded = true;
        _tree.EndUpdate();
    }

    private void ApplySelection(EnterpriseTreeNode node)
    {
        Grid.ClearFilters();
        switch (node.Tag)
        {
            case string department: Grid.FilterBy("Department", EnterpriseUI.Data.FilterOperator.IsEqualTo, department); break;
            case CourseRecord course: Grid.FilterBy("Code", EnterpriseUI.Data.FilterOperator.IsEqualTo, course.Code); break;
        }
    }

    public EnterpriseTreeViewControl Tree => _tree;
    protected override void Dispose(bool disposing) { if (disposing) Store.Changed -= TreeChanged; base.Dispose(disposing); }
}

public sealed class TasksView : RecordWorkspace<TaskRecord>
{
    private readonly Panel _board;
    public TasksView(SchoolStore store) : base(store, "Tasks")
    {
        _board = new Panel { Dock = DockStyle.Fill, Visible = false, Padding = new Padding(16) };
        Controls.Add(_board); _board.BringToFront();
        AddCommand("Board / List", () =>
        {
            _board.Visible = !_board.Visible;
            Grid.Visible = !_board.Visible;
            if (_board.Visible) BuildBoard();
        });
        Store.Changed += BoardChanged;
        AddCommand("Complete", () => { if (Grid.FocusedRecord is { } task) Store.Transaction(data => { var record = data.Tasks.Single(item => item.Id == task.Id); record.Status = "Done"; record.Progress = 100; }); });
    }
    private void BoardChanged(object? sender, EventArgs e) { if (_board.Visible) BuildBoard(); }
    private void BuildBoard()
    {
        foreach (Control control in _board.Controls.Cast<Control>().ToArray()) control.Dispose();
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        foreach (var status in new[] { "To do", "In progress", "Done" })
        {
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
            var lane = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, AllowDrop = true, Padding = new Padding(10), Margin = new Padding(6) };
            lane.Controls.Add(new Label { Text = status, Width = 260, Height = 34, Font = new Font("Segoe UI", 12f, FontStyle.Bold) });
            lane.DragEnter += (_, e) => e.Effect = e.Data?.GetDataPresent(typeof(TaskRecord)) == true ? DragDropEffects.Move : DragDropEffects.None;
            var target = status;
            lane.DragDrop += (_, e) =>
            {
                if (e.Data?.GetData(typeof(TaskRecord)) is TaskRecord task)
                    Store.Transaction(data => { var record = data.Tasks.Single(item => item.Id == task.Id); record.Status = target; record.Progress = target == "Done" ? 100 : target == "In progress" ? 50 : 0; });
            };
            foreach (var task in Store.Data.Tasks.Where(item => item.Status == status))
            {
                var card = new Button { Width = 270, Height = 100, TextAlign = ContentAlignment.MiddleLeft, Text = $"{task.Title}\r\n{Store.Data.Staff.Single(item => item.Id == task.OwnerId).FullName}\r\n{task.DueOn:dd MMM}  ·  {task.Priority}", Margin = new Padding(0, 0, 0, 10), BackColor = SchoolUi.Header, ForeColor = SchoolUi.Text, FlatStyle = FlatStyle.Flat };
                var current = task;
                var dragStart = Point.Empty;
                card.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) dragStart = e.Location; };
                card.MouseMove += (_, e) =>
                {
                    if (e.Button == MouseButtons.Left && (Math.Abs(e.X - dragStart.X) >= SystemInformation.DragSize.Width || Math.Abs(e.Y - dragStart.Y) >= SystemInformation.DragSize.Height))
                        card.DoDragDrop(current, DragDropEffects.Move);
                };
                card.Click += (_, _) => Edit(current);
                lane.Controls.Add(card);
            }
            layout.Controls.Add(lane);
        }
        _board.Controls.Add(layout);
        _board.Top = Commands.Bottom;
    }
    protected override void Dispose(bool disposing) { if (disposing) Store.Changed -= BoardChanged; base.Dispose(disposing); }
}