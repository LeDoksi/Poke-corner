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
    public event Action? OpenShopRequested;
    public event Action? CatchRequested;
    public event Action? ExitRequested;

    private readonly ToolStripMenuItem _catchMenuItem;

    public TrayIconService()
    {
        _activeIcon = SystemIcons.Application;
        _inactiveIcon = SystemIcons.Application;
        // Task 9 replaces both icons with the app's real tray art (a plain sprite icon,
        // and a dimmed/badged variant for when activity tracking is off) once assets exist.

        var menu = new ContextMenuStrip();
        menu.Items.Add("Коллекция", null, (_, _) => OpenCollectionRequested?.Invoke());
        menu.Items.Add("Настройки", null, (_, _) => OpenSettingsRequested?.Invoke());
        menu.Items.Add("Магазин", null, (_, _) => OpenShopRequested?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        _catchMenuItem = new ToolStripMenuItem("Поймать! 🎣", null, (_, _) => CatchRequested?.Invoke()) { Enabled = false };
        menu.Items.Add(_catchMenuItem);
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

    /// Toggles the "Поймать!" menu item and tray tooltip so the player can see a catch
    /// is ready and trigger it themselves, rather than it happening automatically —
    /// per explicit feedback, auto-catching on threshold felt like it took the moment
    /// away from the player.
    public void SetCatchReady(bool isReady)
    {
        _catchMenuItem.Enabled = isReady;
        _catchMenuItem.Font = isReady
            ? new System.Drawing.Font(_catchMenuItem.Font, System.Drawing.FontStyle.Bold)
            : new System.Drawing.Font(_catchMenuItem.Font, System.Drawing.FontStyle.Regular);
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
