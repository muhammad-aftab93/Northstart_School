using System.Globalization;
using System.Reflection;
using EnterpriseUI.Editors.Interactivity;
using EnterpriseUI.WinForms;
using TestDataGrid.Models;
using TestDataGrid.Services;
using TestDataGrid.Views;

namespace TestDataGrid.Dialogs;

public sealed class SchoolRecordDialog<T> : Form where T : SchoolRecord
{
    private static readonly string[] DateFormats = { "yyyy-MM-dd", "yyyy-MM-dd HH:mm", "yyyy-MM-ddTHH:mm" };

    private readonly SchoolStore _store;
    private readonly T _draft;
    private readonly List<(PropertyInfo Property, Control Input, RecordField Field, IReadOnlyList<LookupOption>? Options)> _inputs = new();
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
        var rows = (fields.Count(field => !field.Multiline) + 1) / 2 + fields.Count(field => field.Multiline);
        ClientSize = new Size(720, Math.Clamp(190 + rows * 82, 380, 720));

        var layout = new EnterpriseDataLayoutControl { Dock = DockStyle.Fill, ColumnWidth = 320, MaxColumns = 2, CardGroups = false, AccessibleName = title };
        foreach (var field in fields)
        {
            var property = typeof(T).GetProperty(field.Name)!;
            var value = property.GetValue(_draft);
            IReadOnlyList<LookupOption>? options = null;
            Control input;
            string? hint = null;
            if (field.Rating)
            {
                input = new EnterpriseStarRatingControl { Maximum = 5, Precision = RatingPrecision.Half, SymbolSize = 26, Width = 220, Height = 40, Value = Convert.ToDouble(value ?? 0m), Descriptions = new[] { "Poor", "Fair", "Good", "Very good", "Excellent" } };
                hint = "Click a star again to clear the rating";
            }
            else if (field.Lookup != null)
            {
                options = field.Lookup(store.Data).ToList();
                var drop = new EnterpriseDropDownControl { PlaceholderText = "Select " + field.Label.Replace(" ID", string.Empty, StringComparison.Ordinal).ToLowerInvariant() };
                drop.SetItems(options.Select(option => option.Label));
                drop.SelectedIndex = Math.Max(0, options.ToList().FindIndex(option => option.Id == Convert.ToInt32(value ?? 0)));
                input = drop;
            }
            else if (field.Choices != null)
            {
                var drop = new EnterpriseDropDownControl();
                drop.SetItems(field.Choices);
                drop.SelectedItem = Convert.ToString(value);
                if (drop.SelectedIndex < 0) drop.SelectedIndex = 0;
                input = drop;
            }
            else if (property.PropertyType == typeof(bool)) input = new EnterpriseCheckControl { Text = "Yes", Checked = value is true };
            else if (property.PropertyType == typeof(DateTime))
            {
                var withTime = field.Name.EndsWith("At", StringComparison.Ordinal);
                input = new EnterpriseInputControl { Text = ((DateTime)(value ?? DateTime.Today)).ToString(withTime ? "yyyy-MM-dd HH:mm" : "yyyy-MM-dd", CultureInfo.InvariantCulture) };
                hint = withTime ? "Format: 2026-11-09 14:30" : "Format: 2026-11-09";
            }
            else if (property.PropertyType == typeof(int) || property.PropertyType == typeof(decimal))
                input = new EnterpriseInputControl { Text = Convert.ToDecimal(value ?? 0).ToString(CultureInfo.CurrentCulture) };
            else input = new EnterpriseInputControl { Text = Convert.ToString(value) ?? string.Empty, Multiline = field.Multiline };

            input.AccessibleName = field.Label;
            layout.AddItem(field.Label, input, columnSpan: field.Multiline ? 2 : 1, height: field.Multiline ? 80 : 0, description: hint);
            _inputs.Add((property, input, field, options));
        }

        _error = new Label { Dock = DockStyle.Bottom, Height = 46, Padding = new Padding(24, 0, 20, 0), ForeColor = Color.Firebrick, AutoEllipsis = true };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 60, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(18, 10, 18, 10) };
        var cancel = new EnterpriseButtonControl { Text = "Cancel", Variant = EnterpriseButtonVariant.Outline, Width = 100, Height = 34, DialogResult = DialogResult.Cancel };
        var save = new EnterpriseButtonControl { Text = "Save", Glyph = "\u2714", Variant = EnterpriseButtonVariant.Primary, Width = 100, Height = 34 };
        save.Click += (_, _) => SaveDraft();
        buttons.Controls.Add(cancel); buttons.Controls.Add(save);
        AddAssistTools(buttons);
        Controls.Add(layout); Controls.Add(_error); Controls.Add(buttons);
        AcceptButton = save;
        CancelButton = cancel;
        SchoolUi.Apply(this);
        _error.ForeColor = Color.Firebrick;
    }

    /// <summary>Contact paste for people records; dictation and inline writing help for the notes field.</summary>
    private void AddAssistTools(FlowLayoutPanel buttons)
    {
        var first = true;
        Padding NextMargin() { var margin = first ? new Padding(28, 3, 3, 3) : new Padding(3); first = false; return margin; }

        if (typeof(T) == typeof(StudentRecord) || typeof(T) == typeof(StaffRecord))
        {
            var paste = new EnterpriseSmartPasteButton { Text = "Paste details", Width = 160, Height = 34, Margin = NextMargin(), AccessibleName = "Paste contact details" };
            foreach (var (_, input, field, _) in _inputs)
            {
                if (field.Name == "FullName") paste.Bind(input, SmartPasteFieldKind.Name, "FullName");
                else if (field.Name == "Email") paste.Bind(input, SmartPasteFieldKind.Email, "Email");
            }

            paste.Failed += (_, exception) => SchoolUi.Hint(paste, exception.Message, "Nothing was pasted");
            buttons.Controls.Add(paste);
        }

        if (_inputs.Where(item => item.Field.Multiline).Select(item => item.Input).OfType<EnterpriseInputControl>().FirstOrDefault() is not { } notes) return;
        var mic = new EnterpriseSpeechToTextButton { Text = "Dictate", Target = notes.InnerTextBox, Provider = SchoolUi.Speech, Width = 150, Height = 34, ShowPartialText = false, Margin = NextMargin(), AccessibleName = "Dictate notes" };
        mic.Error += (_, exception) => SchoolUi.Hint(mic, exception.Message, "Dictation is unavailable");
        buttons.Controls.Add(mic);
        var assistant = new EnterpriseInlineAIAssistant { Target = notes.InnerTextBox, Provider = new SampleAIPromptProvider(), Context = "A short note in a school record" };
        Disposed += (_, _) => assistant.Dispose();
    }

    private static object ParseNumber(PropertyInfo property, RecordField field, string text)
    {
        if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var number)) throw new FormatException($"{field.Label} must be a number.");
        if (property.PropertyType == typeof(int)) return number == decimal.Truncate(number) ? (int)number : throw new FormatException($"{field.Label} must be a whole number.");
        return number;
    }

    private static DateTime ParseDate(RecordField field, string text)
    {
        if (DateTime.TryParseExact(text.Trim(), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact)) return exact;
        if (DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out var parsed)) return parsed;
        throw new FormatException($"{field.Label} must be a date such as 2026-11-09.");
    }

    public bool SaveDraft()
    {
        try
        {
            foreach (var (_, input, _, _) in _inputs) if (input is EnterpriseInputControl text) text.HasError = false;
            foreach (var (property, input, field, options) in _inputs)
            {
                try
                {
                    object? value = input switch
                    {
                        EnterpriseStarRatingControl rating => (decimal)rating.Value,
                        EnterpriseCheckControl check => check.Checked,
                        EnterpriseDropDownControl drop when options != null => drop.SelectedIndex >= 0 ? options[drop.SelectedIndex].Id : 0,
                        EnterpriseDropDownControl drop => drop.SelectedItem,
                        EnterpriseInputControl text when property.PropertyType == typeof(DateTime) => ParseDate(field, text.Text),
                        EnterpriseInputControl text when property.PropertyType == typeof(int) || property.PropertyType == typeof(decimal) => ParseNumber(property, field, text.Text),
                        EnterpriseInputControl text => text.Text.Trim(),
                        _ => null
                    };
                    property.SetValue(_draft, value);
                }
                catch (FormatException exception)
                {
                    if (input is EnterpriseInputControl bad) bad.HasError = true;
                    _error.Text = string.Empty;
                    SchoolUi.Hint(input, exception.Message);
                    return false;
                }
            }

            if (_draft is TaskRecord task && task.Status == "Done") task.Progress = 100;
            SchoolOperations.SaveRecord(_store, _draft);
            DialogResult = DialogResult.OK;
            return true;
        }
        catch (Exception exception) { _error.Text = exception.Message; return false; }
    }
}
