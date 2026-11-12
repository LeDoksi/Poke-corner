using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using PokeCorner.Core.Domain;
using PokeCorner.Core.PokeApi;

namespace PokeCorner.Core.Tests.GlobalUpdate;

public sealed class NurseryCatalogTests : IDisposable
{
    private static readonly string Repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
    private readonly string root = Path.Combine(Path.GetTempPath(), "nursery-catalog-" + Guid.NewGuid());

    private NurseryCatalog Load(params int[] available)
    {
        Directory.CreateDirectory(Path.Combine(root, "final"));
        var source = Path.Combine(Repo, "docs/design-v2");
        var catalog = JsonNode.Parse(File.ReadAllText(Path.Combine(source, "final/runtime-catalog.json")))!;
        var files = new List<object>();
        var gif = "GIF89a"u8.ToArray();
        File.WriteAllBytes(Path.Combine(root, "normal.gif"), gif);
        files.Add(new { target = "normal.gif", path = "normal.gif", bytes = gif.Length, sha256 = Hash(gif) });
        foreach (var species in catalog["species"]!.AsArray())
        {
            var enabled = available.Contains((int)species!["id"]!);
            species["normalAvailable"] = enabled;
            species["normalAsset"] = enabled ? new JsonObject { ["path"] = "normal.gif", ["sha256"] = Hash(gif) } : null;
            species["shinyAvailable"] = false;
            species["shinyAsset"] = null;
            species["voice"] = null;
        }
        Write("final/runtime-catalog.json", catalog.ToJsonString(), files);
        foreach (var name in new[] { "catalog-rules.json", "balance.json" })
            Write("final/" + name, File.ReadAllText(Path.Combine(source, "final", name)), files);
        Write("nursery-recipes-draft.json", File.ReadAllText(Path.Combine(source, "nursery-recipes-draft.json")), files);
        File.WriteAllText(Path.Combine(root, "final/runtime-package.json"), JsonSerializer.Serialize(new
        {
            schemaVersion = 1, catalogRevision = (string)catalog["dataRevision"]!, files
        }));
        return new NurseryCatalog(CatalogPackage.Load(root));
    }

    private void Write(string relative, string content, List<object> files)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        File.WriteAllBytes(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)), bytes);
        files.Add(new { target = relative, path = relative, bytes = bytes.Length, sha256 = Hash(bytes) });
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }

    [Fact]
    public void CanonicalDefinitionsAndFamilyRoots()
    {
        var catalog = Load(26, 172, 134, 133, 150);
        Assert.Equal(1082, catalog.MixedRecipes.Count); // 541 families, four partners each
        Assert.Equal(541, catalog.SameSpeciesRecipes.Count);
        Assert.Equal(172, catalog.Find(26, 26)!.Outcomes.Single().SpeciesId);
        Assert.Equal(133, catalog.Find(134, 134)!.Outcomes.Single().SpeciesId);
        Assert.Equal(2, catalog.Find(26, 26)!.Hours);
        Assert.Equal(4, catalog.Find(134, 134)!.Hours);
        Assert.Equal(15, catalog.Find(26, 26)!.BaseBerries["oran"]);
        var legendaryRoots = catalog.MixedRecipes.SelectMany(r => r.Outcomes)
            .Where(o => o.Rarity == "Legendary").GroupBy(o => o.SpeciesId).ToArray();
        Assert.Equal(88, legendaryRoots.Length);
        Assert.All(legendaryRoots, group => Assert.Equal(4, group.Count())); // every baby hatches from four pairs
        Assert.All(catalog.MixedRecipes.Concat(catalog.SameSpeciesRecipes), r =>
            Assert.All(r.Outcomes, o => Assert.Equal(o.SpeciesId, catalog.FamilyRoot(o.SpeciesId))));
    }

    [Fact]
    public void OneFamilyHatchesOnlyItsOwnBabyAndEveryBabyKeepsAMixedPair()
    {
        var catalog = Load(Enumerable.Range(1, 1100).ToArray());
        Assert.All(catalog.SameSpeciesRecipes.Where(r => r.IsAvailable), r =>
            Assert.Equal([r.FirstParentId], r.AvailableOutcomes.Select(o => o.SpeciesId)));
        var mixedBabies = catalog.MixedRecipes.Where(r => r.IsAvailable).SelectMany(r => r.AvailableOutcomes.Select(o => o.SpeciesId)).ToHashSet();
        Assert.All(catalog.SameSpeciesRecipes.Where(r => r.IsAvailable), r => Assert.Contains(r.FirstParentId, mixedBabies));
    }

    [Fact]
    public void EveryFamilyHasFourPartnersEightBabiesAndHatchesFromFourPairs()
    {
        // Georgy 12.11: the same share for everyone (docs/design-v2/build-nursery-families.py).
        var catalog = Load(Enumerable.Range(1, 1100).ToArray());
        var roots = catalog.SameSpeciesRecipes.Select(r => r.FirstParentId).ToArray();
        Assert.Equal(541, roots.Length);
        Assert.All(catalog.MixedRecipes, r => Assert.Equal(2, r.Outcomes.Count));
        Assert.All(roots, root =>
        {
            var pairs = catalog.MixedRecipes.Where(r => r.FirstParentId == root || r.SecondParentId == root).ToArray();
            Assert.Equal(4, pairs.Length);
            Assert.Equal(8, pairs.SelectMany(r => r.Outcomes).Select(o => o.SpeciesId).Distinct().Count());
            Assert.Equal(4, catalog.MixedRecipes.Count(r => r.Outcomes.Any(o => o.SpeciesId == root)));
        });
    }

    [Fact]
    public void DifferentFamiliesNeverHatchABabyOfEitherParentFamily()
    {
        var catalog = Load(26, 172, 134, 133, 150);
        Assert.All(catalog.MixedRecipes, r =>
        {
            Assert.NotEqual(r.FirstParentId, r.SecondParentId);
            Assert.DoesNotContain(r.Outcomes, o => o.SpeciesId == r.FirstParentId || o.SpeciesId == r.SecondParentId);
            Assert.DoesNotContain(r.AvailableOutcomes, o => o.SpeciesId == r.FirstParentId || o.SpeciesId == r.SecondParentId);
        });
        // Any stages of the two families stand for the pair: Beedrill + Toxtricity is Weedle's family + Toxel's.
        Assert.Equal(new[] { 41, 69 }, catalog.Find(15, 849)!.Outcomes.Select(o => o.SpeciesId));
        // Parents of one family still hatch their own root.
        Assert.Contains(catalog.SameSpeciesRecipes, r => r.Outcomes.Any(o => o.SpeciesId == r.FirstParentId));
    }

    [Fact]
    public void AvailabilityRecomputesParentsOutcomesWeightsAndTimeWithoutMutation()
    {
        var catalog = Load(125, 849, 871, 992);
        var pair = catalog.Find(125, 849)!;
        Assert.True(pair.IsAvailable);
        Assert.Equal(new[] { 871, 992 }, pair.AvailableOutcomes.Select(o => o.SpeciesId));
        Assert.Equal(new[] { 8, 4 }, pair.AvailableOutcomes.Select(o => o.Weight));
        Assert.Equal(6, pair.Hours);
        Assert.Same(pair, catalog.Find(849, 125));
        Assert.Same(pair, catalog.Find(125, 849));
        Assert.Contains(848, catalog.Partners(125)); // partner families by root

        var limited = Load(125, 849, 871);
        var oneOutcome = limited.Find(125, 849)!;
        Assert.True(oneOutcome.IsAvailable);
        Assert.Equal(new[] { 871 }, oneOutcome.AvailableOutcomes.Select(o => o.SpeciesId));
        Assert.Equal(4, oneOutcome.Hours);
        Assert.False(oneOutcome.SelectionUseful);
        Assert.Empty(oneOutcome.SelectedSurcharge);
        Assert.False(limited.Find(992, 992)!.IsAvailable);

        Assert.False(Load(125, 849).Find(125, 849)!.IsAvailable);

        var developedOnly = Load(26);
        Assert.False(developedOnly.Find(26, 26)!.IsAvailable);
        Assert.Empty(developedOnly.Find(26, 26)!.AvailableOutcomes);
    }

    [Fact]
    public void EqualSpeciesRequiresTwoDistinctIndividuals()
    {
        var catalog = Load(26, 172);
        var first = Individual(26);
        var second = Individual(26);
        Assert.Null(catalog.Find(first, first));
        Assert.NotNull(catalog.Find(first, second));
        Assert.Null(catalog.Find(first, Individual(26, first.Id)));
        second.CurrentSpeciesId = 134;
        second.DisplayStageOverride = 0;
        Assert.Null(catalog.Find(first, second));
    }

    [Fact]
    public void PublishedCollectionsCannotChangeCachedRecipes()
    {
        var catalog = Load(125, 849, 871, 992);
        var recipe = catalog.Find(125, 849)!;
        if (recipe.Outcomes is NurseryOutcome[] outcomes)
            outcomes[0] = new NurseryOutcome(999, "Legendary", 999, true);
        if (recipe.AvailableOutcomes is NurseryOutcome[] available)
            available[0] = new NurseryOutcome(999, "Legendary", 999, true);
        if (recipe.BaseBerries is Dictionary<string, int> berries) berries["pecha"] = 999;
        if (recipe.SelectedSurcharge is Dictionary<string, int> surcharge) surcharge["pecha"] = 999;
        if (catalog.MixedRecipes is List<NurseryRecipe> mixed) mixed.Clear();
        if (catalog.SameSpeciesRecipes is List<NurseryRecipe> same) same.Clear();
        if (catalog.Partners(125) is int[] partners) partners[0] = 999;

        Assert.Throws<NotSupportedException>(() => ((IList<NurseryOutcome>)recipe.Outcomes)[0] =
            new NurseryOutcome(999, "Legendary", 999, true));
        Assert.Throws<NotSupportedException>(() => ((IList<NurseryOutcome>)recipe.AvailableOutcomes)[0] =
            new NurseryOutcome(999, "Legendary", 999, true));
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, int>)recipe.BaseBerries)["pecha"] = 999);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, int>)recipe.SelectedSurcharge)["pecha"] = 999);
        Assert.Throws<NotSupportedException>(() => ((IList<NurseryRecipe>)catalog.MixedRecipes).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<NurseryRecipe>)catalog.SameSpeciesRecipes).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<int>)catalog.Partners(125))[0] = 999);

        var again = catalog.Find(849, 125)!;
        Assert.Equal(871, again.Outcomes[0].SpeciesId);
        Assert.Equal(871, again.AvailableOutcomes[0].SpeciesId);
        Assert.Equal(15, again.BaseBerries["oran"]);
        Assert.Equal(5, again.SelectedSurcharge["oran"]);
        Assert.Equal(1082, catalog.MixedRecipes.Count); // 541 families, four partners each
        Assert.Equal(541, catalog.SameSpeciesRecipes.Count);
        Assert.Contains(848, catalog.Partners(125)); // partner families by root
    }

    private static PokemonIndividual Individual(int speciesId, Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(), SpeciesId = speciesId, CurrentSpeciesId = speciesId, EvolutionChainId = speciesId,
        ChainLength = 1, Nature = default, IsShiny = false, CaughtAtUtc = DateTimeOffset.UtcNow
    };
}
