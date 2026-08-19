namespace PokeCorner.Core.Activity;

public sealed class ActivityAccumulator
{
    // Tuned so ~2-3 hours of medium-high activity yields roughly one catch-check
    // (see spec: 2026-08-19-desktop-pokemon-pet-design.md, "Поимка новых покемонов").
    public const long CatchCheckIntervalUnits = 3000;

    private long _unitsSinceLastCatchCheck;

    public long TotalUnits { get; private set; }

    public event Action? CatchCheckDue;

    public void AddUnits(long units)
    {
        TotalUnits += units;
        _unitsSinceLastCatchCheck += units;

        while (_unitsSinceLastCatchCheck >= CatchCheckIntervalUnits)
        {
            _unitsSinceLastCatchCheck -= CatchCheckIntervalUnits;
            CatchCheckDue?.Invoke();
        }
    }
}
