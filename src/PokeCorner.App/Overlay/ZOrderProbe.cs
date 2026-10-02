using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PokeCorner.App.Overlay;

/// Reads where the pet actually sits in the z-order, for <see cref="ZOrderLog"/> (POKE-9).
/// "Covered" means a visible window of another process sits ABOVE the pet and overlaps its
/// rectangle — the user-visible symptom — not merely that some window is higher in the list.
public static class ZOrderProbe
{
    /// The state line to log, e.g. "topmost=True fallbacks=0 covered=[]". The shell's taskbars
    /// are left out of "covered": the pet sits below them on purpose.
    public static string Describe(IntPtr petHwnd)
    {
        if (petHwnd == IntPtr.Zero || !IsWindowVisible(petHwnd)) return "hidden";

        var topmost = (NativeMethods.GetWindowLong(petHwnd, GWL_EXSTYLE) & WS_EX_TOPMOST) != 0;
        NativeMethods.GetWindowRect(petHwnd, out var pet);
        var ownPid = (uint)Environment.ProcessId;
        var covering = new List<string>();

        // EnumWindows walks top-level windows from the top of the z-order down, so everything
        // seen before the pet is above it.
        EnumWindows((hwnd, _) =>
        {
            if (hwnd == petHwnd) return false;
            if (!IsWindowVisible(hwnd) || IsCloaked(hwnd)) return true;

            NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == ownPid) return true;

            var className = NativeMethods.ClassName(hwnd);
            if (className is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return true;

            if (!NativeMethods.GetWindowRect(hwnd, out var r) || r.Right <= pet.Left || r.Left >= pet.Right || r.Bottom <= pet.Top || r.Top >= pet.Bottom) return true;

            var otherTopmost = (NativeMethods.GetWindowLong(hwnd, GWL_EXSTYLE) & WS_EX_TOPMOST) != 0;
            covering.Add($"{className}({ProcessName(pid)}{(otherTopmost ? ",topmost" : "")})");
            return true;
        }, IntPtr.Zero);

        return $"topmost={topmost} fallbacks={OverlayWindow.TopmostFallbackCount} covered=[{string.Join("; ", covering)}]";
    }

    /// What the user was working in at that moment — the detail half of a log line.
    public static string ForegroundDescription()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return "fg=none";
        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        return $"fg={NativeMethods.ClassName(hwnd)}({ProcessName(pid)})";
    }

    private static string ProcessName(uint pid)
    {
        try { using var p = Process.GetProcessById((int)pid); return p.ProcessName; }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return pid.ToString(); }
    }

    /// UWP/suspended windows report as visible while DWM is not drawing them at all.
    private static bool IsCloaked(IntPtr hwnd) =>
        DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out var cloaked, sizeof(int)) == 0 && cloaked != 0;


    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOPMOST = 0x00000008;
    private const int DWMWA_CLOAKED = 14;


    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);
}
