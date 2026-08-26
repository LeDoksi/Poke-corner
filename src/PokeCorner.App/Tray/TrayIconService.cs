using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PokeCorner.App.Tray;

public sealed class TrayIconService : IDisposable
{
    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    private readonly NotifyIcon _notifyIcon;
    private Icon _currentSpriteIcon;
    private Icon? _currentDesaturatedIcon;
    private bool _isActivityTrackingIndicatorOn = true;
    private bool? _appliedIndicatorState;

    public event Action? OpenCollectionRequested;
    public event Action? OpenSettingsRequested;
    public event Action? OpenShopRequested;
    public event Action? CatchRequested;
    public event Action? ExitRequested;

    private readonly ToolStripMenuItem _catchMenuItem;

    public TrayIconService()
    {
        _currentSpriteIcon = DrawPlaceholderPokeBallIcon();

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
            Icon = _currentSpriteIcon,
            Visible = true,
            Text = "PokéCorner",
            ContextMenuStrip = menu
        };
        _notifyIcon.DoubleClick += (_, _) => OpenCollectionRequested?.Invoke();
    }

    /// Called from App.xaml.cs right where the overlay's sprite PNG bytes are already
    /// decoded — the tray icon always matches whoever is currently on the desktop.
    public void SetSpriteIcon(byte[] pngBytes)
    {
        using var stream = new MemoryStream(pngBytes);
        using var bitmap = new Bitmap(stream);
        var newIcon = IconFromBitmap(bitmap);

        _currentSpriteIcon.Dispose();
        _currentSpriteIcon = newIcon;
        _notifyIcon.Icon = _isActivityTrackingIndicatorOn ? _currentSpriteIcon : GetOrCreateDesaturatedIcon();
    }

    /// Returns the desaturated view of _currentSpriteIcon, regenerating and caching it in
    /// _currentDesaturatedIcon. NotifyIcon.Icon's setter never disposes the icon it
    /// replaces, so handing it a freshly-allocated Icon on every call (as DesaturateIcon
    /// alone would) abandons the previous one with no explicit Dispose() — relying on the
    /// CLR finalizer to eventually reclaim it, which this continuously-running tray app
    /// can't rely on. Caching the one currently in play here, and disposing it before
    /// replacing it, keeps this on the same explicit dispose-before-replace footing as
    /// _currentSpriteIcon.
    private Icon GetOrCreateDesaturatedIcon()
    {
        _currentDesaturatedIcon?.Dispose();
        _currentDesaturatedIcon = DesaturateIcon(_currentSpriteIcon);
        return _currentDesaturatedIcon;
    }

    /// Converts a Bitmap to an Icon that fully owns its native resources.
    /// Icon.FromHandle(bitmap.GetHicon()) alone does NOT take ownership of the HICON it
    /// wraps (per Microsoft's own docs), so disposing that Icon never releases the native
    /// handle — a slow GDI handle leak in a tray app that swaps its icon repeatedly over
    /// hours/days. Cloning the FromHandle-wrapped icon produces a GDI+-owned copy whose
    /// Dispose() genuinely releases its resources, and DestroyIcon frees the original
    /// handle that FromHandle never took ownership of.
    private static Icon IconFromBitmap(Bitmap bitmap)
    {
        var hIcon = bitmap.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(hIcon);
            return (Icon)temp.Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    private static Icon DrawPlaceholderPokeBallIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.FillPie(Brushes.Red, 1, 1, 30, 30, 180, 180);
            g.FillPie(Brushes.White, 1, 1, 30, 30, 0, 180);
            g.FillRectangle(Brushes.Black, 1, 14, 30, 4);
            g.FillEllipse(Brushes.White, 12, 12, 8, 8);
            g.DrawEllipse(Pens.Black, 12, 12, 8, 8);
            g.DrawEllipse(Pens.Black, 1, 1, 30, 30);
        }
        return IconFromBitmap(bitmap);
    }

    private static Icon DesaturateIcon(Icon source)
    {
        using var bitmap = source.ToBitmap();
        var matrix = new System.Drawing.Imaging.ColorMatrix(new float[][]
        {
            new float[] { 0.3f, 0.3f, 0.3f, 0, 0 },
            new float[] { 0.59f, 0.59f, 0.59f, 0, 0 },
            new float[] { 0.11f, 0.11f, 0.11f, 0, 0 },
            new float[] { 0, 0, 0, 1, 0 },
            new float[] { 0, 0, 0, 0, 1 }
        });
        using var attributes = new System.Drawing.Imaging.ImageAttributes();
        attributes.SetColorMatrix(matrix);
        using var result = new Bitmap(bitmap.Width, bitmap.Height);
        using (var g = Graphics.FromImage(result))
        {
            g.DrawImage(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height), 0, 0, bitmap.Width, bitmap.Height, GraphicsUnit.Pixel, attributes);
        }
        return IconFromBitmap(result);
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
        _isActivityTrackingIndicatorOn = isActive;

        // Called from App's settings reconcile, which runs on every growth tick — now once a
        // second rather than once every five. Redoing the work when nothing changed meant
        // rebuilding (and re-leaking, before GetOrCreateDesaturatedIcon existed) a GDI icon
        // every tick for no visible difference. Nullable so the very first call always applies.
        if (_appliedIndicatorState == isActive) return;
        _appliedIndicatorState = isActive;

        _notifyIcon.Icon = isActive ? _currentSpriteIcon : GetOrCreateDesaturatedIcon();
        _notifyIcon.Text = isActive ? "PokéCorner — трекинг активности включён" : "PokéCorner — трекинг активности выключен";
    }

    public void ShowNotification(string title, string text) =>
        _notifyIcon.ShowBalloonTip(timeout: 5000, tipTitle: title, tipText: text, tipIcon: ToolTipIcon.Info);

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _currentSpriteIcon.Dispose();
        _currentDesaturatedIcon?.Dispose();
    }
}
