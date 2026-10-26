using System.Text.Json;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using PokeCorner.App.Popup;
using PokeCorner.App.Tests.Theme;
using PokeCorner.Core;
using PokeCorner.Core.Domain;
using PokeCorner.Core.PokeApi;

namespace PokeCorner.App.Tests.GlobalUpdate;

public sealed class RussianFactsTests
{
    [Fact]
    public void EveryCatalogSpeciesHasRussianFactOffline()
    {
        var catalog = CatalogPackage.Load(Path.Combine(AppContext.BaseDirectory, "Data"));
        Assert.Equal(1025, catalog.Species.Count);
        var missing = catalog.Species.Where(s => string.IsNullOrWhiteSpace(CollectionPresentation.Fact(s.Id))).Select(s => s.Id).ToArray();
        Assert.True(missing.Length == 0, $"Missing Russian facts: {missing.Length}; first IDs: {string.Join(",", missing.Take(16))}");
        Assert.All(catalog.Species, s => Assert.Matches("[А-Яа-яЁё]", CollectionPresentation.Fact(s.Id)!));
        Assert.Null(CollectionPresentation.Fact(0));
        Assert.Null(CollectionPresentation.Fact(1026));
    }

    [Fact]
    public void DataAndExactSourceProvenanceShipThroughManifest()
    {
        var data = Path.Combine(AppContext.BaseDirectory, "Data");
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(data, "final/runtime-package.json")));
        foreach (var target in new[] { "catalog-package/russian-facts.json", "catalog-package/russian-facts-provenance.json" })
        {
            var item = Assert.Single(manifest.RootElement.GetProperty("files").EnumerateArray().Where(f => f.GetProperty("target").GetString() == target));
            var bytes = File.ReadAllBytes(Path.Combine(data, target));
            Assert.Equal(item.GetProperty("bytes").GetInt64(), bytes.LongLength);
            Assert.Equal(item.GetProperty("sha256").GetString(), Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        }
        using var facts = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(data, "catalog-package/russian-facts.json")));
        using var provenance = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(data, "catalog-package/russian-facts-provenance.json")));
        var rows = facts.RootElement.GetProperty("facts").EnumerateArray().ToArray();
        Assert.Equal(Enumerable.Range(1, 1025), rows.Select(r => r.GetProperty("speciesId").GetInt32()).Order());
        var sources = provenance.RootElement.GetProperty("facts").EnumerateArray().ToDictionary(r => r.GetProperty("speciesId").GetInt32());
        Assert.Equal(1025, sources.Count);
        Assert.All(rows, row => Assert.Equal("en", sources[row.GetProperty("speciesId").GetInt32()].GetProperty("sourceLanguage").GetString()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SpeciesCaughtInEitherFormShowsFactAndOwnedFormsShareIt(bool ownedShiny) => WpfTestHost.Run(() =>
    {
        var state = new AppState();
        var pet = new PokemonIndividual { SpeciesId = 1, CurrentSpeciesId = 1, FamilyRootId = 1, ReachedSpeciesIds = [1],
            EvolutionChainId = 1, ChainLength = 1, IsShiny = ownedShiny, Nature = Nature.Hardy, CaughtAtUtc = DateTimeOffset.UtcNow };
        state.Collection.Add(pet);
        var vm = new CollectionViewModel(state.Collection, state.CareStateByIndividualId, new CareModeService(), [], [],
            species: [new CatalogSpecies { Id = 1, DisplayName = "Секретный вид", DescriptionRu = "Служебная сводка типов", NormalAvailable = true, ShinyAvailable = true }]);
        var view = new CollectionView(vm, state, new SpriteCache(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())),
            () => {}, () => {}, () => {}, () => null, () => false, () => false);
        ((Button)view.FindName("SpeciesModeButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        var grid = (ListBox)view.FindName("SpeciesGrid"); grid.SelectedIndex = 0;
        var fact = CollectionPresentation.Fact(1); Assert.NotNull(fact);
        Assert.Equal(fact, vm.GetSpeciesEntries(ownedShiny).Single().DescriptionRu);
        var closed = vm.GetSpeciesEntries(!ownedShiny).Single(); Assert.Null(closed.DescriptionRu);
        Assert.False(CollectionViewModel.MatchesSpeciesSearch(closed, fact));
        Assert.Equal(fact, ((TextBlock)view.FindName("SpeciesDetailDescription")).Text);
        Assert.True(view.OpenIndividual(pet.Id));
        Assert.Equal(fact, ((TextBlock)view.FindName("DetailDescriptionText")).Text);
        var other = new PokemonIndividual { SpeciesId = 1, CurrentSpeciesId = 1, FamilyRootId = 1, ReachedSpeciesIds = [1],
            EvolutionChainId = 1, ChainLength = 1, IsShiny = !ownedShiny, Nature = Nature.Hardy, CaughtAtUtc = DateTimeOffset.UtcNow };
        state.Collection.Add(other);
        Assert.Equal(vm.GetEntry(pet.Id, DateTimeOffset.UtcNow)!.DescriptionRu, vm.GetEntry(other.Id, DateTimeOffset.UtcNow)!.DescriptionRu);
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
    });
    [Theory]
    [InlineData(null)]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"facts\":[{\"speciesId\":1,\"textRu\":\" \"}]}")]
    [InlineData("{\"facts\":[{\"speciesId\":1,\"textRu\":\"English fallback\"}]}")]
    [InlineData("{\"facts\":[{\"speciesId\":\"1\",\"textRu\":\"Русский факт\"}]}")]
    [InlineData("{\"facts\":[{\"speciesId\":1026,\"textRu\":\"Русский факт\"}]}")]
    [InlineData("{\"facts\":[{\"speciesId\":1,\"textRu\":\"Русский факт\"},{\"speciesId\":1,\"textRu\":\"Дубликат\"}]}")]
    public void MissingOrMalformedDatasetFailsSafelyWithoutFiller(string? content)
    {
        var path = Path.Combine(Path.GetTempPath(), "facts-" + Guid.NewGuid() + ".json");
        try
        {
            if (content is not null) File.WriteAllText(path, content);
            var loader = typeof(CollectionPresentation).GetMethod("LoadFacts",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(loader);
            var result = Assert.IsAssignableFrom<IReadOnlyDictionary<int, string>>(loader.Invoke(null, [path]));
            Assert.Empty(result);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

}
