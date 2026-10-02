using PokeCorner.App.Onboarding;
using PokeCorner.Core.Domain;
using PokeCorner.Core.Persistence;

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

    // ===================== Part D: "wild catches always start at stage 1" =====================
    // Verified by reading the code as well as by these tests: PokemonIndividual.CurrentStage
    // defaults to Stage1, the three creation sites (wild catch and egg hatch in App.xaml.cs,
    // the starter in FirstRunFlow.HatchChosen) all use an object initializer that never
    // assigns it, and the ONLY post-construction mutation in the whole tree is
    // AppState.TryEvolve's explicit bump. These lock that in so it can't regress silently.

    /// Every creation path relies on the property default rather than setting the stage, so
    /// the default IS the invariant. If someone changes it, every catch/hatch/starter starts
    /// mid-chain at once — and this test fails first.
    [Fact]
    public void NewIndividual_DefaultsToStage1_WhateverTheChainLength()
    {
        foreach (var chainLength in new[] { 1, 2, 3 })
        {
            var individual = new PokemonIndividual
            {
                SpeciesId = 1, EvolutionChainId = 1, ChainLength = chainLength,
                Nature = Nature.Hardy, IsShiny = false, CaughtAtUtc = DateTimeOffset.UtcNow
            };

            Assert.Equal(EvolutionStage.Stage1, individual.CurrentStage);
        }
    }

    /// The rolled chain must never influence the starting stage — a 1-, 2- or 3-stage line all
    /// begin at Stage1. Rolled over many seeds so this covers whatever the roller picks
    /// (shiny/nature vary; the stage must not).
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void HatchChosen_AlwaysStartsAtStage1_RegardlessOfChainRolled(int chainLength)
    {
        var stageIds = Enumerable.Range(1, chainLength).ToArray();
        var stageNames = stageIds.Select(id => $"Species{id}").ToArray();
        var chain = new SpeciesEvolutionChain(
            ChainId: 1, StarterSpeciesId: 1, CaptureRate: 45, ChainLength: chainLength,
            StageSpeciesIds: stageIds, StageSpeciesNames: stageNames);

        for (var seed = 0; seed < 50; seed++)
        {
            var starter = FirstRunFlow.HatchChosen(chain, new CatchRoller(new Random(seed)), DateTimeOffset.UtcNow);

            Assert.Equal(EvolutionStage.Stage1, starter.CurrentStage);
            Assert.Equal(0, starter.AccumulatedCarePoints);
        }
    }
}
