using System.Runtime.InteropServices;

namespace PokeCorner.App.Input;

public sealed class GlobalInputHook : IDisposable
{
    private const int WH_MOUSE_LL = 14;
    private const int WH_KEYBOARD_LL = 13;

    // WM_* message identifiers, used only to tell a discrete click/scroll apart from
    // continuous mouse-move noise — this is not a privacy-sensitive read: it reveals
    // only "what kind of event this is" (down/up/wheel), never a position, a key, or
    // any content. WH_MOUSE_LL fires on every pixel of movement, so counting it
    // unfiltered inflates activity by orders of magnitude (a few centimeters of mouse
    // travel would otherwise look like hours of "clicking").
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_MBUTTONDOWN = 0x0207;
    private const int WM_XBUTTONDOWN = 0x020B;
    private const int WM_MOUSEWHEEL = 0x020A;

    private readonly InputEventCounter _counter;
    private readonly LowLevelProc _mouseProc;
    private readonly LowLevelProc _keyboardProc;
    private IntPtr _mouseHookHandle;
    private IntPtr _keyboardHookHandle;

    private delegate IntPtr LowLevelProc(int nCode, IntPtr wParam, IntPtr lParam);

    public GlobalInputHook(InputEventCounter counter)
    {
        _counter = counter;
        // Keep delegate instances alive for the hook's lifetime — the CLR would
        // otherwise be free to collect them since SetWindowsHookEx only holds a
        // native function pointer, not a managed reference.
        _mouseProc = MouseHookCallback;
        _keyboardProc = KeyboardHookCallback;
    }

    public void Start()
    {
        using var currentModule = System.Diagnostics.Process.GetCurrentProcess().MainModule!;
        var moduleHandle = GetModuleHandle(currentModule.ModuleName);

        _mouseHookHandle = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, moduleHandle, 0);
        _keyboardHookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, moduleHandle, 0);
    }

    public void Stop()
    {
        if (_mouseHookHandle != IntPtr.Zero) UnhookWindowsHookEx(_mouseHookHandle);
        if (_keyboardHookHandle != IntPtr.Zero) UnhookWindowsHookEx(_keyboardHookHandle);
        _mouseHookHandle = IntPtr.Zero;
        _keyboardHookHandle = IntPtr.Zero;
    }

    public void Dispose() => Stop();

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var message = wParam.ToInt32();
            if (message is WM_LBUTTONDOWN or WM_RBUTTONDOWN or WM_MBUTTONDOWN or WM_XBUTTONDOWN or WM_MOUSEWHEEL)
            {
                _counter.RecordEvent(); // count only — never inspect lParam (position/key content).
            }
        }
        return CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0) _counter.RecordEvent(); // count only — never read the key code.
        return CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);
}
