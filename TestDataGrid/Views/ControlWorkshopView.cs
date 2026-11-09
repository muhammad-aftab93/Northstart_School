using EnterpriseUI.WinForms;

namespace TestDataGrid.Views;

public sealed class ControlWorkshopView : UserControl
{
    private readonly Panel _preview;
    private readonly EnterprisePropertyGridControl _properties;
    public ControlWorkshopView()
    {
        Dock = DockStyle.Fill; Font = SchoolUi.BodyFont;
        var controls = new EnterpriseListControl { Dock = DockStyle.Left, Width = 290, DisplayMember = "Name", ItemHeight = 30, AccessibleName = "Controls" };
        controls.DataSource = typeof(EnterpriseDataGrid).Assembly.GetExportedTypes().Where(type => typeof(Control).IsAssignableFrom(type) && !type.IsAbstract && !type.ContainsGenericParameters && type.GetConstructor(Type.EmptyTypes) != null).OrderBy(type => type.Name).ToList();
        _preview = new Panel { Dock = DockStyle.Fill, Padding = new Padding(30) };
        _properties = new EnterprisePropertyGridControl { Dock = DockStyle.Right, Width = 380, AccessibleName = "Control properties" };
        controls.SelectionChanged += (_, _) =>
        {
            try
            {
                foreach (Control old in _preview.Controls.Cast<Control>().ToArray()) old.Dispose();
                if (controls.SelectedItem is not Type type) return;
                var control = (Control)Activator.CreateInstance(type)!;
                control.Location = new Point(30, 30); control.Width = Math.Max(200, _preview.Width - 60); control.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                _preview.Controls.Add(control); _properties.SelectedObject = control;
            }
            catch (Exception exception) { MessageBox.Show(FindForm(), exception.Message, "Control Preview", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        };
        _properties.PropertyValueChanged += (_, _) => _preview.Invalidate(true);
        Controls.Add(_preview); Controls.Add(_properties); Controls.Add(controls); Controls.Add(SchoolUi.Heading("Control Explorer"));
        if (controls.Count > 0) controls.SelectedIndex = 0;
        SchoolUi.Apply(this);
    }
}
