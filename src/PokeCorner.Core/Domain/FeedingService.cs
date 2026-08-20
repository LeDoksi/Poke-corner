namespace PokeCorner.Core.Domain;

public sealed class FeedingService
{
    public const int TreatCostPerFeed = 1;
    // Recalibrated ×7.5 alongside ActivityAccumulator.CatchCheckIntervalUnits.
    public const long ActivityUnitsPerTreatEarned = 3_750;

    public bool TryFeed(FeedingMode mode, FeedingInventory inventory, CareState state, DateTimeOffset nowUtc)
    {
        if (mode == FeedingMode.Economy)
        {
            if (inventory.Treats < TreatCostPerFeed) return false;
            inventory.Treats -= TreatCostPerFeed;
        }

        state.LastInteractionUtc = nowUtc;
        state.RanAwayUntilUtc = null;
        return true;
    }

    public void EarnTreatsFromActivity(FeedingInventory inventory, long activityUnits)
    {
        var totalUnits = inventory.RemainderUnits + activityUnits;
        inventory.Treats += (int)(totalUnits / ActivityUnitsPerTreatEarned);
        inventory.RemainderUnits = totalUnits % ActivityUnitsPerTreatEarned;
    }
}
