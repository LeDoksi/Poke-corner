using PokeCorner.Core.Domain;
using PokeCorner.Core.Persistence;

namespace PokeCorner.Core.Tests.GlobalUpdate;

public class ShinyTests
{
    [Fact]
    public void Chance_UsesOnlyNurseryLuckAndNeverOffersUnsupportedVariant()
    {
        Assert.Equal(.15, ShinyRules.Chance(ShinySource.Activity, .05, false, true));
        Assert.Equal(1, ShinyRules.Chance(ShinySource.Expedition, .05, true, true), 10);
        Assert.Equal(.15 + .2, ShinyRules.Chance(ShinySource.Nursery, .2, false, true), 10);
        Assert.Equal(ShinyRules.NurseryChanceCap, ShinyRules.Chance(ShinySource.Nursery, .5, false, true));
        Assert.Equal(.45, ShinyRules.NurseryChanceCap, 10);
        Assert.Equal(1, ShinyRules.Chance(ShinySource.Nursery, .5, true, true), 10);
        Assert.Equal(0, ShinyRules.Chance(ShinySource.Nursery, .05, true, false));
    }

    [Fact]
    public void NurseryOutcome_ChangesLuckOnlyForEligibleAttempt()
    {
        Assert.Equal(.09, ShinyRules.NextNurseryLuck(.06, false, true), 8);
        Assert.Equal(0, ShinyRules.NextNurseryLuck(.06, true, true));
        Assert.Equal(.06, ShinyRules.NextNurseryLuck(.06, false, false));
        Assert.Equal(.06, ShinyRules.NextNurseryLuck(.06, true, false));
        Assert.Equal(ShinyRules.NurseryLuckMax, ShinyRules.NextNurseryLuck(ShinyRules.NurseryLuckMax, false, true));
        Assert.Equal(ShinyRules.NurseryLuckMax, ShinyRules.NextNurseryLuck(.29, false, true));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(.0025, 1)]
    [InlineData(.0225, 9)]  // Georgy's 2.4% shown chance: nine eligible hatches
    [InlineData(.098046875, 10)]
    public void LegacyLuck_KeepsEveryEligibleHatchAsOneNewStep(double legacy, int steps)
    {
        Assert.Equal(steps * ShinyRules.NurseryLuckStep, ShinyRules.ConvertLegacyNurseryLuck(legacy), 10);
        Assert.Equal(steps, ShinyRules.NurseryLuckStepsOf(ShinyRules.ConvertLegacyNurseryLuck(legacy)));
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
        var result = roller.RollActivity([onlyRoot], new HashSet<int>(), false, [1, 0, 0, 0], .8,
            _ => false, _ => { spend++; return 4; });
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
        Assert.False(ShinyRules.Resolve(ShinySource.Activity, new ShinyDraw(.2, ShinyRules.BaseChance)));
    }
}
