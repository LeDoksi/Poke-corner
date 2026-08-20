namespace PokeCorner.Core.Domain;

public sealed class FeedingService
{
    // Recalibrated ×7.5 alongside ActivityAccumulator.CatchCheckIntervalUnits.
    public const long ActivityUnitsPerTreatEarned = 3_750;
    public const int PetCarePointsBonus = 110;
    public const int FeedCarePointsBonus = 2_250;

    /// Always available, free. Triggered by a left-click on the desktop sprite.
    public void Pet(PokemonIndividual individual, CareState careState, DateTimeOffset nowUtc)
    {
        individual.AccumulatedCarePoints += PetCarePointsBonus;
        careState.LastInteractionUtc = nowUtc;
        careState.RanAwayUntilUtc = null;
    }

    /// Only available through the Collection popup, and only when a treat is in stock.
    public bool Feed(PokemonIndividual individual, FeedingInventory inventory, CareState careState, DateTimeOffset nowUtc)
    {
        if (inventory.Treats < 1) return false;

        inventory.Treats -= 1;
        individual.AccumulatedCarePoints += FeedCarePointsBonus;
        careState.LastInteractionUtc = nowUtc;
        careState.RanAwayUntilUtc = null;
        return true;
    }

    public void EarnTreatsFromActivity(FeedingInventory inventory, long activityUnits)
    {
        var totalUnits = inventory.RemainderUnits + activityUnits;
        inventory.Treats += (int)(totalUnits / ActivityUnitsPerTreatEarned);
        inventory.RemainderUnits = totalUnits % ActivityUnitsPerTreatEarned;
    }
}
