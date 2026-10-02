using PokeCorner.Core.Domain;

namespace PokeCorner.Core.Tests.Domain;

public class ShopServiceTests
{
    private static PokemonIndividual MakeIndividual() => new()
    {
        SpeciesId = 1,
        EvolutionChainId = 1,
        ChainLength = 3,
        Nature = Nature.Hardy,
        IsShiny = false,
        CaughtAtUtc = DateTimeOffset.UtcNow
    };

    [Fact]
    public void EarnCoinsFromActivity_ConvertsUnitsAtFixedRate()
    {
        var service = new ShopService();
        var shop = new ShopInventory();

        service.EarnCoinsFromActivity(shop, ShopService.ActivityUnitsPerCoinEarned * 4 + 50);

        Assert.Equal(4, shop.Coins);
        Assert.Equal(50, shop.RemainderUnits);
    }

    [Fact]
    public void BuyTreatPack_WithoutEnoughCoins_Fails()
    {
        var service = new ShopService();
        var shop = new ShopInventory { Coins = ShopService.TreatPackPrice - 1 };
        var feeding = new FeedingInventory();

        var bought = service.BuyTreatPack(shop, feeding);

        Assert.False(bought);
        Assert.Equal(0, feeding.Treats);
    }

    [Fact]
    public void BuyTreatPack_WithEnoughCoins_SpendsCoinsAndAddsTreats()
    {
        var service = new ShopService();
        var shop = new ShopInventory { Coins = ShopService.TreatPackPrice };
        var feeding = new FeedingInventory();

        var bought = service.BuyTreatPack(shop, feeding);

        Assert.True(bought);
        Assert.Equal(0, shop.Coins);
        Assert.Equal(ShopService.TreatPackAmount, feeding.Treats);
    }

    [Fact]
    public void BuyCarePointsBooster_WithEnoughCoins_SpendsCoinsAndAddsCarePoints()
    {
        var service = new ShopService();
        var shop = new ShopInventory { Coins = ShopService.CarePointsBoosterPrice };
        var individual = MakeIndividual();

        var bought = service.BuyCarePointsBooster(shop, individual);

        Assert.True(bought);
        Assert.Equal(0, shop.Coins);
        Assert.Equal(ShopService.CarePointsBoosterAmount, individual.AccumulatedCarePoints);
    }

    [Fact]
    public void BuyCarePointsBooster_WithoutEnoughCoins_Fails()
    {
        var service = new ShopService();
        var shop = new ShopInventory { Coins = ShopService.CarePointsBoosterPrice - 1 };
        var individual = MakeIndividual();

        var bought = service.BuyCarePointsBooster(shop, individual);

        Assert.False(bought);
        Assert.Equal(0, individual.AccumulatedCarePoints);
    }

    [Fact]
    public void BuyCatchLuckyCharm_WithEnoughCoins_ActivatesCharm()
    {
        var service = new ShopService();
        var shop = new ShopInventory { Coins = ShopService.CatchLuckyCharmPrice };

        var bought = service.BuyCatchLuckyCharm(shop);

        Assert.True(bought);
        Assert.Equal(0, shop.Coins);
        Assert.True(shop.CatchLuckyCharmActive);
    }

    [Fact]
    public void BuyCatchLuckyCharm_WithoutEnoughCoins_Fails()
    {
        var service = new ShopService();
        var shop = new ShopInventory { Coins = ShopService.CatchLuckyCharmPrice - 1 };

        var bought = service.BuyCatchLuckyCharm(shop);

        Assert.False(bought);
        Assert.False(shop.CatchLuckyCharmActive);
    }

    [Theory]
    [InlineData(RarityTier.Common, ShopService.EggPriceCommon)]
    [InlineData(RarityTier.Uncommon, ShopService.EggPriceUncommon)]
    [InlineData(RarityTier.Rare, ShopService.EggPriceRare)]
    [InlineData(RarityTier.Legendary, ShopService.EggPriceLegendary)]
    public void EggPriceForTier_ReturnsExpectedPrice(RarityTier tier, int expectedPrice)
    {
        Assert.Equal(expectedPrice, ShopService.EggPriceForTier(tier));
    }

    [Fact]
    public void TrySpendForEgg_WithEnoughCoins_SpendsExactTierPrice()
    {
        var service = new ShopService();
        var shop = new ShopInventory { Coins = ShopService.EggPriceLegendary };

        var spent = service.TrySpendForEgg(shop, RarityTier.Legendary);

        Assert.True(spent);
        Assert.Equal(0, shop.Coins);
    }

    [Fact]
    public void TrySpendForEgg_WithoutEnoughCoins_Fails()
    {
        var service = new ShopService();
        var shop = new ShopInventory { Coins = ShopService.EggPriceRare - 1 };

        var spent = service.TrySpendForEgg(shop, RarityTier.Rare);

        Assert.False(spent);
        Assert.Equal(ShopService.EggPriceRare - 1, shop.Coins);
    }

    [Fact]
    public void ConsumeCatchLuckyCharm_WhenActive_ReturnsMultiplierAndClearsFlag()
    {
        var shop = new ShopInventory { CatchLuckyCharmActive = true };

        var multiplier = new ShopService().ConsumeCatchLuckyCharm(shop);

        Assert.Equal(4.0, multiplier);
        Assert.False(shop.CatchLuckyCharmActive);
    }

    [Fact]
    public void ConsumeCatchLuckyCharm_WhenInactive_ReturnsOne()
    {
        Assert.Equal(1.0, new ShopService().ConsumeCatchLuckyCharm(new ShopInventory()));
    }

    [Fact]
    public void ConsumeCatchLuckyCharm_SecondCall_ReturnsOne()
    {
        var service = new ShopService();
        var shop = new ShopInventory { CatchLuckyCharmActive = true };

        Assert.Equal(4.0, service.ConsumeCatchLuckyCharm(shop));
        Assert.Equal(1.0, service.ConsumeCatchLuckyCharm(shop));
    }

    /// The charm is a flag, not a stack: buying it again while active used to just burn coins.
    [Fact]
    public void BuyCatchLuckyCharm_WhenAlreadyActive_FailsAndKeepsCoins()
    {
        var shop = new ShopInventory { Coins = ShopService.CatchLuckyCharmPrice * 2, CatchLuckyCharmActive = true };

        Assert.False(new ShopService().BuyCatchLuckyCharm(shop));
        Assert.Equal(ShopService.CatchLuckyCharmPrice * 2, shop.Coins);
    }
}
