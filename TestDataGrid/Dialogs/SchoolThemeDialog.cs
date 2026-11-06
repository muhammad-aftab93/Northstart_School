using EnterpriseUI.Theming;
using EnterpriseUI.Abstractions.Theming;
using System.Text.Json;
using System.Text.Json.Serialization;
using TestDataGrid.Views;

namespace TestDataGrid.Dialogs;

public sealed class SchoolThemeDialog : Form
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    private sealed class ThemeConfiguration
    {
        public string Name { get; set; } = "School Theme";
        public ThemeType Type { get; set; }
        public ColorScheme Scheme { get; set; }
        public Dictionary<string, string> Tokens { get; set; } = new();
    }

    private static ThemeConfiguration Parse(string json)
    {
        if (json.Length > 262144) throw new InvalidDataException("Theme configuration is too large.");
        var config = JsonSerializer.Deserialize<ThemeConfiguration>(json, Options) ?? throw new InvalidDataException("Theme configuration is empty.");
        if (string.IsNullOrWhiteSpace(config.Name) || !Enum.IsDefined(config.Type) || !Enum.IsDefined(config.Scheme) || config.Tokens == null || config.Tokens.Count is < 1 or > 1024
            || config.Tokens.Any(token => string.IsNullOrWhiteSpace(token.Key) || string.IsNullOrWhiteSpace(token.Value)))
            throw new InvalidDataException("Theme name, type, scheme and non-empty tokens are required.");
        return config;
    }

    public SchoolThemeDialog()
    {
        Text = "Theme Editor"; ClientSize = new Size(720, 650); StartPosition = FormStartPosition.CenterParent;
        Font = SchoolUi.BodyFont;
        var active = ThemeManager.Instance.CurrentTheme;
        var editor = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font("Consolas", 10f), Text = JsonSerializer.Serialize(new ThemeConfiguration { Name = active.Name, Type = active.Type, Scheme = active.Scheme, Tokens = new Dictionary<string, string>(active.DesignTokens) }, Options) };
        var toolbar = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden };
        var status = new Label { Dock = DockStyle.Bottom, Height = 40, AutoEllipsis = true, Padding = new Padding(12, 0, 12, 0) };
        void Command(string title, Action action)
        {
            var button = new ToolStripButton(title);
            button.Click += (_, _) => { try { action(); status.Text = "Theme configuration updated."; } catch (Exception exception) { status.Text = exception.Message; } };
            toolbar.Items.Add(button);
        }
        Command("Apply Preview", () => { var config = Parse(editor.Text); ThemeManager.Instance.ApplyTheme(new Theme(config.Name, config.Type, config.Scheme, config.Tokens)); SchoolUi.Apply(this); });
        Command("Import", () => { using var dialog = new OpenFileDialog { Filter = "Theme JSON|*.json" }; if (dialog.ShowDialog(this) == DialogResult.OK) editor.Text = File.ReadAllText(dialog.FileName); });
        Command("Export", () => { Parse(editor.Text); using var dialog = new SaveFileDialog { Filter = "Theme JSON|*.json", FileName = "school-theme.json" }; if (dialog.ShowDialog(this) == DialogResult.OK) File.WriteAllText(dialog.FileName, editor.Text); });
        Controls.Add(editor); Controls.Add(status); Controls.Add(toolbar); SchoolUi.Apply(this);
    }
}