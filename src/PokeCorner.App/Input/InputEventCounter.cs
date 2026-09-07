namespace PokeCorner.App.Input;

public sealed class InputEventCounter
{
    private long _pendingUnits;

    // Ticks (UTC), not a plain DateTimeOffset field: Interlocked has no overload for that
    // type, and this is written from the hook callback thread while read from the dispatcher
    // thread on every growth tick. Starts "now" rather than default/null so a fresh app launch
    // doesn't look like the hook has already gone stale before it even got a chance to fire.
    private long _lastHookRecordedTicks = DateTimeOffset.UtcNow.UtcTicks;

    public void RecordEvent()
    {
        Interlocked.Increment(ref _pendingUnits);
        Interlocked.Exchange(ref _lastHookRecordedTicks, DateTimeOffset.UtcNow.UtcTicks);
    }

    /// Credits one unit without touching LastHookRecordedUtc — used only by
    /// FallbackActivityMonitor's OS-level signal, which must keep re-firing every tick for as
    /// long as the hook stays silent and the OS reports genuine activity. If this instead went
    /// through RecordEvent, the fallback's own credit would un-stale the hook check it just
    /// satisfied, so it would fire once and then wait out another 5 quiet seconds before firing
    /// again — a real Genshin session would earn a fraction of the activity a working hook
    /// would have credited it, not the steady per-tick trickle this is meant to approximate.
    public void RecordFallbackUnit() => Interlocked.Increment(ref _pendingUnits);

    public long DrainUnits() => Interlocked.Exchange(ref _pendingUnits, 0);

    public DateTimeOffset LastHookRecordedUtc => new(Interlocked.Read(ref _lastHookRecordedTicks), TimeSpan.Zero);
}
