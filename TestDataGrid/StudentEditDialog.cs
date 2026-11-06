using System.Net.Mail;
using EnterpriseUI.Grids;
using EnterpriseUI.Theming;

namespace TestDataGrid;

public sealed class StudentEditDialog : Form
{
    private readonly GridRowEditSession<Student> _session;
    private readonly TextBox _nameInput;
    private readonly TextBox _emailInput;
    private readonly ErrorProvider _errors;
    private readonly List<(GridColumn Column, Control Input)> _inputs = new();

    public StudentEditDialog(GridRowEditSession<Student> session, IEnumerable<GridColumn> columns)
    {
        _session = session;
        var palette = GridPalette.FromTheme(ThemeManager.Instance.CurrentTheme);
        var surface = ColorTranslator.FromHtml(palette.Surface);
        var text = ColorTranslator.FromHtml(palette.Text);

        Text = "Edit Student";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 10f);
        BackColor = surface;
        ForeColor = text;
        var editableColumns = columns.Where(column => column.IsEditable && column is not GridCommandColumn).ToList();
        ClientSize = new Size(540, Math.Min(660, 160 + editableColumns.Count * 40));

        var fields = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20),
            ColumnCount = 2,
            RowCount = editableColumns.Count + 2,
            AutoScroll = true
        };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var rowIndex = 0; rowIndex < fields.RowCount; rowIndex++)
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

        TextBox AddField(string label, string value, int rowIndex, bool readOnly = false)
        {
            fields.Controls.Add(new Label
            {
                Text = label,
                AutoSize = true,
                Anchor = AnchorStyles.Left
            }, 0, rowIndex);
            var input = new TextBox
            {
                Text = value,
                ReadOnly = readOnly,
                TabStop = !readOnly,
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                BackColor = ColorTranslator.FromHtml(readOnly ? palette.Alt : palette.Surface),
                ForeColor = text,
                AccessibleName = label
            };
            fields.Controls.Add(input, 1, rowIndex);
            return input;
        }

        AddField("Student ID", session.Item.Id.ToString(), 0, true);
        AddField("Address ID", session.Item.AddressId.ToString(), 1, true);
        foreach (var column in editableColumns)
        {
            var rowIndex = _inputs.Count + 2;
            var value = column.GetValue(session.Item);
            Control input;
            if (column.ColumnType == GridColumnType.Boolean)
                input = new CheckBox { Checked = value is true, AutoSize = true };
            else if (column.ColumnType == GridColumnType.Date && value is DateTime date)
                input = new DateTimePicker { Format = DateTimePickerFormat.Short, Value = date };
            else if (column.ColumnType == GridColumnType.ComboBox && column.ComboItems != null)
            {
                var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
                combo.Items.AddRange(column.ComboItems.ToArray());
                combo.SelectedItem = value;
                input = combo;
            }
            else
                input = new TextBox { Text = Convert.ToString(value, System.Globalization.CultureInfo.CurrentCulture), BackColor = surface, ForeColor = text };

            input.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            input.AccessibleName = column.Title;
            fields.Controls.Add(new Label { Text = column.Title, AutoSize = true, Anchor = AnchorStyles.Left }, 0, rowIndex);
            fields.Controls.Add(input, 1, rowIndex);
            _inputs.Add((column, input));
        }
        _nameInput = (TextBox)_inputs.First(input => input.Column.Field == nameof(Student.Name)).Input;
        _emailInput = (TextBox)_inputs.First(input => input.Column.Field == nameof(Student.Email)).Input;
        _errors = new ErrorProvider { ContainerControl = this, BlinkStyle = ErrorBlinkStyle.NeverBlink };

        var save = new Button { Text = "Save", Width = 90, Height = 32, ForeColor = SystemColors.ControlText, UseVisualStyleBackColor = true };
        var cancel = new Button { Text = "Cancel", Width = 90, Height = 32, ForeColor = SystemColors.ControlText, DialogResult = DialogResult.Cancel, UseVisualStyleBackColor = true };
        save.Click += SaveClicked;
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 60,
            Padding = new Padding(16, 10, 16, 10),
            FlowDirection = FlowDirection.RightToLeft
        };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(save);
        Controls.Add(fields);
        Controls.Add(buttons);
        AcceptButton = save;
        CancelButton = cancel;
        ActiveControl = _nameInput;
    }

    private void SaveClicked(object? sender, EventArgs e)
    {
        _errors.Clear();
        var name = _nameInput.Text.Trim();
        var email = _emailInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(name)) _errors.SetError(_nameInput, "Enter a student name.");
        if (!MailAddress.TryCreate(email, out _)) _errors.SetError(_emailInput, "Enter a valid email address.");
        if (_errors.GetError(_nameInput).Length > 0 || _errors.GetError(_emailInput).Length > 0) return;

        foreach (var (column, input) in _inputs)
        {
            _session.SetValue(column.Field, input switch
            {
                CheckBox checkbox => checkbox.Checked,
                DateTimePicker date => date.Value.Date,
                ComboBox combo => combo.SelectedItem,
                TextBox textbox => textbox.Text.Trim(),
                _ => null
            });
        }
        if (_session.TryCommit(out var errors))
        {
            DialogResult = DialogResult.OK;
            return;
        }
        foreach (var error in errors)
        {
            var input = _inputs.FirstOrDefault(input => input.Column.Field == error.Field).Input ?? _nameInput;
            _errors.SetError(input, error.Message);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _errors?.Dispose();
        base.Dispose(disposing);
    }
}