using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace PokeCorner.App.Fullscreen;

public sealed class ForegroundWindowMonitor : IDisposable
{
    private readonly Action _onFullscreenEntered;
    private readonly Action _onFullscreenExited;

    /// The pet's own window handle, read fresh on every poll rather than captured once: the
    /// pet can be dragged to another monitor at any time, and the answer to "does this
    /// fullscreen app actually cover the pet" has to follow it there.
    private readonly Func<IntPtr> _petWindowHandle;

    private readonly DispatcherTimer _pollTimer;
    private bool _wasHiding;

    public ForegroundWindowMonitor(Action onFullscreenEntered, Action onFullscreenExited, Func<IntPtr> petWindowHandle)
    {
        _onFullscreenEntered = onFullscreenEntered;
        _onFullscreenExited = onFullscreenExited;
        _petWindowHandle = petWindowHandle;
        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _pollTimer.Tick += (_, _) => Poll();
    }

    public void Start() => _pollTimer.Start();
    public void Stop() => _pollTimer.Stop();
    public void Dispose() => Stop();

    /// The tracked state is "a fullscreen app is covering THE PET", not merely "something
    /// somewhere is fullscreen". Because that is re-evaluated on every 500ms tick rather than
    /// latched when the app went fullscreen, dragging the pet ONTO an already-fullscreen
    /// monitor (or off one) flips it on the next tick, with no extra wiring.
    private void Poll()
    {
        var isHiding = IsPetCoveredByFullscreenWindow();
        if (isHiding && !_wasHiding) _onFullscreenEntered();
        else if (!isHiding && _wasHiding) _onFullscreenExited();
        _wasHiding = isHiding;
    }

    private bool IsPetCoveredByFullscreenWindow()
    {
        var foreground = NativeMethods.GetForegroundWindow();
        if (foreground == IntPtr.Zero) return false;

        // The shell's own desktop/taskbar windows report as borderless full-monitor
        // too; exclude them by class name so the pet never hides on an empty desktop.
        var className = NativeMethods.ClassName(foreground);
        if (className is "Progman" or "WorkerW" or "Shell_TrayWnd") return false;

        if (!NativeMethods.GetWindowRect(foreground, out var windowRect)) return false;

        var monitorHandle = MonitorFromWindow(foreground, MONITOR_DEFAULTTONEAREST);
        var monitorInfo = MONITORINFO.Create();
        if (!GetMonitorInfo(monitorHandle, ref monitorInfo)) return false;

        int style = NativeMethods.GetWindowLong(foreground, GWL_STYLE);
        var hasBorder = (style & (WS_CAPTION | WS_THICKFRAME)) != 0;

        var windowScreenRect = new ScreenRect(windowRect.Left, windowRect.Top, windowRect.Right - windowRect.Left, windowRect.Bottom - windowRect.Top);
        var monitorScreenRect = new ScreenRect(
            monitorInfo.rcMonitor.Left, monitorInfo.rcMonitor.Top,
            monitorInfo.rcMonitor.Right - monitorInfo.rcMonitor.Left,
            monitorInfo.rcMonitor.Bottom - monitorInfo.rcMonitor.Top);

        if (!FullscreenDetector.IsFullscreen(windowScreenRect, monitorScreenRect, hasBorder)) return false;

        // A fullscreen app only hides anything on ITS OWN monitor. Compared by monitor HANDLES
        // (MonitorFromWindow for both windows): the same rule and units on both sides, no
        // DIP/physical conversion to get wrong, and no reliance on the MonitorIndex setting — a
        // dragged pet may be on another monitor.
        var petWindow = _petWindowHandle();

        // No pet window yet (polling can start before the overlay is sourced): hide anyway rather
        // than risk the pet over a fullscreen app.
        if (petWindow == IntPtr.Zero) return true;

        return MonitorFromWindow(petWindow, MONITOR_DEFAULTTONEAREST) == monitorHandle;
    }


    private const int GWL_STYLE = -16;
    private const int WS_CAPTION = 0x00C00000;
    private const int WS_THICKFRAME = 0x00040000;
    private const uint MONITOR_DEFAULTTONEAREST = 2;


    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public uint cbSize;
        public NativeMethods.RECT rcMonitor;
        public NativeMethods.RECT rcWork;
        public uint dwFlags;

        public static MONITORINFO Create() => new() { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
    }

    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint dwFlags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
}
