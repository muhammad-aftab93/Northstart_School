using EnterpriseUI.WinForms;

namespace TestDataGrid.Views;

/// <summary>Wrapping row of command buttons shared by the list screens.</summary>
public sealed class CommandBar : FlowLayoutPanel
{
    private readonly Dictionary<string, Control> _items = new();

    public CommandBar()
    {
        Dock = DockStyle.Top;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        WrapContents = true;
        Padding = new Padding(16, 6, 10, 6);
        Font = SchoolUi.BodyFont;
    }

    public IReadOnlyCollection<string> Titles => _items.Keys;
    public Control? Find(string title) => _items.GetValueOrDefault(title);

    private int ButtonHeight => LogicalToDeviceUnits(32);

    private int WidthFor(string text, bool glyph, bool arrow)
    {
        using var bold = new Font(Font, FontStyle.Bold);
        return TextRenderer.MeasureText(text, bold).Width + LogicalToDeviceUnits(28 + (glyph ? 22 : 0) + (arrow ? 24 : 0));
    }

    private T Place<T>(string title, T control) where T : Control
    {
        control.Margin = new Padding(0, 2, 6, 2);
        control.Height = ButtonHeight;
        _items[title] = control;
        Controls.Add(control);
        return control;
    }

    private void Run(string title, Action action)
    {
        try { action(); }
        catch (Exception exception) { MessageBox.Show(FindForm(), exception.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    public EnterpriseButtonControl AddCommand(string title, Action action, string? glyph = null, EnterpriseButtonVariant variant = EnterpriseButtonVariant.Outline)
    {
        var button = Place(title, new EnterpriseButtonControl { Text = title, Glyph = glyph, Variant = variant, Width = WidthFor(title, glyph != null, false), AccessibleName = title });
        button.Click += (_, _) => Run(title, action);
        return button;
    }

    public EnterpriseToggleButtonControl AddToggle(string title, Action<bool> changed, string? glyph = null)
    {
        var toggle = Place(title, new EnterpriseToggleButtonControl { Text = title, Glyph = glyph, Width = WidthFor(title, glyph != null, false), AccessibleName = title });
        toggle.CheckedChanged += (_, _) => Run(title, () => changed(toggle.Checked));
        return toggle;
    }

    public EnterpriseSplitButtonControl AddSplit(string title, Action primary, IEnumerable<(string Text, Action Action)> options, string? glyph = null)
    {
        var split = Place(title, new EnterpriseSplitButtonControl { Text = title, Glyph = glyph, Width = WidthFor(title, glyph != null, true), AccessibleName = title });
        split.PrimaryActionClicked += (_, _) => Run(title, primary);
        foreach (var (text, action) in options) split.AddOption(text, () => Run(title, action));
        return split;
    }

    public EnterpriseDropDownButtonControl AddMenu(string title, params (string Text, Action Action)[] options)
    {
        var menu = Place(title, new EnterpriseDropDownButtonControl { Text = title, Variant = EnterpriseButtonVariant.Outline, Width = WidthFor(title, false, true), AccessibleName = title });
        foreach (var (text, action) in options) menu.AddOption(text, () => Run(title, action));
        return menu;
    }

    public void AddSeparator()
    {
        Controls.Add(new Panel { Width = LogicalToDeviceUnits(10), Height = ButtonHeight, Margin = new Padding(0, 2, 0, 2) });
    }

    public T AddControl<T>(string title, T control) where T : Control => Place(title, control);

    public void PerformCommand(string title)
    {
        switch (Find(title))
        {
            case EnterpriseSplitButtonControl split: split.PerformClick(); break;
            case EnterpriseButtonBase button: button.PerformClick(); break;
        }
    }
}
