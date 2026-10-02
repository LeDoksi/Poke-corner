using System.Text.Json;
using PokeCorner.Core.Domain;

namespace PokeCorner.Core.Tests.Domain;

public class SpeciesEvolutionChainTests
{
    private static SpeciesEvolutionChain Butterfree() => new(
        ChainId: 3, StarterSpeciesId: 10, CaptureRate: 255, ChainLength: 3,
        StageSpeciesIds: new[] { 10, 11, 12 },
        StageSpeciesNames: new[] { "Caterpie", "Metapod", "Butterfree" },
        StageTypes: new[] { new[] { "bug" }, new[] { "bug" }, new[] { "bug", "flying" } });

    /// Old-cache compatibility, the same contract StageSpeciesNames already had: a catalog.json
    /// written before types existed deserializes with the property missing, and System.Text.Json
    /// hands the constructor a null. That must become an empty list, not a NullReferenceException
    /// at every card that renders a badge.
    [Fact]
    public void StageTypes_WhenOmitted_NormalizesToEmptyListNotNull()
    {
        var chain = new SpeciesEvolutionChain(
            ChainId: 1, StarterSpeciesId: 1, CaptureRate: 45, ChainLength: 1,
            StageSpeciesIds: new[] { 1 }, StageSpeciesNames: new[] { "Bulbasaur" });

        Assert.NotNull(chain.StageTypes);
        Assert.Empty(chain.StageTypes);
    }

    [Fact]
    public void StageTypes_DeserializedFromCatalogWithoutTheProperty_IsEmpty()
    {
        const string legacyJson = """
        [{"ChainId":1,"StarterSpeciesId":1,"CaptureRate":45,"ChainLength":1,
          "StageSpeciesNames":["Bulbasaur"],"StageSpeciesIds":[1]}]
        """;

        var chains = JsonSerializer.Deserialize<List<SpeciesEvolutionChain>>(legacyJson)!;

        Assert.Empty(chains[0].StageTypes);
        Assert.Empty(chains.ResolveSpeciesTypes(1));
    }

    /// A null ELEMENT inside the array (rather than a missing array) must not reach the UI as a
    /// null enumeration.
    [Fact]
    public void StageTypes_WithNullInnerList_NormalizesThatEntryToEmpty()
    {
        var chain = new SpeciesEvolutionChain(
            ChainId: 1, StarterSpeciesId: 1, CaptureRate: 45, ChainLength: 2,
            StageSpeciesIds: new[] { 1, 2 }, StageSpeciesNames: new[] { "A", "B" },
            StageTypes: new IReadOnlyList<string>?[] { null, new[] { "water" } }!);

        Assert.Equal(2, chain.StageTypes.Count);
        Assert.Empty(chain.StageTypes[0]);
        Assert.Equal(new[] { "water" }, chain.StageTypes[1]);
    }

    [Fact]
    public void StageTypes_SurviveAJsonRoundTrip()
    {
        var round = JsonSerializer.Deserialize<SpeciesEvolutionChain>(JsonSerializer.Serialize(Butterfree()))!;

        Assert.Equal(new[] { "bug", "flying" }, round.StageTypes[2]);
    }

    /// Positionally aligned with StageSpeciesIds, exactly like StageSpeciesNames — reading the
    /// types of an EVOLVED stage must not return the base form's.
    [Fact]
    public void ResolveSpeciesTypes_ReadsTheEntryAtTheMatchingStageIndex()
    {
        var chains = new[] { Butterfree() };

        Assert.Equal(new[] { "bug" }, chains.ResolveSpeciesTypes(10));
        Assert.Equal(new[] { "bug", "flying" }, chains.ResolveSpeciesTypes(12));
    }

    /// No "Pokemon #{id}" equivalent here on purpose: an invented type would be a factual
    /// claim about the species, so an unknown one renders no badges at all.
    [Fact]
    public void ResolveSpeciesTypes_UnknownSpecies_ReturnsEmptyRatherThanAPlaceholder()
    {
        var chains = new[] { Butterfree() };

        Assert.Empty(chains.ResolveSpeciesTypes(9999));
    }
}
