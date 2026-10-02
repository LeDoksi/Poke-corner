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

    // Only mutates AccumulatedCarePoints, like FeedingService.Pet/Feed: crossing a threshold just
    // makes the individual ready, and the player evolves it via "Эволюционировать!".
    public bool BuyCarePointsBooster(ShopInventory shop, PokemonIndividual activeIndividual)
    {
        if (shop.Coins < CarePointsBoosterPrice) return false;
        shop.Coins -= CarePointsBoosterPrice;
        activeIndividual.AccumulatedCarePoints += CarePointsBoosterAmount;
        return true;
    }

    public bool BuyCatchLuckyCharm(ShopInventory shop)
    {
        // A flag, not a stack: a second charm while one is active would only burn coins.
        if (shop.CatchLuckyCharmActive || shop.Coins < CatchLuckyCharmPrice) return false;
        shop.Coins -= CatchLuckyCharmPrice;
        shop.CatchLuckyCharmActive = true;
        return true;
    }

    // Read-and-clear in one place so a catch and an egg hatch spend the charm identically:
    // whichever happens first gets the boost, regardless of whether the roll comes up shiny.
    public double ConsumeCatchLuckyCharm(ShopInventory shop)
    {
        if (!shop.CatchLuckyCharmActive) return 1.0;
        shop.CatchLuckyCharmActive = false;
        return CatchLuckyCharmShinyMultiplier;
    }

    public static int EggPriceForTier(RarityTier tier) => tier switch
    {
        RarityTier.Common => EggPriceCommon,
        RarityTier.Uncommon => EggPriceUncommon,
        RarityTier.Rare => EggPriceRare,
        RarityTier.Legendary => EggPriceLegendary,
        _ => throw new ArgumentOutOfRangeException(nameof(tier))
    };

    // Only spends the currency. The bought egg then incubates (EggIncubationService), and the
    // species is rolled only when it hatches — App.HatchEgg via CatchRoller.RollCatchFromTier.
    // Kept separate so ShopService, like FeedingService, never needs to know about
    // PokemonCollection.
    public bool TrySpendForEgg(ShopInventory shop, RarityTier tier)
    {
        var price = EggPriceForTier(tier);
        if (shop.Coins < price) return false;
        shop.Coins -= price;
        return true;
    }
}
