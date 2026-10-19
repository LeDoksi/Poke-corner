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


    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Fix1ActualOldV1MigrationPreservesHistoryAndExactOrdinaryAuthority(bool exactFieldPresent)
    {
        using var fixture = new NurseryCoreTests(); var catalog = fixture.Catalog([1, 2], 1, 2);
        File.Copy(Path.Combine(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../..")), "docs/design-v2/final/achievement-rewards.json"), Path.Combine(catalog.DataRoot, "final/achievement-rewards.json"));
        var pet = new PokemonIndividual { SpeciesId = 1, EvolutionChainId = 1, ChainLength = 2, CurrentStage = EvolutionStage.Stage2,
            IsShiny = true, Nature = Nature.Hardy, CaughtAtUtc = DateTimeOffset.Parse("2020-01-01T00:00:00Z"), Nickname = "История", AccumulatedCarePoints = 50000 };
        var old = new SaveData { Individuals = [pet], Settings = new(), ShopCoins = 77, FeedingTreats = 3,
            DiscoveredSpeciesIds = [25], ShinySpeciesIds = [133], NormalSpeciesIds = [] };
        var json = JsonNode.Parse(JsonSaveStore.Serialize(old))!.AsObject(); if (!exactFieldPresent) json.Remove("NormalSpeciesIds");
        var loaded = LoadJson(json.ToJsonString());
        var lookup = new LegacyLookup([new SpeciesEvolutionChain(1, 1, 45, 2, [1, 2], ["Bulbasaur", "Ivysaur"])], "[]"u8.ToArray());
        var migrated = SaveMigration.Migrate(loaded, lookup, catalog);
        Assert.Contains(25, migrated.DiscoveredSpeciesIds); Assert.Contains(133, migrated.ShinySpeciesIds);
        Assert.Equal(exactFieldPresent ? Array.Empty<int>() : new[] { 1, 2, 25 }, migrated.NormalSpeciesIds.Order());
        Assert.Contains(1, migrated.ShinySpeciesIds); Assert.Contains(2, migrated.ShinySpeciesIds);
        var retained = Assert.Single(migrated.Individuals); Assert.Equal(pet.Id, retained.Id); Assert.True(retained.IsShiny);
        Assert.Equal(EvolutionStage.Stage2, retained.CurrentStage); Assert.Equal(2, retained.CurrentSpeciesId); Assert.Equal("История", retained.Nickname);
        Assert.Equal(77 + migrated.AwardLedger.Values.Sum(a => a.CoinsGranted), migrated.ShopCoins); Assert.Equal(3, migrated.FeedingTreats); Assert.Equal(50000, retained.AccumulatedCarePoints);
        var roundtrip = LoadJson(JsonSaveStore.Serialize(migrated)); Assert.Equal(migrated.NormalSpeciesIds, roundtrip.NormalSpeciesIds);
        Assert.Contains(25, roundtrip.DiscoveredSpeciesIds); Assert.Contains(133, roundtrip.ShinySpeciesIds);
    }


    [Theory]
    [InlineData(1, 1, 66500)] [InlineData(1, 20, 63000)] [InlineData(1, 30, 59500)]
    [InlineData(2, 10, 66500)] [InlineData(2, 20, 63000)] [InlineData(2, 30, 59500)]
    public void Fix1FastGrowerCanonicalTierAndEitherSlotControlExactCareBoundary(int slot, int level, int expected)
    {
        using var fixture = new NurseryCoreTests(); var catalog = fixture.Catalog([1, 2]); var levels = new LevelProgression(catalog.DataRoot);
        var data = V2(); var pet = Individual(catalog, 1); data.Individuals.Add(pet); pet.TotalXp = levels.XpForLevel(level);
        var plain = new EvolutionService(catalog, data).Preview(pet.Id); Assert.Equal(70000, plain.CareRequired);
        if (slot == 1) pet.FirstTraitId = "fast-grower"; else pet.SecondTraitId = "fast-grower";
        pet.AccumulatedCarePoints = expected - 1;
        var preview = new EvolutionService(catalog, data).Preview(pet.Id); Assert.Equal(expected, preview.CareRequired);
        Assert.Equal(plain.BerryCost, preview.BerryCost); Assert.Equal(6, preview.BerryCost); Assert.NotNull(preview.Reason);
        Assert.False(new EvolutionService(catalog, data).Evolve(pet.Id, null, Guid.NewGuid()).Success);
        var berries = data.Berries[preview.BerryId]; pet.AccumulatedCarePoints = expected;
        Assert.Null(new EvolutionService(catalog, data).Preview(pet.Id).Reason);
        Assert.True(new EvolutionService(catalog, data).Evolve(pet.Id, null, Guid.NewGuid()).Success);
        Assert.Equal(expected, pet.AccumulatedCarePoints); Assert.Equal(berries - 6, data.Berries[preview.BerryId]);
        Assert.Equal(2, pet.CurrentSpeciesId); Assert.Equal(levels.XpForLevel(level), pet.TotalXp);
    }

    [Fact]
    public void Fix1FastGrowerRetainsCumulativeSecondStageCostAndUnavailableTerminalGuards()
    {
        using var fixture = new NurseryCoreTests(); var catalog = fixture.Catalog([1, 2, 3], 1);
        var data = V2(); var pet = Individual(catalog, 2); pet.ReachedSpeciesIds = [1, 2]; pet.CurrentStage = EvolutionStage.Stage2;
        pet.FirstTraitId = "fast-grower"; pet.TotalXp = new LevelProgression(catalog.DataRoot).XpForLevel(30); pet.AccumulatedCarePoints = 166600;
        data.Individuals.Add(pet); var preview = new EvolutionService(catalog, data).Preview(pet.Id);
        Assert.Equal(166600, preview.CareRequired); Assert.Null(preview.Reason);
        Assert.True(new EvolutionService(catalog, data).Evolve(pet.Id, null, Guid.NewGuid()).Success);
        Assert.Equal(166600, pet.AccumulatedCarePoints); var berries = data.Berries.Values.Sum();
        Assert.NotNull(new EvolutionService(catalog, data).Preview(pet.Id).Reason);
        Assert.False(new EvolutionService(catalog, data).Evolve(pet.Id, null, Guid.NewGuid()).Success); Assert.Equal(berries, data.Berries.Values.Sum());
        var unsupported = V2(); var shiny = Individual(catalog, 1, true); shiny.FirstTraitId = "fast-grower"; shiny.AccumulatedCarePoints = 196000; unsupported.Individuals.Add(shiny);
        Assert.All(new EvolutionService(catalog, unsupported).Preview(shiny.Id).Branches, b => Assert.False(b.Available));
        Assert.False(new EvolutionService(catalog, unsupported).Evolve(shiny.Id, null, Guid.NewGuid()).Success);
        Assert.Equal(1, shiny.CurrentSpeciesId); Assert.True(shiny.IsShiny); Assert.Equal(600, unsupported.Berries.Values.Sum());
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
