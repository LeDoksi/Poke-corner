using System.Security.Cryptography;
using System.Text.Json;
using PokeCorner.Core.PokeApi;
using PokeCorner.Core.Domain;

namespace PokeCorner.App.Tests.GlobalUpdate;

public sealed class CatalogTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "CatalogTest_" + Guid.NewGuid());
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }

    private void Setup(bool shiny = true, string? path = null, string? hash = null, byte[]? bytes = null)
    {
        path ??= "catalog-package/assets/normal/1.gif";
        bytes ??= "GIF89a;"u8.ToArray();
        var file = Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllBytes(file, bytes);
        hash ??= Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        Directory.CreateDirectory(Path.Combine(root, "final"));
        File.WriteAllText(Path.Combine(root, "final/runtime-package.json"), JsonSerializer.Serialize(new { schemaVersion = 1, catalogRevision = "test", files = new[] { new { path = "source.gif", target = path, bytes = bytes.Length, sha256 = hash } } }));
        var asset = new { path, sha256 = hash };
        File.WriteAllText(Path.Combine(root, "final/runtime-catalog.json"), JsonSerializer.Serialize(new { schemaVersion = 1, dataRevision = "test", species = new[] { new { id = 1, displayName = "Bulbasaur", descriptionRu = "Description", types = new[] { "grass" }, familyRootId = 1, rarity = "Uncommon", evolutionChainId = 1, normalAvailable = true, shinyAvailable = shiny, normalAsset = asset, shinyAsset = shiny ? asset : null, voice = (object?)null } } }));
    }

    [Fact] public void OfflineInformationAndAsset() { Setup(); var c = CatalogPackage.Load(root); Assert.Single(c.Species); Assert.Equal("Description", c.Species[0].DescriptionRu); Assert.True(c.CanObtain(1, false)); Assert.NotNull(c.GetAssetPath(1, false)); }
    [Fact] public void UnsupportedVariant() { Setup(false); var c = CatalogPackage.Load(root); Assert.True(c.CanObtain(1, false)); Assert.False(c.CanObtain(1, true)); Assert.Null(c.GetAssetPath(1, true)); }
    [Fact] public void DirectIssueIncludesSingleRootAndRespectsVariant() { Setup(false); var c = CatalogPackage.Load(root); Assert.True(c.CanIssueDirectly(1, false)); Assert.False(c.CanIssueDirectly(1, true)); }
    [Theory]
    [InlineData("../escape.gif", null)]
    [InlineData("catalog-package/assets/normal/1.gif", "bad-hash")]
    public void UnsafePathOrHash(string path, string? hash) { Setup(path: path, hash: hash); Assert.Throws<InvalidDataException>(() => CatalogPackage.Load(root)); }
    [Fact] public void WrongFormat() { Setup(bytes: "not GIF"u8.ToArray()); Assert.Throws<InvalidDataException>(() => CatalogPackage.Load(root)); }
    [Fact] public void LegacyLookupShape() { var c = LegacyOfflineChains.All.Single(c => c.ChainId == 900004); Assert.Equal(new[] { 25, 26 }, c.StageSpeciesIds); }
    [Fact] public void ShippedPackageCounts()
    {
        var repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var c = CatalogPackage.Load(Path.Combine(repo, "src", "PokeCorner.App", "bin", Directory.GetParent(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))!.Name, "net8.0-windows", "Data"));
        Assert.Equal(1025, c.Species.Count);
        Assert.Equal(868, c.Species.Count(s => s.NormalAvailable));
        Assert.Equal(859, c.Species.Count(s => s.ShinyAvailable));
        Assert.Equal(9, c.Species.Count(s => s.NormalAvailable && !s.ShinyAvailable && s.ShinyAsset is null));
        Assert.Equal(541, c.Species.Count(s => s.Id == s.FamilyRootId));
        Assert.True(c.CanIssueDirectly(172, false)); // Pichu is the full Pikachu family root.
        Assert.False(c.CanIssueDirectly(25, false));
        Assert.False(c.CanIssueDirectly(26, false));
        Assert.True(c.CanObtain(26, false));
        Assert.False(c.CanIssueDirectly(650, false));
        Assert.True(c.CanObtain(651, false));
        Assert.False(c.CanIssueDirectly(651, false)); // An available child cannot replace a blocked root.
        Assert.True(c.CanIssueDirectly(201, false)); // A standalone family remains catchable.
    }
    [Fact] public void BuildMappingMatchesManifest()
    {
        var repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        using var m = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo, "docs/design-v2/final/runtime-package.json")));
        var expected = m.RootElement.GetProperty("files").EnumerateArray()
            .Select(f => (@"..\..\" + f.GetProperty("path").GetString()!.Replace('/', '\\'), @"Data\" + f.GetProperty("target").GetString()!.Replace('/', '\\'))).ToHashSet();
        var props = System.Xml.Linq.XDocument.Load(Path.Combine(repo, "src/PokeCorner.App/RuntimePackage.props"));
        var actual = props.Descendants("Content").Select(e => ((string)e.Attribute("Include")!, (string)e.Attribute("Link")!)).ToList();
        Assert.Equal(expected.Count, actual.Count);
        Assert.True(expected.SetEquals(actual));
    }
    [Fact] public void GiftCandidatesHaveTwoAvailableFamilyRootsIncludingStandalone()
    {
        var repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var c = CatalogPackage.Load(Path.Combine(repo, "src", "PokeCorner.App", "bin", Directory.GetParent(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))!.Name, "net8.0-windows", "Data"));
        using var rules = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo, "docs/design-v2/final/catalog-rules.json")));
        using var nursery = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo, "docs/design-v2/nursery-recipes-draft.json")));
        var ids = rules.RootElement.GetProperty("giftRecipeCandidates").EnumerateArray().Select(x => x.GetString()).ToHashSet();
        var candidates = nursery.RootElement.GetProperty("recipes").EnumerateArray().Where(r => ids.Contains(r.GetProperty("id").GetString())).ToArray();
        Assert.Equal(166, candidates.Length);
        Assert.All(candidates, r => Assert.All(r.GetProperty("parentIds").EnumerateArray(), p => Assert.True(c.CanIssueDirectly(p.GetInt32(), false))));
        Assert.Contains(candidates, r => r.GetProperty("parentIds").EnumerateArray().Any(p => p.GetInt32() == 142));
    }
    [Fact] public void MissingRequiredAsset() { Setup(); File.Delete(Path.Combine(root, "catalog-package/assets/normal/1.gif")); Assert.Throws<InvalidDataException>(() => CatalogPackage.Load(root)); }

    [Fact] public void UnreferencedManifestFileMustMatchHash()
    {
        Setup();
        var file = Path.Combine(root, "extra.txt");
        File.WriteAllText(file, "bad");
        var manifestPath = Path.Combine(root, "final/runtime-package.json");
        var manifest = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(manifestPath))!;
        manifest["files"]!.AsArray().Add(JsonSerializer.SerializeToNode(new {
            path = "extra.txt", target = "extra.txt", bytes = 3,
            sha256 = Convert.ToHexString(SHA256.HashData("good"u8)).ToLowerInvariant()
        }));
        File.WriteAllText(manifestPath, manifest.ToJsonString());
        Assert.Throws<InvalidDataException>(() => CatalogPackage.Load(root));
    }
    [Fact] public void ExistingShinyRemainsShinyWhenNewShinyIsUnavailable()
    {
        Setup(shiny: false);
        var existing = new PokemonIndividual {
            SpeciesId = 1, EvolutionChainId = 1, ChainLength = 1,
            Nature = Nature.Hardy, IsShiny = true, CaughtAtUtc = DateTimeOffset.UtcNow
        };
        Assert.False(CatalogPackage.Load(root).CanObtain(1, true));
        Assert.True(existing.IsShiny);
    }
}
