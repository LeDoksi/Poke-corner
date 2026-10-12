namespace PokeCorner.Core.Domain;

public sealed class FeedingService
{
    // Recalibrated ×7.5 alongside ActivityAccumulator.CatchCheckIntervalUnits.
    public const long ActivityUnitsPerTreatEarned = 3_750;
    public const int PetCarePointsBonus = 110;
    public const int FeedCarePointsBonus = 2_250;

    /// Free and always available, but rate-limited — otherwise it's just a spam-click exploit
    /// for unlimited CP, bypassing the whole activity-based pacing the rest of the game rests on.
    public static readonly TimeSpan PetCooldown = TimeSpan.FromSeconds(3);

    /// Triggered by a left-click on the desktop sprite. Returns false (no-op) while on cooldown.
    public bool Pet(PokemonIndividual individual, CareState careState, DateTimeOffset nowUtc)
    {
        if (careState.LastPetUtc is { } lastPet && nowUtc - lastPet < PetCooldown) return false;

        individual.AccumulatedCarePoints += PetCarePointsBonus;
        careState.LastInteractionUtc = nowUtc;
        careState.LastPetUtc = nowUtc;
        return true;
    }

    /// Only available through the Collection popup, and only when a treat is in stock.
    public bool Feed(PokemonIndividual individual, FeedingInventory inventory, CareState careState, DateTimeOffset nowUtc)
    {
        if (inventory.Treats < 1) return false;

        inventory.Treats -= 1;
        individual.AccumulatedCarePoints += FeedCarePointsBonus;
        careState.LastInteractionUtc = nowUtc;
        return true;
    }

    public void EarnTreatsFromActivity(FeedingInventory inventory, long activityUnits)
    {
        var totalUnits = inventory.RemainderUnits + activityUnits;
        inventory.Treats += (int)(totalUnits / ActivityUnitsPerTreatEarned);
        inventory.RemainderUnits = totalUnits % ActivityUnitsPerTreatEarned;
    }
}
