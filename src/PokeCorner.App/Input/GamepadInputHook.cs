using System.Numerics;
using System.Runtime.InteropServices;

namespace PokeCorner.App.Input;

/// One XInput controller's state. Laid out to match XINPUT_GAMEPAD exactly so it can be
/// marshalled straight out of XInputGetState with no conversion step. Like the mouse/keyboard
/// hook, nothing here is recorded or inspected beyond "did something change" — the values live
/// only long enough to be diffed against the previous poll.
[StructLayout(LayoutKind.Sequential)]
public struct GamepadSnapshot
{
    public ushort Buttons;
    public byte LeftTrigger;
    public byte RightTrigger;
    public short ThumbLX;
    public short ThumbLY;
    public short ThumbRX;
    public short ThumbRY;
}

/// Gamepad activity source, feeding the same InputEventCounter as GlobalInputHook.
///
/// XInput has no callback mechanism — the only API is "give me the current state" — so this
/// polls. Two constraints shape it, both inherited from GlobalInputHook's hard-won lessons:
///
/// 1. Polling runs on a System.Threading.Timer (thread-pool), NEVER on the WPF dispatcher
///    thread. That thread owns the low-level mouse/keyboard hooks, and anything slow on it
///    freezes mouse input system-wide (see App.OnGrowthTick's comment). XInput imposes no
///    thread affinity, so there is no reason to go anywhere near that thread.
/// 2. Only DISCRETE transitions count. A held button, a tilted stick and a squeezed trigger are
///    continuous per-poll state — counting them every tick would inflate activity exactly the
///    way counting every WH_MOUSE_LL mouse-move would.
public sealed class GamepadInputHook : IDisposable
{
    // XInput's own documented defaults (XINPUT_GAMEPAD_*_DEADZONE / _TRIGGER_THRESHOLD).
    public const int LeftThumbDeadzone = 7849;
    public const int RightThumbDeadzone = 8689;
    public const int TriggerThreshold = 30;

    private const int MaxControllers = 4; // XInput supports dwUserIndex 0-3, full stop.
    private const int ErrorSuccess = 0;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(125);

    private static readonly XInputGetStateFn? Native = ResolveNative();

    private readonly InputEventCounter _counter;
    private readonly GamepadSnapshot[] _previous = new GamepadSnapshot[MaxControllers];
    private readonly bool[] _wasConnected = new bool[MaxControllers];
    private System.Threading.Timer? _timer;
    private int _polling;

    public GamepadInputHook(InputEventCounter counter) => _counter = counter;

    public void Start()
    {
        if (Native is null) return; // no XInput on this machine — silently no-op, not an error.
        _timer = new System.Threading.Timer(_ => Poll(), null, PollInterval, PollInterval);
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    public void Dispose() => Stop();

    /// How many discrete activity events the move from `previous` to `current` represents:
    /// one per button that went 0->1, plus one per trigger/stick that crossed from at-rest to
    /// engaged. Held-past-the-threshold across polls counts zero more times. Pure and static so
    /// it is testable with synthetic states, without a controller or the P/Invoke.
    public static int CountDiscreteEvents(in GamepadSnapshot previous, in GamepadSnapshot current)
    {
        var count = BitOperations.PopCount((uint)(ushort)(current.Buttons & ~previous.Buttons));

        if (current.LeftTrigger > TriggerThreshold && previous.LeftTrigger <= TriggerThreshold) count++;
        if (current.RightTrigger > TriggerThreshold && previous.RightTrigger <= TriggerThreshold) count++;

        if (IsEngaged(current.ThumbLX, current.ThumbLY, LeftThumbDeadzone)
            && !IsEngaged(previous.ThumbLX, previous.ThumbLY, LeftThumbDeadzone)) count++;
        if (IsEngaged(current.ThumbRX, current.ThumbRY, RightThumbDeadzone)
            && !IsEngaged(previous.ThumbRX, previous.ThumbRY, RightThumbDeadzone)) count++;

        return count;
    }

    /// Radial deadzone (magnitude, not per-axis) — matches how XInput's own docs describe the
    /// constants. Squared comparison in long arithmetic: two squared shorts overflow int.
    private static bool IsEngaged(short x, short y, int deadzone)
        => ((long)x * x) + ((long)y * y) >= (long)deadzone * deadzone;

    /// Reads one slot. Returns false for every non-success status, which includes the entirely
    /// normal ERROR_DEVICE_NOT_CONNECTED (1167) for an empty slot — the common case on a machine
    /// with no controller, so it is skipped silently rather than logged or thrown on.
    public static bool TryReadState(int userIndex, out GamepadSnapshot snapshot)
    {
        snapshot = default;
        if (Native is null) return false;
        if (Native(userIndex, out var state) != ErrorSuccess) return false;
        snapshot = state.Gamepad;
        return true;
    }

    private void Poll()
    {
        if (Interlocked.Exchange(ref _polling, 1) == 1) return; // a tick still running: skip this one.
        try
        {
            for (var i = 0; i < MaxControllers; i++)
            {
                if (!TryReadState(i, out var current))
                {
                    _wasConnected[i] = false;
                    _previous[i] = default;
                    continue;
                }

                // A slot seen for the first time only establishes a baseline: whatever is already
                // held at plug-in time was not pressed during this session.
                var units = _wasConnected[i] ? CountDiscreteEvents(_previous[i], current) : 0;
                _previous[i] = current;
                _wasConnected[i] = true;

                for (var e = 0; e < units; e++) _counter.RecordEvent();
            }
        }
        catch
        {
            // An unhandled exception on a thread-pool timer callback takes the whole process
            // down. Activity counting is never worth crashing the app over.
        }
        finally
        {
            Interlocked.Exchange(ref _polling, 0);
        }
    }

    /// xinput1_4 ships with Windows 8+; the older names cover machines that only have the
    /// DirectX-redist or Windows 7-era versions. A machine with none of them just gets no
    /// gamepad tracking instead of a DllNotFoundException at startup.
    private static XInputGetStateFn? ResolveNative()
    {
        foreach (var dll in new[] { "xinput1_4.dll", "xinput1_3.dll", "xinput9_1_0.dll" })
        {
            if (!NativeLibrary.TryLoad(dll, out var handle)) continue;
            if (NativeLibrary.TryGetExport(handle, "XInputGetState", out var export))
            {
                return Marshal.GetDelegateForFunctionPointer<XInputGetStateFn>(export);
            }
        }
        return null;
    }

    private delegate int XInputGetStateFn(int dwUserIndex, out XInputState pState);

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState
    {
        public uint PacketNumber;
        public GamepadSnapshot Gamepad;
    }
}
