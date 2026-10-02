namespace PokeCorner.App.Input;

/// Some kernel anti-cheat drivers (Genshin Impact's mhyprot2.sys is the reported case) stop
/// other processes' WH_MOUSE_LL/WH_KEYBOARD_LL hooks from firing system-wide while loaded —
/// even in the launcher. GlobalInputHook then sees nothing, and every activity-driven system
/// (care points, treats, coins) stalls while the player is actively using the PC.
///
/// GetLastInputInfo is not a hook — it's a value Windows already tracks for idle detection —
/// so it is used as a fallback: credit only when the hook has gone quiet AND the OS saw input
/// moments ago. With the hook working it never goes quiet, so there is no double counting.
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
