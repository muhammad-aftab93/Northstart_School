using EnterpriseUI.Theming;
using TestDataGrid.Services;

namespace TestDataGrid.Views;

public sealed class SettingsView : UserControl
{
    private readonly SchoolStore _store;
    public SettingsView(SchoolStore store)
    {
        _store = store; Dock = DockStyle.Fill; Font = SchoolUi.BodyFont;
        var settings = new PropertyGrid { Dock = DockStyle.Fill, SelectedObject = SchoolStore.Clone(store.Data.Settings), HelpVisible = true };
        var toolbar = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(18, 8, 8, 8) };
        void Command(string title, Action action)
        {
            var button = new ToolStripButton(title);
            button.Click += (_, _) => { try { action(); } catch (Exception exception) { MessageBox.Show(FindForm(), exception.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error); } };
            toolbar.Items.Add(button);
        }
        Command("Save Settings", () =>
        {
            var value = (Models.SchoolSettings)settings.SelectedObject;
            var previous = ThemeManager.Instance.CurrentTheme;
            try { ThemeManager.Instance.ApplyTheme(value.Theme); _store.Transaction(data => data.Settings = SchoolStore.Clone(value)); }
            catch { ThemeManager.Instance.ApplyTheme(previous); throw; }
        });
        Command("Theme Editor", () => { using var dialog = new Dialogs.SchoolThemeDialog(); dialog.ShowDialog(FindForm()); });
        Command("Backup", () => { using var dialog = new SaveFileDialog { Filter = "School backup|*.json", FileName = "northstar-backup.json" }; if (dialog.ShowDialog(FindForm()) == DialogResult.OK) _store.Backup(dialog.FileName); });
        Command("Restore", () =>
        {
            using var dialog = new OpenFileDialog { Filter = "School backup|*.json" };
            if (dialog.ShowDialog(FindForm()) == DialogResult.OK && MessageBox.Show(FindForm(), "Replace current school records with this backup?", "Restore Backup", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
            { _store.Restore(dialog.FileName); settings.SelectedObject = SchoolStore.Clone(_store.Data.Settings); if (ThemeManager.Instance.AvailableThemes.Any(theme => theme.Name == _store.Data.Settings.Theme)) ThemeManager.Instance.ApplyTheme(_store.Data.Settings.Theme); }
        });
        Command("Open Data Folder", () => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Path.GetDirectoryName(_store.FilePath)!) { UseShellExecute = true }));
        var themes = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
        themes.Items.AddRange(ThemeManager.Instance.AvailableThemes.Select(theme => (object)theme.Name).ToArray());
        themes.SelectedItem = store.Data.Settings.Theme;
        themes.SelectedIndexChanged += (_, _) => { if (themes.SelectedItem is string name) { ThemeManager.Instance.ApplyTheme(name); ((Models.SchoolSettings)settings.SelectedObject).Theme = name; SchoolUi.Apply(this); } };
        toolbar.Items.Add(new ToolStripLabel("Theme")); toolbar.Items.Add(new ToolStripControlHost(themes));
        Controls.Add(settings); Controls.Add(toolbar); Controls.Add(SchoolUi.Heading("School Settings")); SchoolUi.Apply(this);
    }
}