using PokeCorner.App.Onboarding;
using PokeCorner.Core.Domain;
using PokeCorner.Core.Persistence;
using PokeCorner.Core.PokeApi;

namespace PokeCorner.App.Tests.Onboarding;

public class FirstRunFlowTests
{
    private static readonly Lazy<CatalogPackage> Package = new(() => CatalogPackage.Load(Path.Combine(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..")), "src", "PokeCorner.App", "bin", Directory.GetParent(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))!.Name, "net8.0-windows", "Data")));
    [Fact]
    public void NeedsStarter_EmptySaveData_ReturnsTrue()
    {
        Assert.True(FirstRunFlow.NeedsStarter(new SaveData { Individuals = [], Settings = new() }));
    }

    [Fact]
    public void NeedsStarter_EmptyIndividuals_ReturnsTrue()
    {
        var data = new SaveData { Individuals = new(), Settings = new PokeCorner.Core.AppSettings() };

        Assert.True(FirstRunFlow.NeedsStarter(data));
    }

    [Fact]
    public void NeedsStarter_ExplicitResetPendingStarter_ReturnsTrue()
    {
        var data = new SaveData { Individuals = [], Settings = new(), StarterSelectionPending = true };
        Assert.True(FirstRunFlow.NeedsStarter(data));
    }

    [Fact]
    public void NeedsStarter_HasIndividuals_ReturnsFalse()
    {
        var individual = new PokemonIndividual
        {
            SpeciesId = 1, EvolutionChainId = 1, ChainLength = 3,
            Nature = Nature.Hardy, IsShiny = false, CaughtAtUtc = DateTimeOffset.UtcNow
        };
        var data = new SaveData { Individuals = new() { individual }, Settings = new PokeCorner.Core.AppSettings() };

        Assert.False(FirstRunFlow.NeedsStarter(data));
    }

    [Fact]
    public void HatchChosen_ProducesIndividualMatchingTheChosenChain()
    {
        var chosenChain = new SpeciesEvolutionChain(ChainId: 4, StarterSpeciesId: 4, CaptureRate: 45, ChainLength: 3, StageSpeciesIds: new[] { 4, 5, 6 }, StageSpeciesNames: new[] { "Charmander", "Charmeleon", "Charizard" });
        var roller = new CatchRoller(new Random(Seed: 42));

        var starter = FirstRunFlow.HatchChosen(chosenChain, roller, DateTimeOffset.UtcNow, Package.Value);

        Assert.Equal(chosenChain.StarterSpeciesId, starter.SpeciesId);
        Assert.Equal(chosenChain.ChainId, starter.EvolutionChainId);
        Assert.Equal(chosenChain.ChainLength, starter.ChainLength);
        Assert.Equal(0, starter.AccumulatedCarePoints);
        Assert.Equal(EvolutionStage.Stage1, starter.CurrentStage);
    }

    [Fact]
    public void HatchChosen_RejectsAnEvolvedStarterEvenWhenItsAssetExists()
    {
        var chain = new SpeciesEvolutionChain(10, 25, 190, 2, [25, 26], ["Pikachu", "Raichu"]);
        Assert.True(Package.Value.CanObtain(25, false));
        Assert.Throws<InvalidDataException>(() => FirstRunFlow.HatchChosen(chain, new CatchRoller(new Random(1)), DateTimeOffset.UtcNow, Package.Value));
    }

    [Fact]
    public void HatchChosen_AcceptsStandaloneRoot()
    {
        var chain = new SpeciesEvolutionChain(201, 201, 255, 1, [201], ["Unown"]);
        Assert.Equal(201, FirstRunFlow.HatchChosen(chain, new CatchRoller(new Random(1)), DateTimeOffset.UtcNow, Package.Value).SpeciesId);
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
            var starter = FirstRunFlow.HatchChosen(chain, new CatchRoller(new Random(seed)), DateTimeOffset.UtcNow, Package.Value);

            Assert.Equal(EvolutionStage.Stage1, starter.CurrentStage);
            Assert.Equal(0, starter.AccumulatedCarePoints);
        }
    }
}
