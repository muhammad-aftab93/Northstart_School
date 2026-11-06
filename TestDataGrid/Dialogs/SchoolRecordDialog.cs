using System.Reflection;
using TestDataGrid.Models;
using TestDataGrid.Services;
using TestDataGrid.Views;

namespace TestDataGrid.Dialogs;

public sealed class SchoolRecordDialog<T> : Form where T : SchoolRecord
{
    private readonly SchoolStore _store;
    private readonly T _draft;
    private readonly List<(PropertyInfo Property, Control Input)> _inputs = new();
    private readonly Label _error;
    public T SavedRecord => _draft;

    public SchoolRecordDialog(SchoolStore store, T record, string title)
    {
        _store = store;
        _draft = SchoolStore.Clone(record);
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        Font = SchoolUi.BodyFont;
        AutoScaleMode = AutoScaleMode.Dpi;
        var fields = SchoolSchema.Fields<T>().Where(field => !field.ReadOnly).ToArray();
        ClientSize = new Size(570, Math.Min(700, 180 + fields.Length * 44));
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24, 14, 34, 8), AutoScroll = true, ColumnCount = 2, RowCount = fields.Length };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var index = 0; index < fields.Length; index++)
        {
            var field = fields[index];
            var property = typeof(T).GetProperty(field.Name)!;
            var value = property.GetValue(_draft);
            Control input;
            if (field.Lookup != null)
            {
                var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Label", ValueMember = "Id" };
                combo.DataSource = field.Lookup(store.Data).ToList();
                combo.SelectedValue = value ?? 0;
                if (combo.SelectedIndex < 0 && combo.Items.Count > 0) combo.SelectedIndex = 0;
                input = combo;
            }
            else if (field.Choices != null)
            {
                var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
                combo.Items.AddRange(field.Choices);
                combo.SelectedItem = value;
                if (combo.SelectedIndex < 0) combo.SelectedIndex = 0;
                input = combo;
            }
            else if (property.PropertyType == typeof(bool)) input = new CheckBox { Checked = value is true, AutoSize = true };
            else if (property.PropertyType == typeof(DateTime)) input = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = field.Name.EndsWith("At", StringComparison.Ordinal) ? "dd MMM yyyy HH:mm" : "dd MMM yyyy", Value = value is DateTime date ? date : DateTime.Today };
            else if (property.PropertyType == typeof(int) || property.PropertyType == typeof(decimal)) input = new NumericUpDown { Minimum = 0, Maximum = 1000000, DecimalPlaces = property.PropertyType == typeof(decimal) ? 2 : 0, Value = Convert.ToDecimal(value ?? 0) };
            else input = new TextBox { Text = Convert.ToString(value), Multiline = field.Multiline, Height = field.Multiline ? 65 : 28 };
            input.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            input.AccessibleName = field.Label;
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, field.Multiline ? 80 : 44));
            table.Controls.Add(new Label { Text = field.Label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, index);
            table.Controls.Add(input, 1, index);
            _inputs.Add((property, input));
        }
        _error = new Label { Dock = DockStyle.Bottom, Height = 46, Padding = new Padding(24, 0, 20, 0), ForeColor = Color.Firebrick, AutoEllipsis = true };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 60, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(18, 10, 18, 10) };
        var cancel = new Button { Text = "Cancel", Width = 90, Height = 32, DialogResult = DialogResult.Cancel };
        var save = new Button { Text = "Save", Width = 90, Height = 32 };
        save.Click += (_, _) => SaveDraft();
        buttons.Controls.Add(cancel); buttons.Controls.Add(save);
        Controls.Add(table); Controls.Add(_error); Controls.Add(buttons);
        AcceptButton = save;
        CancelButton = cancel;
        SchoolUi.Apply(this);
        _error.ForeColor = Color.Firebrick;
    }

    public bool SaveDraft()
    {
        try
        {
            foreach (var (property, input) in _inputs)
            {
                object? value = input switch
                {
                    CheckBox checkbox => checkbox.Checked,
                    DateTimePicker date => date.Value,
                    NumericUpDown numeric => property.PropertyType == typeof(int) ? (object)(int)numeric.Value : numeric.Value,
                    ComboBox combo => combo.SelectedItem is LookupOption option ? option.Id : combo.SelectedItem,
                    TextBox text => text.Text.Trim(),
                    _ => null
                };
                property.SetValue(_draft, value);
            }
            if (_draft is TaskRecord task && task.Status == "Done") task.Progress = 100;
            SchoolOperations.SaveRecord(_store, _draft);
            DialogResult = DialogResult.OK;
            return true;
        }
        catch (Exception exception) { _error.Text = exception.Message; return false; }
    }
}