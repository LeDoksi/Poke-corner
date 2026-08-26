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
    public void HatchChosen_ProducesIndividualMatchingTheChosenChain()
    {
        var chosenChain = new SpeciesEvolutionChain(ChainId: 4, StarterSpeciesId: 4, CaptureRate: 45, ChainLength: 3, StageSpeciesIds: new[] { 4, 5, 6 }, StageSpeciesNames: new[] { "Charmander", "Charmeleon", "Charizard" });
        var roller = new CatchRoller(new Random(Seed: 42));

        var starter = FirstRunFlow.HatchChosen(chosenChain, roller, DateTimeOffset.UtcNow);

        Assert.Equal(chosenChain.StarterSpeciesId, starter.SpeciesId);
        Assert.Equal(chosenChain.ChainId, starter.EvolutionChainId);
        Assert.Equal(chosenChain.ChainLength, starter.ChainLength);
        Assert.Equal(0, starter.AccumulatedCarePoints);
        Assert.Equal(EvolutionStage.Stage1, starter.CurrentStage);
    }
}
