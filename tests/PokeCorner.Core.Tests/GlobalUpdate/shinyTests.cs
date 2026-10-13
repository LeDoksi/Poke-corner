using PokeCorner.Core.Domain;
using PokeCorner.Core.Persistence;

namespace PokeCorner.Core.Tests.GlobalUpdate;

public class ShinyTests
{
    [Fact]
    public void Chance_UsesOnlyNurseryLuckAndNeverOffersUnsupportedVariant()
    {
        Assert.Equal(1d / 512, ShinyRules.Chance(ShinySource.Activity, .05, false, true));
        Assert.Equal(4d / 512, ShinyRules.Chance(ShinySource.Expedition, .05, true, true));
        Assert.Equal(.1, ShinyRules.Chance(ShinySource.Nursery, .2, false, true));
        Assert.Equal(.4, ShinyRules.Chance(ShinySource.Nursery, .2, true, true));
        Assert.Equal(0, ShinyRules.Chance(ShinySource.Nursery, .05, true, false));
    }

    [Fact]
    public void NurseryOutcome_ChangesLuckOnlyForEligibleAttempt()
    {
        Assert.Equal(.025, ShinyRules.NextNurseryLuck(.0225, false, true), 8);
        Assert.Equal(0, ShinyRules.NextNurseryLuck(.0225, true, true));
        Assert.Equal(.0225, ShinyRules.NextNurseryLuck(.0225, false, false));
        Assert.Equal(.0225, ShinyRules.NextNurseryLuck(.0225, true, false));
        Assert.Equal(.1 - ShinyRules.BaseChance,
            ShinyRules.NextNurseryLuck(.1 - ShinyRules.BaseChance, false, true));
    }

    [Fact]
    public void Activation_RequiresStockAndUnsupportedAttemptPreservesIt()
    {
        var state = new SaveData { Individuals = [], Settings = new(), SchemaVersion = 2,
            Items = new() { ["lucky-charm"] = 1 } };
        var shop = new ShopService();
        Assert.True(shop.TryActivateLuckyCharm(state).Success);
        Assert.Equal(0, state.Items["lucky-charm"]);
        Assert.True(state.ShopCatchLuckyCharmActive);
        Assert.False(shop.TryActivateLuckyCharm(state).Success);
        Assert.False(shop.TryConsumeLuckyCharm(state, false));
        Assert.True(state.ShopCatchLuckyCharmActive);
        Assert.True(shop.TryConsumeLuckyCharm(state, true));
        Assert.False(state.ShopCatchLuckyCharmActive);
    }

    [Fact]
    public void UnsupportedPickedVariant_DoesNotSpendCharmOrRerollSpecies()
    {
        var onlyRoot = new SpeciesEvolutionChain(1, 870, 255, 1, [870], ["Falinks"]);
        var spend = 0;
        var roller = new CatchRoller(new Random(1));
        Assert.True(roller.TryRollCatchFromTier([onlyRoot], RarityTier.Common, [],
            () => { spend++; return 4; }, out var result, _ => false));
        Assert.Equal(870, result.SpeciesId);
        Assert.False(result.IsShiny);
        Assert.Equal(0, spend);
    }

    [Fact]
    public void Resolve_ReusesPersistedDraw()
    {
        var draw = new ShinyDraw(.002, ShinyRules.Chance(ShinySource.Nursery, .01, false, true));
        Assert.True(ShinyRules.Resolve(ShinySource.Nursery, draw));
        Assert.True(ShinyRules.Resolve(ShinySource.Nursery, draw));
        Assert.False(ShinyRules.Resolve(ShinySource.Activity, new ShinyDraw(.002, ShinyRules.BaseChance)));
    }
}
