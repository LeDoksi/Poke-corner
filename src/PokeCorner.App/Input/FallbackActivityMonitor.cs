namespace PokeCorner.App.Input;

/// Genshin Impact ships a kernel-mode anti-cheat driver (mhyprot2.sys) that is widely and
/// independently reported (outside this codebase) to interfere with other processes'
/// SetWindowsHookEx(WH_MOUSE_LL/WH_KEYBOARD_LL) low-level hooks system-wide while it is
/// loaded — including in the launcher, before the game process itself has even started. When
/// that happens, GlobalInputHook's callback simply stops being invoked: InputEventCounter
/// keeps returning zero units forever, and every activity-driven system (CarePoints, treats,
/// coins) silently stalls even though the player is actively using their PC. See
/// GlobalInputHook.cs and GamepadInputHook.cs for the same "document the lesson" convention.
///
/// SystemIdleTime.GetTimeSinceLastInput (GetLastInputInfo) is not a hook — it is a single
/// value Windows itself already tracks for idle/screensaver purposes, reading it never
/// touches another process, so there is no reason for anti-cheat software to interfere with
/// it. It is used here purely as a fallback: only fires when GlobalInputHook has gone quiet
/// AND the OS reports the player was active moments ago. When the hook is working normally it
/// is already crediting real per-event units every tick, so it never goes quiet and this stays
/// fully dormant — no double counting in the normal case.
public static class FallbackActivityMonitor
{
    /// How long the hook must have been silent before it's treated as broken rather than "the
    /// player just hasn't clicked or typed in the last moment", which is entirely normal.
    public static readonly TimeSpan HookStaleThreshold = TimeSpan.FromSeconds(5);

    /// How recent the OS's last-input timestamp must be to count as "active right now" for
    /// this tick. Matches OnGrowthTick's own 1-second cadence with a little slack.
    public static readonly TimeSpan RecentOsInputWindow = TimeSpan.FromSeconds(2);

    public static bool ShouldCreditFallbackUnit(DateTimeOffset nowUtc, DateTimeOffset lastHookRecordedUtc, TimeSpan timeSinceLastOsInput)
    {
        var hookIsStale = nowUtc - lastHookRecordedUtc >= HookStaleThreshold;
        var osReportsRecentActivity = timeSinceLastOsInput <= RecentOsInputWindow;
        return hookIsStale && osReportsRecentActivity;
    }
}
