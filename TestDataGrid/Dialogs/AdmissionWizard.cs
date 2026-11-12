using System.Net.Mail;
using EnterpriseUI.Editors.Interactivity;
using EnterpriseUI.WinForms;
using TestDataGrid.Models;
using TestDataGrid.Services;
using TestDataGrid.Views;

namespace TestDataGrid.Dialogs;

/// <summary>Four-step admission: student details, courses, tuition, review. Nothing is saved until the last step is confirmed.</summary>
public sealed class AdmissionWizard : Form
{
    private sealed record CourseChoice(int Id, string Label, int SeatsLeft);

    private static readonly string[] Departments = { "Science", "Business", "Engineering", "Arts" };
    private const int TuitionPerCourse = 425;

    private readonly SchoolStore _store;
    private readonly Control[] _pages;
    private readonly List<CourseChoice> _choices = new();
    private bool _tuitionChosen;

    public EnterpriseStepProgressBarControl Steps { get; } = new() { Dock = DockStyle.Top, Height = 96, AccessibleName = "Admission steps" };
    public EnterpriseInputControl NameInput { get; } = new() { AccessibleName = "Full name", PlaceholderText = "Full name", Height = 36 };
    public EnterpriseInputControl EmailInput { get; } = new() { AccessibleName = "Email", PlaceholderText = "name@example.com", Height = 36 };
    public EnterpriseDropDownControl DepartmentInput { get; } = new() { AccessibleName = "Department", Height = 36 };
    public EnterpriseDropDownControl YearInput { get; } = new() { AccessibleName = "Year", Height = 36 };
    public EnterpriseCheckedListBoxControl CourseList { get; } = new() { DisplayMember = "Label", ItemHeight = 34, Dock = DockStyle.Fill, AccessibleName = "Courses", EmptyText = "No active courses" };
    public EnterpriseTrackBarControl Tuition { get; } = new() { Dock = DockStyle.Top, Height = 74, Minimum = 0, Maximum = 3000, Step = 50, LargeChange = 250, TickFrequency = 500, TickStyle = TrackTickStyle.BottomRight, ShowTickLabels = true, AccessibleName = "Tuition amount" };
    public EnterpriseButtonControl BackButton { get; } = new() { Text = "Back", Variant = EnterpriseButtonVariant.Outline, Width = 100, Height = 34 };
    public EnterpriseButtonControl NextButton { get; } = new() { Text = "Next", Variant = EnterpriseButtonVariant.Primary, Width = 150, Height = 34 };
    private readonly EnterpriseLabelControl _review = new() { Dock = DockStyle.Fill, MarkupMode = LabelMarkupMode.Markdown, AccessibleName = "Admission summary" };
    private readonly EnterpriseLabelControl _tuitionNote = new() { Dock = DockStyle.Top, Height = 54, MarkupMode = LabelMarkupMode.Markdown };

    public AdmissionWizard(SchoolStore store)
    {
        _store = store;
        Text = "Admit Student";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        Font = SchoolUi.BodyFont;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(780, 560);

        DepartmentInput.SetItems(Departments);
        DepartmentInput.SelectedIndex = 0;
        YearInput.SetItems(Enumerable.Range(1, 6).Select(year => "Year " + year));
        YearInput.SelectedIndex = 0;
        Tuition.ValueFormat = "{0:N0}";
        Tuition.ValueChanged += (_, _) => UpdateTuitionNote();
        Tuition.ValueCommitted += (_, _) => _tuitionChosen = true;

        foreach (var course in store.Data.Courses.Where(item => item.Active).OrderBy(item => item.Code))
        {
            var seats = course.Capacity - store.Data.Enrollments.Count(item => item.CourseId == course.Id && item.Status == "Enrolled");
            _choices.Add(new CourseChoice(course.Id, $"{course.Code} \u00b7 {course.Title}   ({(seats > 0 ? seats + " seats left" : "full")})", seats));
        }

        CourseList.AddRange(_choices.Cast<object>());

        Steps.AddStep("Student", "Name and contact");
        Steps.AddStep("Courses", "Choose subjects");
        Steps.AddStep("Tuition", "Fee to invoice");
        Steps.AddStep("Review", "Confirm admission");
        Steps.StepChanging += OnStepChanging;
        Steps.StepChanged += (_, _) => ShowPage();

        _pages = new[] { BuildStudentPage(), BuildCoursesPage(), BuildTuitionPage(), BuildReviewPage() };
        var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 8, 24, 8) };
        foreach (var page in _pages) { page.Dock = DockStyle.Fill; page.Visible = false; host.Controls.Add(page); }

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 58, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(18, 10, 18, 10) };
        var cancel = new EnterpriseButtonControl { Text = "Cancel", Variant = EnterpriseButtonVariant.Outline, Width = 100, Height = 34, DialogResult = DialogResult.Cancel };
        BackButton.Click += (_, _) => Steps.Previous();
        NextButton.Click += (_, _) => Advance();
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(NextButton);
        buttons.Controls.Add(BackButton);

        Controls.Add(host);
        Controls.Add(buttons);
        Controls.Add(Steps);
        AcceptButton = NextButton;
        CancelButton = cancel;
        SchoolUi.Apply(this);
        ShowPage();
    }

    private Control BuildStudentPage()
    {
        var page = new TableLayoutPanel { ColumnCount = 2, RowCount = 5, Padding = new Padding(0, 6, 0, 0) };
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < 4; row++) page.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        void Row(int index, string caption, Control input)
        {
            page.Controls.Add(new EnterpriseLabelControl { Text = caption, LabelFor = input is EnterpriseInputControl text ? text.InnerTextBox : input, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, index);
            input.Dock = DockStyle.Top;
            page.Controls.Add(input, 1, index);
        }

        Row(0, "&Full name", NameInput);
        Row(1, "&Email", EmailInput);
        Row(2, "&Department", DepartmentInput);
        Row(3, "&Year", YearInput);

        var paste = new EnterpriseSmartPasteButton { Text = "Paste contact details", Width = 230, Height = 36, Dock = DockStyle.Top, AccessibleName = "Paste contact details" };
        paste.Bind(NameInput, SmartPasteFieldKind.Name, "FullName");
        paste.Bind(EmailInput, SmartPasteFieldKind.Email, "Email");
        paste.Failed += (_, exception) => SchoolUi.Hint(paste, exception.Message, "Nothing was pasted");
        var hint = new EnterpriseLabelControl { Text = "Copy an email signature or a \"name: ...\" block, then paste to fill the name and email.", Dock = DockStyle.Top, Height = 40, ForeColor = SchoolUi.Text };
        var pasteRow = new Panel { Dock = DockStyle.Fill };
        pasteRow.Controls.Add(hint);
        pasteRow.Controls.Add(paste);
        page.Controls.Add(pasteRow, 1, 4);
        return page;
    }

    private Control BuildCoursesPage()
    {
        var page = new Panel();
        page.Controls.Add(CourseList);
        page.Controls.Add(new EnterpriseLabelControl { Text = "Tick every course the student starts with. Courses without free seats cannot be chosen.", Dock = DockStyle.Top, Height = 40 });
        return page;
    }

    private Control BuildTuitionPage()
    {
        var page = new Panel();
        page.Controls.Add(_tuitionNote);
        page.Controls.Add(Tuition);
        page.Controls.Add(new EnterpriseLabelControl { Text = "Set the tuition for the first invoice. The suggestion is " + TuitionPerCourse + " per course.", Dock = DockStyle.Top, Height = 40 });
        return page;
    }

    private Control BuildReviewPage() => _review;

    private IEnumerable<CourseChoice> Chosen => CourseList.CheckedIndices.Cast<int>().Select(index => _choices[index]);

    private void UpdateTuitionNote()
    {
        var count = Chosen.Count();
        _tuitionNote.Text = $"**{Tuition.Value:N0} {_store.Data.Settings.Currency}** for {count} course{(count == 1 ? string.Empty : "s")}";
    }

    private (string? Message, Control? Target) Validate(int step)
    {
        switch (step)
        {
            case 0:
                if (NameInput.Text.Trim().Length < 3) return ("Enter the student's full name.", NameInput);
                if (!MailAddress.TryCreate(EmailInput.Text.Trim(), out _)) return ("Enter a valid email address.", EmailInput);
                return (null, null);
            case 1:
                if (!Chosen.Any()) return ("Choose at least one course.", CourseList);
                if (Chosen.FirstOrDefault(choice => choice.SeatsLeft <= 0) is { } full) return (full.Label.Split('\u00b7')[0].Trim() + " is full. Untick it to continue.", CourseList);
                return (null, null);
            case 2:
                return Tuition.Value < 50 ? ("Set a tuition amount of at least 50.", Tuition) : (null, null);
            default:
                return (null, null);
        }
    }

    private void OnStepChanging(object? sender, StepChangingEventArgs e)
    {
        if (e.To <= e.From) return;
        var (message, target) = Validate(e.From);
        if (message == null) return;
        e.Cancel = true;
        e.Message = message;
        SchoolUi.Hint(target ?? NextButton, message);
    }

    private void Advance()
    {
        if (Steps.CurrentStep < Steps.StepCount - 1) { Steps.Next(); return; }
        if (!Steps.Finish()) return;
        Admit();
    }

    private void ShowPage()
    {
        var step = Steps.CurrentStep;
        for (var index = 0; index < _pages.Length; index++) _pages[index].Visible = index == step;
        if (step == 2 && !_tuitionChosen) Tuition.Value = Math.Min(Tuition.Maximum, Chosen.Count() * TuitionPerCourse / 50 * 50);
        if (step == 2) UpdateTuitionNote();
        if (step == 3) _review.Text = BuildReview();
        BackButton.Enabled = step > 0;
        NextButton.Text = step == Steps.StepCount - 1 ? "Admit student" : "Next";
        NextButton.Glyph = step == Steps.StepCount - 1 ? "\u2714" : null;
    }

    private string BuildReview() =>
        $"**{NameInput.Text.Trim()}**\n{EmailInput.Text.Trim()}\n{DepartmentInput.SelectedItem} \u00b7 {YearInput.SelectedItem}\n\n**Courses**\n"
        + string.Join("\n", Chosen.Select(choice => "- " + choice.Label.Split('(')[0].Trim()))
        + $"\n\n**Tuition invoice:** {Tuition.Value:N0} {_store.Data.Settings.Currency}";

    private void Admit()
    {
        try
        {
            using var busy = SchoolUi.Busy(this, "Admitting student\u2026");
            var student = new StudentRecord { FullName = NameInput.Text.Trim(), Email = EmailInput.Text.Trim(), Department = (string)DepartmentInput.SelectedItem!, Year = Math.Max(1, YearInput.SelectedIndex + 1) };
            SchoolOperations.AdmitStudent(_store, student, Chosen.Select(choice => choice.Id).ToList(), (decimal)Tuition.Value);
            DialogResult = DialogResult.OK;
        }
        catch (Exception exception)
        {
            SchoolUi.Hint(NextButton, exception.Message, "The student could not be admitted");
        }
    }
}
