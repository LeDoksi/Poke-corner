namespace PokeCorner.Core.Activity;

public sealed class ActivityAccumulator
{
    // Recalibrated for realistic gamer click rates (150 APM / 9000 units/hour, based on
    // real Dota 2 APM data — see docs/superpowers/specs/2026-08-20-shop-visuals-and-sprites-design.md
    // §0) rather than the ~1200 units/hour this constant originally, silently, assumed.
    // ~2.5 hours of active play still yields roughly one catch-check.
    public const long CatchCheckIntervalUnits = 22_500;

    public long TotalUnits { get; private set; }

    /// <summary>
    /// Progress toward the next catch check — the number behind the "До поимки" bar.
    /// Settable so it can be restored from the save file at load: it used to be read-only,
    /// which is why that bar started over at zero on every relaunch. Mirrors the public
    /// setters <see cref="Domain.FeedingInventory.RemainderUnits"/> and
    /// <see cref="Domain.ShopInventory.RemainderUnits"/> already use for the same purpose.
    /// Assigning past the threshold does not raise <see cref="CatchCheckDue"/> — only
    /// <see cref="AddUnits"/> does, which is what hydration wants.
    /// </summary>
    public long UnitsSinceLastCatchCheck { get; set; }

    public event Action? CatchCheckDue;

    public void AddUnits(long units)
    {
        if (units <= 0) return;

        TotalUnits += units;
        UnitsSinceLastCatchCheck += units;

        while (UnitsSinceLastCatchCheck >= CatchCheckIntervalUnits)
        {
            UnitsSinceLastCatchCheck -= CatchCheckIntervalUnits;
            CatchCheckDue?.Invoke();
        }
    }
}
