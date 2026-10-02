using PokeCorner.Core.Domain;

namespace PokeCorner.Core.Tests.Domain;

public class DisplaySpeciesAndCatchResultTests
{
    private static readonly SpeciesEvolutionChain Bulbasaur = new(1, 1, 45, 3, new[] { 1, 2, 3 }, new[] { "Bulbasaur", "Ivysaur", "Venusaur" });

    private static PokemonIndividual Individual(EvolutionStage stage, int? displayOverride = null, int chainId = 1) => new()
    {
        SpeciesId = 1,
        EvolutionChainId = chainId,
        ChainLength = 3,
        Nature = Nature.Hardy,
        IsShiny = false,
        CaughtAtUtc = DateTimeOffset.UnixEpoch,
        CurrentStage = stage,
        DisplayStageOverride = displayOverride
    };

    [Fact]
    public void ResolveDisplaySpeciesId_UsesCurrentStage() =>
        Assert.Equal(2, Individual(EvolutionStage.Stage2).ResolveDisplaySpeciesId(new[] { Bulbasaur }));

    [Fact]
    public void ResolveDisplaySpeciesId_HonoursDisplayOverrideWithinCurrentStage() =>
        Assert.Equal(1, Individual(EvolutionStage.Stage3, displayOverride: 0).ResolveDisplaySpeciesId(new[] { Bulbasaur }));

    /// A hand-edited save is a trust boundary: an override past the stage actually reached
    /// must not reveal a later evolution.
    [Fact]
    public void ResolveDisplaySpeciesId_IgnoresOverrideAboveCurrentStage() =>
        Assert.Equal(1, Individual(EvolutionStage.Stage1, displayOverride: 2).ResolveDisplaySpeciesId(new[] { Bulbasaur }));

    [Fact]
    public void ResolveDisplaySpeciesId_ChainMissing_FallsBackToSpeciesId() =>
        Assert.Equal(1, Individual(EvolutionStage.Stage3, chainId: 999).ResolveDisplaySpeciesId(new[] { Bulbasaur }));

    [Fact]
    public void ToIndividual_CopiesAllRolledFields()
    {
        var caughtAt = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
        var result = new CatchResult(ChainId: 7, SpeciesId: 25, Nature: Nature.Jolly, IsShiny: true, WasDuplicateSpecies: false, ChainLength: 2);

        var individual = result.ToIndividual(caughtAt);

        Assert.Equal(7, individual.EvolutionChainId);
        Assert.Equal(25, individual.SpeciesId);
        Assert.Equal(Nature.Jolly, individual.Nature);
        Assert.True(individual.IsShiny);
        Assert.Equal(2, individual.ChainLength);
        Assert.Equal(caughtAt, individual.CaughtAtUtc);
    }
}
