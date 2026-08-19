using PokeCorner.Core;
using PokeCorner.Core.Activity;
using PokeCorner.Core.Domain;
using Xunit;

namespace PokeCorner.Core.Tests;

public class AppStateTests
{
    private static PokemonIndividual MakeIndividual(int chainLength = 3) => new()
    {
        SpeciesId = 1,
        EvolutionChainId = 1,
        ChainLength = chainLength,
        Nature = Nature.Hardy,
        IsShiny = false,
        CaughtAtUtc = DateTimeOffset.UtcNow
    };

    [Fact]
    public void ApplyActivityUnits_GrowsActiveIndividualCarePoints()
    {
        var state = new AppState();
        var individual = MakeIndividual();
        state.Collection.Add(individual);

        state.ApplyActivityUnits(1234, DateTimeOffset.UtcNow);

        Assert.Equal(1234, individual.AccumulatedCarePoints);
    }

    [Fact]
    public void ApplyActivityUnits_PastStage2Threshold_EvolvesActiveIndividual()
    {
        var state = new AppState();
        var individual = MakeIndividual();
        state.Collection.Add(individual);

        state.ApplyActivityUnits(EvolutionThresholds.Stage2Threshold, DateTimeOffset.UtcNow);

        Assert.Equal(EvolutionStage.Stage2, individual.CurrentStage);
    }

    [Fact]
    public void ApplyActivityUnits_NoActiveIndividual_DoesNotThrow()
    {
        var state = new AppState();

        var exception = Record.Exception(() => state.ApplyActivityUnits(500, DateTimeOffset.UtcNow));

        Assert.Null(exception);
    }

    [Fact]
    public void ApplyActivityUnits_CrossingCatchInterval_RaisesCatchRollDue()
    {
        var state = new AppState();
        state.Collection.Add(MakeIndividual());
        var raised = 0;
        state.CatchRollDue += () => raised++;

        state.ApplyActivityUnits(ActivityAccumulator.CatchCheckIntervalUnits, DateTimeOffset.UtcNow);

        Assert.Equal(1, raised);
    }
}
