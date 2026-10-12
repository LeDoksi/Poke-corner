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
        Assert.Equal(1046, catalog.MixedRecipes.Count);
        Assert.Equal(1025, catalog.SameSpeciesRecipes.Count);
        Assert.Equal(172, catalog.Find(26, 26)!.Outcomes.Single().SpeciesId);
        Assert.Equal(133, catalog.Find(134, 134)!.Outcomes.Single().SpeciesId);
        Assert.Equal(2, catalog.Find(26, 26)!.Hours);
        Assert.Equal(4, catalog.Find(134, 134)!.Hours);
        Assert.Equal(10, catalog.Find(26, 26)!.BaseBerries["oran"]);
        var legendaryRoots = catalog.MixedRecipes.SelectMany(r => r.Outcomes)
            .Where(o => o.Rarity == "Legendary").GroupBy(o => o.SpeciesId).ToArray();
        Assert.Equal(88, legendaryRoots.Length);
        Assert.All(legendaryRoots, group => Assert.Single(group));
        Assert.All(catalog.MixedRecipes.Concat(catalog.SameSpeciesRecipes), r =>
            Assert.All(r.Outcomes, o => Assert.Equal(o.SpeciesId, catalog.FamilyRoot(o.SpeciesId))));
    }

    [Fact]
    public void AvailabilityRecomputesParentsOutcomesWeightsAndTimeWithoutMutation()
    {
        var catalog = Load(2, 849, 451, 453, 803);
        var pair = catalog.Find(2, 849)!;
        Assert.True(pair.IsAvailable);
        Assert.Equal(new[] { 451, 453, 803 }, pair.AvailableOutcomes.Select(o => o.SpeciesId));
        Assert.Equal(6, pair.Hours);
        Assert.Same(pair, catalog.Find(849, 2));
        Assert.Same(pair, catalog.Find(2, 849));
        Assert.Contains(849, catalog.Partners(2));

        var limited = Load(2, 849, 451, 453);
        pair = limited.Find(2, 849)!;
        Assert.Equal(new[] { 451, 453 }, pair.AvailableOutcomes.Select(o => o.SpeciesId));
        Assert.Equal(new[] { 8, 8 }, pair.AvailableOutcomes.Select(o => o.Weight));
        Assert.Equal(4, pair.Hours);
        Assert.False(limited.Find(803, 803)!.IsAvailable);
        Assert.DoesNotContain(803, limited.Partners(2));

        var oneOutcome = Load(2, 849, 451).Find(2, 849)!;
        Assert.True(oneOutcome.IsAvailable);
        Assert.False(oneOutcome.SelectionUseful);
        Assert.Empty(oneOutcome.SelectedSurcharge);
        Assert.Single(oneOutcome.AvailableOutcomes);

        Assert.False(Load(2, 849).Find(2, 849)!.IsAvailable);

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
        var catalog = Load(2, 849, 451, 453, 803);
        var recipe = catalog.Find(2, 849)!;
        if (recipe.Outcomes is NurseryOutcome[] outcomes)
            outcomes[0] = new NurseryOutcome(999, "Legendary", 999, true);
        if (recipe.AvailableOutcomes is NurseryOutcome[] available)
            available[0] = new NurseryOutcome(999, "Legendary", 999, true);
        if (recipe.BaseBerries is Dictionary<string, int> berries) berries["pecha"] = 999;
        if (recipe.SelectedSurcharge is Dictionary<string, int> surcharge) surcharge["pecha"] = 999;
        if (catalog.MixedRecipes is List<NurseryRecipe> mixed) mixed.Clear();
        if (catalog.SameSpeciesRecipes is List<NurseryRecipe> same) same.Clear();
        if (catalog.Partners(2) is int[] partners) partners[0] = 999;

        Assert.Throws<NotSupportedException>(() => ((IList<NurseryOutcome>)recipe.Outcomes)[0] =
            new NurseryOutcome(999, "Legendary", 999, true));
        Assert.Throws<NotSupportedException>(() => ((IList<NurseryOutcome>)recipe.AvailableOutcomes)[0] =
            new NurseryOutcome(999, "Legendary", 999, true));
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, int>)recipe.BaseBerries)["pecha"] = 999);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, int>)recipe.SelectedSurcharge)["pecha"] = 999);
        Assert.Throws<NotSupportedException>(() => ((IList<NurseryRecipe>)catalog.MixedRecipes).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<NurseryRecipe>)catalog.SameSpeciesRecipes).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<int>)catalog.Partners(2))[0] = 999);

        var again = catalog.Find(849, 2)!;
        Assert.Equal(451, again.Outcomes[0].SpeciesId);
        Assert.Equal(451, again.AvailableOutcomes[0].SpeciesId);
        Assert.Equal(6, again.BaseBerries["pecha"]);
        Assert.Equal(4, again.SelectedSurcharge["pecha"]);
        Assert.Equal(1046, catalog.MixedRecipes.Count);
        Assert.Equal(1025, catalog.SameSpeciesRecipes.Count);
        Assert.Contains(849, catalog.Partners(2));
    }

    private static PokemonIndividual Individual(int speciesId, Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(), SpeciesId = speciesId, CurrentSpeciesId = speciesId, EvolutionChainId = speciesId,
        ChainLength = 1, Nature = default, IsShiny = false, CaughtAtUtc = DateTimeOffset.UtcNow
    };
}
