using EnterpriseUI.WinForms;

namespace TestDataGrid.Views;

public sealed class ControlWorkshopView : UserControl
{
    private readonly Panel _preview;
    private readonly PropertyGrid _properties;
    public ControlWorkshopView()
    {
        Dock = DockStyle.Fill; Font = SchoolUi.BodyFont;
        var controls = new ListBox { Dock = DockStyle.Left, Width = 270, DisplayMember = "Name" };
        controls.Items.AddRange(typeof(EnterpriseDataGrid).Assembly.GetExportedTypes().Where(type => typeof(Control).IsAssignableFrom(type) && !type.IsAbstract && !type.ContainsGenericParameters && type.GetConstructor(Type.EmptyTypes) != null).OrderBy(type => type.Name).Cast<object>().ToArray());
        _preview = new Panel { Dock = DockStyle.Fill, Padding = new Padding(30) };
        _properties = new PropertyGrid { Dock = DockStyle.Right, Width = 310 };
        controls.SelectedIndexChanged += (_, _) =>
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
        if (controls.Items.Count > 0) controls.SelectedIndex = 0;
        SchoolUi.Apply(this);
    }
}