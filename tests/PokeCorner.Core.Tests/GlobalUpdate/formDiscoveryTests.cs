using System.Text.Json;
using System.Text.Json.Nodes;
using PokeCorner.Core;
using PokeCorner.Core.Domain;
using PokeCorner.Core.PokeApi;
using PokeCorner.Core.Persistence;
namespace PokeCorner.Core.Tests.GlobalUpdate;
public sealed class FormDiscoveryTests
{
    [Fact]
    public void NewProfileSerializesEmptyExactOrdinaryHistory()
    {
        using var json = JsonDocument.Parse(JsonSaveStore.Serialize(new SaveData { Individuals = [], Settings = new() }));
        Assert.True(json.RootElement.TryGetProperty("NormalSpeciesIds", out var normal));
        Assert.Equal(0, normal.GetArrayLength());
    }

    [Fact]
    public void LegacyMissingFieldInfersGlobalOnceAndPreservesEveryOtherValue()
    {
        var pet = new PokemonIndividual { SpeciesId = 133, EvolutionChainId = 67, ChainLength = 2, IsShiny = true,
            Nature = Nature.Hardy, CaughtAtUtc = DateTimeOffset.UtcNow, Nickname = "Старый друг", AccumulatedCarePoints = 1234 };
        var old = new SaveData { Individuals = [pet], Settings = new(), ShopCoins = 321, DiscoveredSpeciesIds = [1, 133, 134], ShinySpeciesIds = [133] };
        var json = JsonNode.Parse(JsonSaveStore.Serialize(old))!.AsObject(); json.Remove("NormalSpeciesIds");
        var loaded = LoadJson(json.ToJsonString());
        Assert.Equal(new[] { 1, 133, 134 }, loaded.NormalSpeciesIds.Order());
        Assert.Equal(321, loaded.ShopCoins); Assert.Equal(pet.Id, loaded.Individuals.Single().Id);
        Assert.True(loaded.Individuals.Single().IsShiny); Assert.Equal("Старый друг", loaded.Individuals.Single().Nickname);
        Assert.Equal(1234, loaded.Individuals.Single().AccumulatedCarePoints);
        loaded.NormalSpeciesIds.Clear(); // An explicitly present empty exact set is authoritative.
        var roundtrip = LoadJson(JsonSaveStore.Serialize(loaded));
        Assert.Empty(roundtrip.NormalSpeciesIds); Assert.Equal(old.DiscoveredSpeciesIds, roundtrip.DiscoveredSpeciesIds);
        Assert.Equal(old.ShinySpeciesIds, roundtrip.ShinySpeciesIds);
    }

    [Fact]
    public async Task ExactHistoryClonesAndRefusedCommitDoesNotPublishIt()
    {
        var data = new SaveData { Individuals = [], Settings = new(), NormalSpeciesIds = [25], ShopCoins = 123 };
        var clone = JsonSaveStore.Clone(data); clone.NormalSpeciesIds.Add(7);
        Assert.Equal(new[] { 25 }, data.NormalSpeciesIds);
        var app = new AppState(); app.Hydrate(data);
        var writer = new BackgroundSaveWriter(new JsonSaveStore(Path.Combine(Path.GetTempPath(), "form-refusal-" + Guid.NewGuid(), "state.json")), _ => throw new IOException("refused synthetic write"));
        await Assert.ThrowsAsync<IOException>(() => app.CommitAsync(writer, next => { next.NormalSpeciesIds.Add(7); next.ShopCoins = 0; }, () => 0));
        Assert.Equal(new[] { 25 }, app.NormalSpeciesIds); Assert.Equal(123, app.Shop.Coins);
    }

    [Theory]
    [InlineData(0)] [InlineData(-1)]
    public void ExactHistoryRejectsInvalidIds(int id)
    {
        var data = new SaveData { Individuals = [], Settings = new(), NormalSpeciesIds = [id] };
        Assert.Throws<JsonException>(() => JsonSaveStore.ValidateData(data));
    }

    [Fact]
    public void OldV2MissingFieldKeepsGlobalOpeningAndExactEmptyV2StaysEmpty()
    {
        var data = V2(); data.DiscoveredSpeciesIds = [1, 25]; data.ShinySpeciesIds = [25];
        var json = JsonNode.Parse(JsonSaveStore.Serialize(data))!.AsObject(); json.Remove("NormalSpeciesIds");
        var loaded = LoadJson(json.ToJsonString());
        Assert.Equal(new[] { 1, 25 }, loaded.NormalSpeciesIds.Order());
        loaded.NormalSpeciesIds.Clear(); Assert.Empty(LoadJson(JsonSaveStore.Serialize(loaded)).NormalSpeciesIds);
        Assert.Equal(new[] { 25 }, loaded.ShinySpeciesIds);
    }

    [Fact]
    public void CanonicalCaptureEvolutionHatchAndRehomePublishExactHistory()
    {
        using var fixture = new NurseryCoreTests(); var catalog = fixture.Catalog([1, 2], 1);
        var state = V2(); var shiny = Individual(catalog, 1, true);
        state.Encounters.Add(new EncounterState { Id = Guid.NewGuid(), Source = "activity", Phase = "resolved", Individual = shiny, Rarity = RarityTier.Uncommon });
        var catchService = new EncounterService(catalog);
        catchService.Open(state, state.Encounters.Single().Id);
        Assert.Contains(1, state.ShinySpeciesIds); Assert.Empty(state.NormalSpeciesIds);
        // Normal hatch uses canonical same-species recipe and frozen child; no ordinary capture history is fabricated.
        var a = Individual(catalog, 1); var b = Individual(catalog, 1); state.Individuals.AddRange([a, b]);
        var nursery = new NurseryService(catalog, state); var now = DateTimeOffset.UtcNow; var request = new NurseryRequest([a.Id, b.Id], null, Guid.NewGuid(), now);
        Assert.True(nursery.Start(request).Success);
        var hatched = nursery.Hatch(request.OperationId, Guid.NewGuid(), now.AddDays(1)); Assert.True(hatched.Success, hatched.Message);
        Assert.Contains(1, state.NormalSpeciesIds);
        var grown = state.Individuals.Single(p => p.Id == a.Id); grown.AccumulatedCarePoints = 196000;
        var evolved = new EvolutionService(catalog, state).Evolve(grown.Id, null, Guid.NewGuid()); Assert.True(evolved.Success, evolved.Message);
        Assert.Contains(2, state.NormalSpeciesIds); Assert.DoesNotContain(2, state.ShinySpeciesIds);
        state.ActiveIndividualId = b.Id;
        Assert.True(new RehomingService(catalog, state).Transfer(grown.Id, Guid.NewGuid()).Success);
        var loaded = LoadJson(JsonSaveStore.Serialize(state));
        Assert.Contains(2, loaded.NormalSpeciesIds); Assert.Contains(1, loaded.ShinySpeciesIds);
        Assert.DoesNotContain(loaded.Individuals, p => p.Id == grown.Id);
    }

    private static SaveData V2() => new()
    {
        SchemaVersion = 2, CatalogRevision = "test", BalanceRevision = "test", MigrationId = SaveMigration.MigrationId,
        Individuals = [], Settings = new(), Berries = SaveMigration.BerryIds.ToDictionary(id => id, _ => 100),
        Items = SaveMigration.ItemIds.ToDictionary(id => id, _ => 0),
        BerryRemainders = SaveMigration.BerryIds.ToDictionary(id => id, _ => new RationalRemainder())
    };
    private static PokemonIndividual Individual(CatalogPackage catalog, int id, bool shiny = false)
    {
        var species = catalog.Species.Single(s => s.Id == id);
        return new() { SpeciesId = id, CurrentSpeciesId = id, FamilyRootId = species.FamilyRootId, ReachedSpeciesIds = [id],
            EvolutionChainId = species.EvolutionChainId, ChainLength = 3, IsShiny = shiny, Nature = Nature.Hardy,
            CaughtAtUtc = DateTimeOffset.UtcNow, FirstTraitId = "gatherer" };
    }
    private static SaveData LoadJson(string json)
    {
        var root = Path.Combine(Path.GetTempPath(), "form-discovery-" + Guid.NewGuid()); Directory.CreateDirectory(root);
        try { var path = Path.Combine(root, "state.json"); File.WriteAllText(path, json); return new JsonSaveStore(path).Load()!; }
        finally { Directory.Delete(root, true); }
    }
}
