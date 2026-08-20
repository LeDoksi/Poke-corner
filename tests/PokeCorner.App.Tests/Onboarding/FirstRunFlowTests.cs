using PokeCorner.App.Onboarding;
using PokeCorner.Core.Domain;
using PokeCorner.Core.Persistence;
using Xunit;

namespace PokeCorner.App.Tests.Onboarding;

public class FirstRunFlowTests
{
    [Fact]
    public void NeedsOnboarding_NullSaveData_ReturnsTrue()
    {
        Assert.True(FirstRunFlow.NeedsOnboarding(null));
    }

    [Fact]
    public void NeedsOnboarding_EmptyIndividuals_ReturnsTrue()
    {
        var data = new SaveData { Individuals = new(), Settings = new PokeCorner.Core.AppSettings() };

        Assert.True(FirstRunFlow.NeedsOnboarding(data));
    }

    [Fact]
    public void NeedsOnboarding_HasIndividuals_ReturnsFalse()
    {
        var individual = new PokemonIndividual
        {
            SpeciesId = 1, EvolutionChainId = 1, ChainLength = 3,
            Nature = Nature.Hardy, IsShiny = false, CaughtAtUtc = DateTimeOffset.UtcNow
        };
        var data = new SaveData { Individuals = new() { individual }, Settings = new PokeCorner.Core.AppSettings() };

        Assert.False(FirstRunFlow.NeedsOnboarding(data));
    }

    [Fact]
    public void HatchStarter_ProducesIndividualFromOfflineStarterChains()
    {
        var offlineStarters = new List<SpeciesEvolutionChain>
        {
            new(ChainId: 1, StarterSpeciesId: 1, CaptureRate: 45, ChainLength: 3, StageSpeciesIds: new[] { 1, 2, 3 }),
            new(ChainId: 4, StarterSpeciesId: 4, CaptureRate: 45, ChainLength: 3, StageSpeciesIds: new[] { 4, 5, 6 })
        };
        var roller = new CatchRoller(new Random(Seed: 42));

        var starter = FirstRunFlow.HatchStarter(roller, offlineStarters, DateTimeOffset.UtcNow);

        Assert.Contains(starter.EvolutionChainId, offlineStarters.Select(c => c.ChainId));
        Assert.Equal(0, starter.AccumulatedCarePoints);
        Assert.Equal(EvolutionStage.Stage1, starter.CurrentStage);
    }
}
