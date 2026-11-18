using EnterpriseUI.Core.Mvvm;
using EnterpriseUI.Editors.Interactivity;
using EnterpriseUI.Navigation;
using EnterpriseUI.WinForms;

namespace TestDataGrid;

/// <summary>The workshop commands as a ribbon: every ribbon item runs the matching (hidden) toolbar item, so both always agree on what is enabled.</summary>
public partial class Form1
{
    private static readonly Dictionary<string, string> RibbonGlyphs = new()
    {
        ["Add"] = "\uFF0B", ["Edit"] = "\u270E", ["Delete"] = "\u2716", ["Undo"] = "\u21B6", ["Redo"] = "\u21B7",
        ["Save Changes"] = "\u2714", ["Discard"] = "\u2718", ["Refresh"] = "\u27F3", ["Cancel"] = "\u25A0"
    };

    private readonly List<(RibbonItem Item, ToolStripItem Source)> _ribbonLinks = new();
    private bool _editingTabShown;

    private DialogResult Notice(string message, string title, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1) =>
        EnterpriseMarkupDialog.ShowMarkup(message, title, buttons, icon, LabelMarkupMode.Plain, this, defaultButton);

    private RibbonItem Link(ToolStripItem source, bool large, string? glyph = null)
    {
        var item = new RibbonItem
        {
            Text = source.Text ?? string.Empty, IsLarge = large, Icon = glyph, ToolTip = source.ToolTipText, IsEnabled = source.Enabled,
            Command = new RelayCommand(_ => { source.PerformClick(); SyncRibbon(); }, _ => source.Enabled)
        };
        _ribbonLinks.Add((item, source));
        return item;
    }

    private static string QueryGroup(string text) =>
        text.StartsWith("Group by", StringComparison.Ordinal) || text.Contains("Groups", StringComparison.Ordinal) || text.Contains("Details", StringComparison.Ordinal) ? "Grouping"
        : text.Contains("Sort", StringComparison.Ordinal) ? "Sorting"
        : text.Contains("Preset", StringComparison.Ordinal) ? "Presets" : "Filtering";

    private void BuildRibbon()
    {
        var buttons = _commands.Items.OfType<ToolStripButton>().Where(button => !string.IsNullOrEmpty(button.Text)).ToDictionary(button => button.Text!);
        RibbonItem Button(string name, bool large = true) => Link(buttons[name], large, RibbonGlyphs.GetValueOrDefault(name));

        var home = new RibbonTab { Title = "Home" };
        var records = new RibbonGroup { Title = "Records" };
        foreach (var name in new[] { "Add", "Edit", "Delete" }) records.Items.Add(Button(name));
        var changes = new RibbonGroup { Title = "Changes" };
        foreach (var name in new[] { "Undo", "Redo", "Save Changes", "Discard" }) changes.Items.Add(Button(name));
        var data = new RibbonGroup { Title = "Data" };
        data.Items.Add(Button("Refresh"));
        data.Items.Add(Link(_cancel, false, RibbonGlyphs["Cancel"]));
        home.Groups.Add(records); home.Groups.Add(changes); home.Groups.Add(data);
        Ribbon.Tabs.Add(home);

        var tabs = new Dictionary<string, RibbonTab>();
        foreach (var menu in _commands.Items.OfType<ToolStripDropDownButton>())
        {
            var title = menu.Text is "Export" or "Print" ? "Output" : menu.Text ?? "More";
            if (!tabs.TryGetValue(title, out var tab)) { tabs[title] = tab = new RibbonTab { Title = title }; Ribbon.Tabs.Add(tab); }
            var groups = new Dictionary<string, RibbonGroup>();
            foreach (var entry in menu.DropDownItems.OfType<ToolStripMenuItem>())
            {
                var groupTitle = menu.Text == "Query" ? QueryGroup(entry.Text ?? string.Empty) : menu.Text ?? "More";
                if (!groups.TryGetValue(groupTitle, out var group)) { groups[groupTitle] = group = new RibbonGroup { Title = groupTitle }; tab.Groups.Add(group); }
                group.Items.Add(Link(entry, false, entry.CheckOnClick ? (entry.Checked ? "\u2611" : "\u2610") : "\u25B8"));
            }
        }

        var editing = new RibbonTab { Title = "Pending changes", ContextualGroup = "Editing", ContextualColor = "#C0504D" };
        var review = new RibbonGroup { Title = "Review" };
        foreach (var name in new[] { "Save Changes", "Discard", "Undo", "Redo" }) review.Items.Add(Button(name));
        editing.Groups.Add(review);
        Ribbon.Tabs.Add(editing);

        foreach (var name in new[] { "Undo", "Redo", "Save Changes" }) QuickAccessItems.Add(Button(name, false));

        _commands.Visible = false;
        Ribbon.RefreshRibbon();
    }

    /// <summary>Copies enabled and checked states from the toolbar items and shows the contextual tab while edits are pending.</summary>
    private void SyncRibbon()
    {
        if (_ribbonLinks.Count == 0 || IsDisposed) return;
        foreach (var (item, source) in _ribbonLinks)
        {
            item.IsEnabled = source.Enabled;
            if (source is ToolStripMenuItem { CheckOnClick: true } check) item.Icon = check.Checked ? "\u2611" : "\u2610";
        }

        var pending = dataGrid.HasPendingChanges;
        if (pending && !_editingTabShown) { _editingTabShown = true; Ribbon.ShowContextualGroup("Editing"); }
        else if (!pending && _editingTabShown) { _editingTabShown = false; Ribbon.HideContextualGroup("Editing"); }
        Ribbon.RefreshRibbon();
    }
}
