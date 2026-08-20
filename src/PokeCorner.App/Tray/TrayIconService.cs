using System.Drawing;
using System.Windows.Forms;

namespace PokeCorner.App.Tray;

public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly Icon _activeIcon;
    private readonly Icon _inactiveIcon;

    public event Action? OpenCollectionRequested;
    public event Action? OpenSettingsRequested;
    public event Action? ExitRequested;

    public TrayIconService()
    {
        _activeIcon = SystemIcons.Application;
        _inactiveIcon = SystemIcons.Application;
        // Task 9 replaces both icons with the app's real tray art (a plain sprite icon,
        // and a dimmed/badged variant for when activity tracking is off) once assets exist.

        var menu = new ContextMenuStrip();
        menu.Items.Add("Коллекция", null, (_, _) => OpenCollectionRequested?.Invoke());
        menu.Items.Add("Настройки", null, (_, _) => OpenSettingsRequested?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Выход", null, (_, _) => ExitRequested?.Invoke());

        _notifyIcon = new NotifyIcon
        {
            Icon = _activeIcon,
            Visible = true,
            Text = "PokéCorner",
            ContextMenuStrip = menu
        };
        _notifyIcon.DoubleClick += (_, _) => OpenCollectionRequested?.Invoke();
    }

    public void SetActivityTrackingIndicator(bool isActive)
    {
        _notifyIcon.Icon = isActive ? _activeIcon : _inactiveIcon;
        _notifyIcon.Text = isActive ? "PokéCorner — трекинг активности включён" : "PokéCorner — трекинг активности выключен";
    }

    public void ShowNotification(string title, string text) =>
        _notifyIcon.ShowBalloonTip(timeout: 5000, tipTitle: title, tipText: text, tipIcon: ToolTipIcon.Info);

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
