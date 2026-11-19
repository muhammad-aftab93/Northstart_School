using System.ComponentModel;
using EnterpriseUI.WinForms;

namespace TestDataGrid.Dialogs;

/// <summary>A date and a time of day edited side by side and read as one <see cref="DateTime"/>.</summary>
public sealed class DateTimeFieldEditor : TableLayoutPanel
{
    private readonly EnterpriseDateOnlyPickerControl _date = new() { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 6, 0), PlaceholderText = "Date" };
    private readonly EnterpriseTimePickerControl _time = new() { Dock = DockStyle.Fill, Margin = Padding.Empty, MinuteStep = 5, Is24Hour = true, PlaceholderText = "Time" };

    public DateTimeFieldEditor(string label, DateTime value)
    {
        Height = 36;
        ColumnCount = 2;
        RowCount = 1;
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        Controls.Add(_date, 0, 0);
        Controls.Add(_time, 1, 0);
        _date.AccessibleName = label + " date";
        _time.AccessibleName = label + " time";
        _date.Value = DateOnly.FromDateTime(value);
        _time.Value = value;
    }

    public EnterpriseDateOnlyPickerControl DatePicker => _date;
    public EnterpriseTimePickerControl TimePicker => _time;

    /// <summary>Null while the date or the time is empty.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public DateTime? Value
    {
        get => _date.Value is { } date && _time.Value is { } time ? date.ToDateTime(TimeOnly.FromDateTime(time)) : null;
        set
        {
            _date.Value = value is { } v ? DateOnly.FromDateTime(v) : null;
            _time.Value = value;
        }
    }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool HasError
    {
        get => _date.HasError || _time.HasError;
        set => _date.HasError = _time.HasError = value;
    }
}
