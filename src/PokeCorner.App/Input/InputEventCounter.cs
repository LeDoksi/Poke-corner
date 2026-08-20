namespace PokeCorner.App.Input;

public sealed class InputEventCounter
{
    private long _pendingUnits;

    public void RecordEvent() => Interlocked.Increment(ref _pendingUnits);

    public long DrainUnits() => Interlocked.Exchange(ref _pendingUnits, 0);
}
