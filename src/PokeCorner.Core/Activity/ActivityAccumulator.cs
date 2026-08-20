namespace PokeCorner.Core.Activity;

public sealed class ActivityAccumulator
{
    // Recalibrated for realistic gamer click rates (150 APM / 9000 units/hour, based on
    // real Dota 2 APM data — see docs/superpowers/specs/2026-08-20-shop-visuals-and-sprites-design.md
    // §0) rather than the ~1200 units/hour this constant originally, silently, assumed.
    // ~2.5 hours of active play still yields roughly one catch-check.
    public const long CatchCheckIntervalUnits = 22_500;

    private long _unitsSinceLastCatchCheck;

    public long TotalUnits { get; private set; }

    public long UnitsSinceLastCatchCheck => _unitsSinceLastCatchCheck;

    public event Action? CatchCheckDue;

    public void AddUnits(long units)
    {
        if (units <= 0) return;

        TotalUnits += units;
        _unitsSinceLastCatchCheck += units;

        while (_unitsSinceLastCatchCheck >= CatchCheckIntervalUnits)
        {
            _unitsSinceLastCatchCheck -= CatchCheckIntervalUnits;
            CatchCheckDue?.Invoke();
        }
    }
}
