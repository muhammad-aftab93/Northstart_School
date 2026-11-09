using System.ComponentModel;
using EnterpriseUI.Theming;
using EnterpriseUI.WinForms;
using TestDataGrid.Models;
using TestDataGrid.Services;

namespace TestDataGrid.Views;

public sealed class ThemeNameConverter : StringConverter
{
    public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;
    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context) => true;
    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context) => new(ThemeManager.Instance.AvailableThemes.Select(theme => theme.Name).ToArray());
}

/// <summary>What the property inspector shows on the settings page: the theme switch and read-only facts about the data.</summary>
public sealed class WorkspacePreferences
{
    private readonly SchoolStore _store;
    private readonly SchoolSettings _settings;
    public WorkspacePreferences(SchoolStore store, SchoolSettings settings) { _store = store; _settings = settings; }

    [Category("Appearance"), DisplayName("Theme"), Description("Color theme of every screen. The change applies immediately."), TypeConverter(typeof(ThemeNameConverter))]
    public string Theme
    {
        get => _settings.Theme;
        set { ThemeManager.Instance.ApplyTheme(value); _settings.Theme = value; _store.Transaction(data => data.Settings.Theme = value); }
    }

    [Category("Data"), DisplayName("Data file"), Description("Where the school records are stored."), ReadOnly(true)]
    public string DataFile => _store.FilePath;

    [Category("Data"), Description("Students on record."), ReadOnly(true)]
    public int Students => _store.Data.Students.Count;

    [Category("Data"), Description("Staff members on record."), ReadOnly(true)]
    public int Staff => _store.Data.Staff.Count;

    [Category("Data"), Description("Courses on record."), ReadOnly(true)]
    public int Courses => _store.Data.Courses.Count;

    [Category("Data"), Description("Invoices on record."), ReadOnly(true)]
    public int Invoices => _store.Data.Invoices.Count;
}

public sealed class SettingsView : UserControl
{
    private readonly SchoolStore _store;
    private readonly EnterpriseDataEntryControl _entry;
    private readonly EnterprisePropertyGridControl _preferences;
    public SettingsView(SchoolStore store)
    {
        _store = store; Dock = DockStyle.Fill; Font = SchoolUi.BodyFont;
        var settings = SchoolStore.Clone(store.Data.Settings);
        _entry = new EnterpriseDataEntryControl { Dock = DockStyle.Fill, Entity = settings, ApplyText = "Save settings", ResetText = "Reset", ColumnWidth = 300, AccessibleName = "School settings" };
        _preferences = new EnterprisePropertyGridControl { Dock = DockStyle.Fill, SelectedObject = new WorkspacePreferences(store, settings), ShowToolbar = false, AccessibleName = "Workspace preferences" };
        _entry.Applied += (_, _) => _store.Transaction(data => data.Settings = SchoolStore.Clone((SchoolSettings)_entry.Entity!));

        var split = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58)); split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        var left = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 0, 8, 12) };
        var right = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 0, 16, 12) };
        split.Controls.Add(left, 0, 0); split.Controls.Add(right, 1, 0);
        left.Controls.Add(_entry);
        left.Controls.Add(new Label { Text = "School profile", Dock = DockStyle.Top, Height = 30, Font = new Font("Segoe UI", 11f, FontStyle.Bold) });
        right.Controls.Add(_preferences);
        right.Controls.Add(new Label { Text = "Workspace", Dock = DockStyle.Top, Height = 30, Font = new Font("Segoe UI", 11f, FontStyle.Bold) });

        var toolbar = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(18, 8, 8, 8) };
        void Command(string title, Action action)
        {
            var button = new ToolStripButton(title);
            button.Click += (_, _) => { try { action(); } catch (Exception exception) { MessageBox.Show(FindForm(), exception.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error); } };
            toolbar.Items.Add(button);
        }
        Command("Theme Editor", () => { using var dialog = new Dialogs.SchoolThemeDialog(); dialog.ShowDialog(FindForm()); });
        Command("Backup", () => { using var dialog = new SaveFileDialog { Filter = "School backup|*.json", FileName = "northstar-backup.json" }; if (dialog.ShowDialog(FindForm()) == DialogResult.OK) _store.Backup(dialog.FileName); });
        Command("Restore", () =>
        {
            using var dialog = new OpenFileDialog { Filter = "School backup|*.json" };
            if (dialog.ShowDialog(FindForm()) == DialogResult.OK && MessageBox.Show(FindForm(), "Replace current school records with this backup?", "Restore Backup", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
            {
                _store.Restore(dialog.FileName);
                if (ThemeManager.Instance.AvailableThemes.Any(theme => theme.Name == _store.Data.Settings.Theme)) ThemeManager.Instance.ApplyTheme(_store.Data.Settings.Theme);
                var fresh = SchoolStore.Clone(_store.Data.Settings);
                _entry.Entity = fresh;
                _preferences.SelectedObject = new WorkspacePreferences(_store, fresh);
            }
        });
        Command("Open Data Folder", () => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Path.GetDirectoryName(_store.FilePath)!) { UseShellExecute = true }));
        Controls.Add(split); Controls.Add(toolbar); Controls.Add(SchoolUi.Heading("School Settings")); SchoolUi.Apply(this);
    }

    public EnterpriseDataEntryControl Entry => _entry;
    public EnterprisePropertyGridControl Preferences => _preferences;
}
