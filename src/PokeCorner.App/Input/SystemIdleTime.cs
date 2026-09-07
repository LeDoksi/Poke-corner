using System.Runtime.InteropServices;

namespace PokeCorner.App.Input;

/// Wraps GetLastInputInfo — the same OS-maintained value Windows itself uses to decide when
/// to blank the screen or lock the session. It is a plain read of state Windows already
/// tracks for every process; it does not hook, subscribe to, or otherwise touch any other
/// process, which is exactly why FallbackActivityMonitor uses it as its "is the player
/// actually at the PC" signal — see that class for why a fallback is needed at all.
public static class SystemIdleTime
{
    public static TimeSpan GetTimeSinceLastInput()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref info))
        {
            // Fails closed as "long idle", never "just active" — an unexpected API failure
            // must not look like fresh input, or FallbackActivityMonitor would credit activity
            // forever regardless of what the player is actually doing.
            return TimeSpan.MaxValue;
        }

        // Both dwTime and GetTickCount() are 32-bit millisecond counts that wrap every ~49.7
        // days; unsigned subtraction is the standard idiom that stays correct across a wrap.
        var idleMs = unchecked(GetTickCount() - info.dwTime);
        return TimeSpan.FromMilliseconds(idleMs);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [DllImport("kernel32.dll")]
    private static extern uint GetTickCount();
}
