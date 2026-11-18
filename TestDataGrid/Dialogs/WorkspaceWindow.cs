using EnterpriseUI.WinForms;

namespace TestDataGrid.Dialogs;

/// <summary>Tabbed window that shows several screens side by side in tabs; a tab dragged out opens in its own window (for example on a second monitor).</summary>
public sealed class WorkspaceWindow : EnterpriseTabbedForm
{
    private readonly MainForm _main;

    public WorkspaceWindow(MainForm main)
    {
        _main = main;
        Text = main.Store.Data.Settings.SchoolName + " \u00b7 Workspace";
        Icon = main.Icon;
        ClientSize = new Size(1150, 740);
        StartPosition = FormStartPosition.CenterParent;
        Owner = main;
        TabAdding += (_, e) => { e.Cancel = true; ShowPageMenu(); };
    }

    /// <summary>Selects the tab of a screen, or opens it in a new tab.</summary>
    public EnterpriseTabbedFormTab OpenPage(string page)
    {
        var existing = Tabs.FirstOrDefault(tab => tab.Tag is string name && name == page);
        if (existing != null)
        {
            SelectedTab = existing;
            return existing;
        }

        var tab = AddTab(page, _main.CreatePage(page));
        tab.Tag = page;
        return tab;
    }

    private void ShowPageMenu()
    {
        var menu = new ContextMenuStrip { ShowImageMargin = false };
        foreach (var page in _main.PageNames.Where(name => name != "Control Explorer")) menu.Items.Add(page, null, (_, _) => OpenPage(page));
        menu.Closed += (_, _) => BeginInvoke(new Action(menu.Dispose));
        var anchor = GetNewTabBounds();
        menu.Show(TabStrip, new Point(anchor.Left, anchor.Bottom));
    }

    protected override EnterpriseTabbedForm CreateDetachedForm() => new WorkspaceWindow(_main) { CloseFormWithLastTab = true };
}
