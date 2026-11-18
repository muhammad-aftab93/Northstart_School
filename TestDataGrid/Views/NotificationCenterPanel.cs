using EnterpriseUI.Editors.Interactivity;
using EnterpriseUI.WinForms;

namespace TestDataGrid.Views;

/// <summary>History of the tray notifications, newest first; Enter or a double-click opens the screen a notification belongs to.</summary>
public sealed class NotificationCenterPanel : UserControl
{
    private readonly NotificationCenter _center;
    private readonly Action<AppNotification> _open;
    private readonly EnterpriseListViewControl _list = new() { Dock = DockStyle.Fill, AccessibleName = "Notifications", EmptyText = "No notifications", SelectionMode = ListSelectionMode.One };

    public NotificationCenterPanel(NotificationCenter center, Action<AppNotification> open)
    {
        _center = center; _open = open; Font = SchoolUi.BodyFont;
        _list.AddColumn("Notification", 250); _list.AddColumn("When", 110);
        _list.ItemActivate += (_, item) => { if (item.Tag is AppNotification notification) _open(notification); };

        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 52, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10, 8, 10, 8) };
        var clear = new EnterpriseButtonControl { Text = "Clear all", Variant = EnterpriseButtonVariant.Outline, Width = 100, Height = 34 };
        var read = new EnterpriseButtonControl { Text = "Mark all read", Variant = EnterpriseButtonVariant.Outline, Width = 130, Height = 34 };
        clear.Click += (_, _) => _center.Clear();
        read.Click += (_, _) => _center.MarkAllRead();
        bar.Controls.Add(clear); bar.Controls.Add(read);

        Controls.Add(_list); Controls.Add(bar);
        _center.Changed += Changed;
        Reload();
        SchoolUi.Apply(this);
    }

    public EnterpriseListViewControl List => _list;

    private void Changed(object? sender, EventArgs e) => Reload();

    private void Reload()
    {
        _list.Items.Clear();
        foreach (var notification in _center.Items.OrderByDescending(item => item.Timestamp))
        {
            var title = (notification.IsRead ? string.Empty : "\u25CF ") + notification.Title + " \u2013 " + notification.Message;
            _list.Items.Add(title, notification.Timestamp.ToLocalTime().ToString("dd MMM HH:mm")).Tag = notification;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _center.Changed -= Changed;
        base.Dispose(disposing);
    }
}
