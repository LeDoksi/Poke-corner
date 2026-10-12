using PokeCorner.Core.Domain;

namespace PokeCorner.Core.Tests.Domain;

public class CareModeServiceTests
{
    [Fact]
    public void ComputeMood_UsesTwelveHourChillPeriod()
    {
        var service = new CareModeService();
        var now = DateTimeOffset.UtcNow;
        var state = new CareState { LastInteractionUtc = now - TimeSpan.FromHours(6) };

        Assert.Equal(0.5, service.ComputeMood(state, now));
        Assert.Equal(0, service.ComputeMood(state, now + TimeSpan.FromHours(6)));
    }

    [Fact]
    public void ComputeMood_LongNeglect_LeavesIndividualAndCareStateUnchanged()
    {
        var collection = new PokemonCollection();
        var individual = new PokemonIndividual { SpeciesId = 1, EvolutionChainId = 1, ChainLength = 1,
            Nature = Nature.Hardy, IsShiny = false, CaughtAtUtc = DateTimeOffset.UtcNow };
        collection.Add(individual);
        var lastInteraction = DateTimeOffset.UtcNow - TimeSpan.FromDays(30);
        var state = new CareState { LastInteractionUtc = lastInteraction };

        Assert.Equal(0, new CareModeService().ComputeMood(state, DateTimeOffset.UtcNow));
        Assert.Same(individual, collection.GetActive());
        Assert.Equal(lastInteraction, state.LastInteractionUtc);
        Assert.Null(state.RanAwayUntilUtc);
    }

    [Fact]
    public void RegisterInteraction_ResetsTimer()
    {
        var service = new CareModeService();
        var now = DateTimeOffset.UtcNow;
        var state = new CareState { LastInteractionUtc = now - TimeSpan.FromDays(10) };

        service.RegisterInteraction(state, now);

        Assert.Equal(now, state.LastInteractionUtc);
    }
}
