namespace PokeCorner.Core.Domain;

public sealed class ShopService
{
    // ~10 coins/hour of active play at the recalibrated 9000-units/hour baseline (see
    // FeedingService.ActivityUnitsPerTreatEarned's comment) — one coin roughly every 6
    // minutes of engaged play.
    public const long ActivityUnitsPerCoinEarned = 900;

    public const int TreatPackPrice = 20;
    public const int TreatPackAmount = 5;
    public const int CarePointsBoosterPrice = 20;
    public const int CarePointsBoosterAmount = 7_500;
    public const int CatchLuckyCharmPrice = 30;
    public const double CatchLuckyCharmShinyMultiplier = 4.0;

    // Deliberately non-linear (+50, +70, +150 between tiers) — Legendary is meant to take
    // ~28-30 hours of play to afford, meaningfully harder than the tiers below it, not just
    // proportionally scaled.
    public const int EggPriceCommon = 30;
    public const int EggPriceUncommon = 80;
    public const int EggPriceRare = 150;
    public const int EggPriceLegendary = 300;

    public void EarnCoinsFromActivity(ShopInventory shop, long activityUnits)
    {
        var totalUnits = shop.RemainderUnits + activityUnits;
        shop.Coins += (int)(totalUnits / ActivityUnitsPerCoinEarned);
        shop.RemainderUnits = totalUnits % ActivityUnitsPerCoinEarned;
    }

    public bool BuyTreatPack(ShopInventory shop, FeedingInventory feeding)
    {
        if (shop.Coins < TreatPackPrice) return false;
        shop.Coins -= TreatPackPrice;
        feeding.Treats += TreatPackAmount;
        return true;
    }

    // Caller (App layer) must call AppState.RecomputeStage(activeIndividual) after a
    // successful purchase — this method only mutates AccumulatedCarePoints, matching the
    // pattern FeedingService.Pet/Feed already established.
    public bool BuyCarePointsBooster(ShopInventory shop, PokemonIndividual activeIndividual)
    {
        if (shop.Coins < CarePointsBoosterPrice) return false;
        shop.Coins -= CarePointsBoosterPrice;
        activeIndividual.AccumulatedCarePoints += CarePointsBoosterAmount;
        return true;
    }

    public bool BuyCatchLuckyCharm(ShopInventory shop)
    {
        if (shop.Coins < CatchLuckyCharmPrice) return false;
        shop.Coins -= CatchLuckyCharmPrice;
        shop.CatchLuckyCharmActive = true;
        return true;
    }

    public static int EggPriceForTier(RarityTier tier) => tier switch
    {
        RarityTier.Common => EggPriceCommon,
        RarityTier.Uncommon => EggPriceUncommon,
        RarityTier.Rare => EggPriceRare,
        RarityTier.Legendary => EggPriceLegendary,
        _ => throw new ArgumentOutOfRangeException(nameof(tier))
    };

    // Only spends the currency — the App layer performs the actual catch roll via
    // CatchRoller.RollCatchFromTier and adds the result to the collection (Task 13),
    // mirroring how activity-triggered catches already work. Kept separate so ShopService,
    // like FeedingService, never needs to know about PokemonCollection.
    public bool TrySpendForEgg(ShopInventory shop, RarityTier tier)
    {
        var price = EggPriceForTier(tier);
        if (shop.Coins < price) return false;
        shop.Coins -= price;
        return true;
    }
}
