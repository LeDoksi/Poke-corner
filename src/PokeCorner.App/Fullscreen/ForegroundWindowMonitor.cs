using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace PokeCorner.App.Fullscreen;

public sealed class ForegroundWindowMonitor : IDisposable
{
    private readonly Action _onFullscreenEntered;
    private readonly Action _onFullscreenExited;
    private readonly DispatcherTimer _pollTimer;
    private bool _wasFullscreen;

    public ForegroundWindowMonitor(Action onFullscreenEntered, Action onFullscreenExited)
    {
        _onFullscreenEntered = onFullscreenEntered;
        _onFullscreenExited = onFullscreenExited;
        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _pollTimer.Tick += (_, _) => Poll();
    }

    public void Start() => _pollTimer.Start();
    public void Stop() => _pollTimer.Stop();
    public void Dispose() => Stop();

    private void Poll()
    {
        var isFullscreen = IsForegroundWindowFullscreen();
        if (isFullscreen && !_wasFullscreen) _onFullscreenEntered();
        else if (!isFullscreen && _wasFullscreen) _onFullscreenExited();
        _wasFullscreen = isFullscreen;
    }

    private static bool IsForegroundWindowFullscreen()
    {
        var foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero) return false;

        // The shell's own desktop/taskbar windows report as borderless full-monitor
        // too; exclude them by class name so the pet never hides on an empty desktop.
        var className = GetClassName(foreground);
        if (className is "Progman" or "WorkerW" or "Shell_TrayWnd") return false;

        if (!GetWindowRect(foreground, out var windowRect)) return false;

        var monitorHandle = MonitorFromWindow(foreground, MONITOR_DEFAULTTONEAREST);
        var monitorInfo = MONITORINFO.Create();
        if (!GetMonitorInfo(monitorHandle, ref monitorInfo)) return false;

        var style = GetWindowLong(foreground, GWL_STYLE);
        var hasBorder = (style & (WS_CAPTION | WS_THICKFRAME)) != 0;

        var windowScreenRect = new ScreenRect(windowRect.Left, windowRect.Top, windowRect.Right - windowRect.Left, windowRect.Bottom - windowRect.Top);
        var monitorScreenRect = new ScreenRect(
            monitorInfo.rcMonitor.Left, monitorInfo.rcMonitor.Top,
            monitorInfo.rcMonitor.Right - monitorInfo.rcMonitor.Left,
            monitorInfo.rcMonitor.Bottom - monitorInfo.rcMonitor.Top);

        return FullscreenDetector.IsFullscreen(windowScreenRect, monitorScreenRect, hasBorder);
    }

    private static string GetClassName(IntPtr hWnd)
    {
        var buffer = new System.Text.StringBuilder(256);
        GetClassName(hWnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    private const int GWL_STYLE = -16;
    private const long WS_CAPTION = 0x00C00000L;
    private const long WS_THICKFRAME = 0x00040000L;
    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;

        public static MONITORINFO Create() => new() { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
    }

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint dwFlags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
    [DllImport("user32.dll")] private static extern long GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);
}
