using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using PokeCorner.Core;
using PokeCorner.Core.Domain;
using PokeCorner.Core.PokeApi;
using PokeCorner.Core.Persistence;

namespace PokeCorner.App.Tests.GlobalUpdate;

public sealed class MigrationTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "MigrationTest_" + Guid.NewGuid());
    private static readonly string DataRoot = Path.Combine(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..")), "src/PokeCorner.App/bin", Directory.GetParent(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))!.Name, "net8.0-windows/Data");
    private static readonly Lazy<CatalogPackage> Package = new(() => CatalogPackage.Load(DataRoot));
    private static LegacyLookup Lookup => LegacyLookup.Load(DataRoot);
    private string V1 => Path.Combine(root, "state.json");
    private string V2 => Path.Combine(root, "state-v2.json");
    public MigrationTests() => Directory.CreateDirectory(root);
    public void Dispose() => Directory.Delete(root, true);
    private static PokemonIndividual Pet(int species = 1, int chain = 1, int length = 3, EvolutionStage stage = EvolutionStage.Stage1) => new()
    {
        SpeciesId = species, EvolutionChainId = chain, ChainLength = length, CurrentStage = stage,
        Nature = Nature.Hardy, IsShiny = true, CaughtAtUtc = DateTimeOffset.Parse("2020-01-02T03:04:05Z"),
        IsFavorite = true, Nickname = "Sunny", AccumulatedCarePoints = 200000
    };
    private static SaveData Old(params PokemonIndividual[] pets) => new()
    {
        Individuals = pets.ToList(), Settings = new() { CareMode = CareMode.Hardcore, SuperHardcoreEnabled = true, MonitorIndex = 999, CrySoundVolume = 0 },
        ActiveIndividualId = pets.FirstOrDefault()?.Id, ShopCoins = 17, FeedingTreats = 8, CatchAccumulatorUnits = 12,
        FeedingRemainderUnits = 11, ShopRemainderUnits = 10, PendingCatchCount = 2, ShopCatchLuckyCharmActive = true
    };

    [Fact]
    public async Task CommitPublishBarrierDefersStalePostFlushExitAndActivity()
    {
        var writes = new List<Action>();
        var store = new JsonSaveStore(V2);
        var delayed = true;
        var writer = new BackgroundSaveWriter(store, action => { if (delayed) writes.Add(action); else action(); });
        var state = new AppState();
        state.Hydrate(Old(Pet()));
        var commit = state.CommitAsync(writer, snapshot => snapshot.ShopCoins += 5, () => 2);
        var originalCare = state.Collection.GetActive()!.AccumulatedCarePoints;
        state.ApplyActivityUnits(23);
        state.RunWhenReady(() => state.Shop.Coins += 1);
        writer.Post(state.ToSaveData(2));
        writer.Flush(state.ToSaveData(2));
        var exit = state.FlushAsync(writer, () => 2);
        Assert.False(exit.IsCompleted);
        Assert.Equal(17, state.Shop.Coins);
        Assert.Equal(originalCare, state.Collection.GetActive()!.AccumulatedCarePoints);
        Assert.Single(writes);
        delayed = false;
        writes[0]();
        await commit;
        await exit;
        Assert.Equal(23, state.Shop.Coins);
        Assert.Equal(23, store.Load()!.ShopCoins);
        Assert.Equal(originalCare + 23, store.Load()!.Individuals[0].AccumulatedCarePoints);
    }

    [Fact]
    public async Task FailedCommitKeepsLiveAndDiskThenReplaysActivity()
    {
        var state = new AppState(); state.Hydrate(Old(Pet()));
        var store = new JsonSaveStore(V2); store.Save(state.ToSaveData(2));
        Directory.CreateDirectory(V2 + ".tmp");
        var writes = new List<Action>(); var writer = new BackgroundSaveWriter(store, writes.Add);
        var task = state.CommitAsync(writer, next => next.ShopCoins = 50, () => 2);
        state.ApplyActivityUnits(7); writes[0]();
        await Assert.ThrowsAnyAsync<IOException>(() => task);
        Assert.Equal(17, state.Shop.Coins); Assert.Equal(17, store.Load()!.ShopCoins);
        Assert.Equal(200007, state.Collection.GetActive()!.AccumulatedCarePoints);
        Assert.False(state.IsCommitting);
    }

    [Fact]
    public void CosmeticRollbackKeepsActualReachedFormAndInputUntouched()
    {
        var pet = Pet(stage: EvolutionStage.Stage3); pet.DisplayStageOverride = 0;
        var v1 = Old(pet); var before = JsonSaveStore.Serialize(v1);
        var next = SaveMigration.Migrate(v1, Lookup, Package.Value);
        var p = next.Individuals[0];
        Assert.Equal(3, p.CurrentSpeciesId); Assert.Equal(1, p.FamilyRootId);
        Assert.Equal(new[] { 1, 2, 3 }, p.ReachedSpeciesIds);
        Assert.Equal(0, p.DisplayStageOverride); Assert.Equal(0, p.TotalXp);
        Assert.Equal(pet.Id, p.Id); Assert.True(p.IsShiny); Assert.Equal(pet.Nature, p.Nature);
        Assert.Equal(pet.CaughtAtUtc, p.CaughtAtUtc); Assert.Equal(pet.Nickname, p.Nickname);
        Assert.Equal(pet.AccumulatedCarePoints, p.AccumulatedCarePoints);
        Assert.Equal(before, JsonSaveStore.Serialize(v1));
        Assert.Equal(1, p.LegacyOrigin!.ChainId); Assert.Equal(Lookup.Sha256, p.LegacyOrigin.LookupSha256);
        Assert.DoesNotContain("fast-grower", p.FirstTraitId!);
        Assert.Equal("unknown", p.AcquisitionSource); Assert.Null(next.Metrics["successful-catches"]);
        Assert.DoesNotContain("trophy-care-1", next.AwardLedger.Keys);
    }

    [Fact]
    public void EeveeUsesExactSavedLookupPath()
    {
        var chosen = new SpeciesEvolutionChain(67, 133, 45, 2, new[] { 133, 135 }, new[] { "Eevee", "Jolteon" });
        var lookup = new LegacyLookup(new[] { chosen }, new byte[] { 1, 2, 3 });
        var next = SaveMigration.Migrate(Old(Pet(133, 67, 2, EvolutionStage.Stage2)), lookup, Package.Value);
        Assert.Equal(135, next.Individuals[0].CurrentSpeciesId);
        Assert.DoesNotContain(134, next.DiscoveredSpeciesIds);
        var ambiguous = new LegacyLookup(new[] { chosen, chosen with { StageSpeciesIds = new[] { 133, 134 } } }, new byte[] { 1 });
        Assert.Throws<InvalidDataException>(() => SaveMigration.Migrate(Old(Pet(133, 67, 2, EvolutionStage.Stage2)), ambiguous, Package.Value));
    }

    [Fact]
    public void LargeCollectionPreservesAllIdsAndProvenAwardsOnce()
    {
        var old = Old(Enumerable.Range(0, 100).Select(_ => Pet()).ToArray());
        var next = SaveMigration.Migrate(old, Lookup, Package.Value);
        Assert.Equal(old.Individuals.Select(p => p.Id), next.Individuals.Select(p => p.Id));
        Assert.Equal(100, next.Individuals.Count);
        Assert.Contains("trophy-population-4", next.AwardLedger.Keys);
        Assert.DoesNotContain("trophy-catches-1", next.AwardLedger.Keys);
        Assert.Equal(17 + next.AwardLedger.Values.Sum(a => a.CoinsGranted), next.ShopCoins);
        Assert.All(next.Items.Where(p => p.Key != "treats").Select(p => p.Value), n => Assert.Equal(0, n)); Assert.Equal(8, next.Items["treats"]); Assert.All(next.Berries.Values, n => Assert.Equal(0, n));
        Assert.Equal(6, next.Items.Count); Assert.Equal(6, next.Berries.Count);
        Assert.All(next.BerryRemainders.Values, n => { Assert.Equal(0, n.Numerator); Assert.Equal(1, n.Denominator); });
        Assert.Equal(2, next.Gift.Gifts.Count); Assert.NotEqual(next.Gift.Gifts[0].Individual.SpeciesId, next.Gift.Gifts[1].Individual.SpeciesId);
        Assert.All(next.Gift.Gifts, g => { Assert.Equal("reserved", g.Status); Assert.NotEqual(1, g.Individual.SpeciesId); Assert.Equal(g.Individual.SpeciesId, Package.Value.Species.Single(s => s.Id == g.Individual.SpeciesId).FamilyRootId); });
    }

    [Fact]
    public void EmptyAndNullActiveAndUnavailableRetainSemantics()
    {
        var empty = SaveMigration.Migrate(Old(), Lookup, Package.Value);
        Assert.Empty(empty.Individuals); Assert.Equal(17, empty.ShopCoins); Assert.Equal(8, empty.FeedingTreats);
        Assert.Empty(empty.Gift.Gifts);
        var old = Old(Pet()); old.ActiveIndividualId = null;
        var next = SaveMigration.Migrate(old, Lookup, Package.Value);
        Assert.Null(next.ActiveIndividualId); Assert.Single(next.Individuals); Assert.Empty(next.Gift.Gifts);
        old.ActiveIndividualId = old.Individuals[0].Id;
        old.CareStates[old.ActiveIndividualId.Value] = new() { RanAwayUntilUtc = DateTimeOffset.UtcNow.AddHours(1) };
        next = SaveMigration.Migrate(old, Lookup, Package.Value);
        Assert.Null(next.CareStates[old.ActiveIndividualId.Value].RanAwayUntilUtc);
        Assert.Equal(CareMode.Chill, next.Settings.CareMode);
        Assert.False(next.Settings.SuperHardcoreEnabled);
        Assert.Equal(old.ActiveIndividualId, next.ActiveIndividualId);
    }

    [Fact]
    public void SettingsCareCharmEggsAndEarnedEncountersPreserved()
    {
        var old = Old(Pet()); old.Settings.EffectsSoundVolume = 0;
        old.CareStates[old.Individuals[0].Id] = new() { LastInteractionUtc = DateTimeOffset.UtcNow.AddDays(-5), LastPetUtc = DateTimeOffset.UtcNow.AddDays(-5), LastNeglectCheckUtc = null };
        old.PendingEggs.Add(new() { Tier = RarityTier.Common, ReadyAtUtc = DateTimeOffset.UtcNow.AddHours(1) });
        var next = SaveMigration.Migrate(old, Lookup, Package.Value);
        Assert.Equal(CareMode.Chill, next.Settings.CareMode); Assert.False(next.Settings.SuperHardcoreEnabled);
        Assert.Equal(old.Settings.MonitorIndex, next.Settings.MonitorIndex);
        Assert.Equal(0, next.Settings.CrySoundVolume); Assert.Equal(0, next.Settings.EffectsSoundVolume);
        Assert.Equal(8, next.FeedingTreats); Assert.Equal(11, next.FeedingRemainderUnits); Assert.Equal(10, next.ShopRemainderUnits);
        Assert.Equal(12, next.CatchAccumulatorUnits); Assert.Equal(2, next.PendingCatchCount);
        Assert.True(next.ShopCatchLuckyCharmActive); Assert.Empty(next.PendingEggs); Assert.Single(old.PendingEggs);
        Assert.Equal(old.Individuals[0].Id, next.Individuals.Single().Id);
        Assert.True(next.Individuals.Single().IsShiny);
        var care = next.CareStates[old.Individuals[0].Id];
        Assert.Null(care.LastNeglectCheckUtc); Assert.Equal(old.CareStates[old.Individuals[0].Id].LastPetUtc, care.LastPetUtc);
        Assert.Equal(old.CareStates[old.Individuals[0].Id].LastInteractionUtc, care.LastInteractionUtc);
    }

    [Fact]
    public async Task DurableMigrationBacksUpExactBytesAndExistingV2DoesNotRepeat()
    {
        var old = Old(Pet()); File.WriteAllText(V1, JsonSaveStore.Serialize(old));
        var bytes = File.ReadAllBytes(V1);
        var first = await SaveMigration.LoadAsync(V1, V2, Lookup, Package.Value);
        Assert.Equal(2, first!.SchemaVersion); Assert.Equal(bytes, File.ReadAllBytes(V1));
        Assert.Contains(Directory.GetFiles(root, "state.json", SearchOption.AllDirectories), p => p != V1 && File.ReadAllBytes(p).SequenceEqual(bytes));
        var saved = new JsonSaveStore(V2).Load()!;
        saved.Gift.Gifts[0].Status = "opened";
        saved.CompletedOperations["gift-open-test"] = saved.Gift.Gifts[0].Individual.Id;
        new JsonSaveStore(V2).Save(saved);
        var disk = File.ReadAllBytes(V2);
        var again = await SaveMigration.LoadAsync(V1, V2, Lookup, Package.Value);
        Assert.Equal(disk, File.ReadAllBytes(V2));
        Assert.Equal("opened", again!.Gift.Gifts[0].Status); Assert.Equal(first.AwardLedger.Count, again.AwardLedger.Count);
        File.WriteAllText(V2, "{bad v2"); var invalid = File.ReadAllBytes(V2);
        await Assert.ThrowsAsync<SaveLoadException>(() => SaveMigration.LoadAsync(V1, V2, Lookup, Package.Value));
        Assert.Equal(invalid, File.ReadAllBytes(V2)); Assert.Equal(bytes, File.ReadAllBytes(V1));
    }

    [Fact]
    public async Task InterruptedLegacyStagingBeforeManifestResumesFromIntactV1()
    {
        File.WriteAllText(V1, JsonSaveStore.Serialize(Old(Pet())));
        var original = File.ReadAllBytes(V1);
        var hash = Convert.ToHexString(SHA256.HashData(original)).ToLowerInvariant();
        var partial = Path.Combine(root, "legacy-v1", hash);
        Directory.CreateDirectory(partial);
        File.WriteAllBytes(Path.Combine(partial, "state.json"), original);
        File.WriteAllBytes(Path.Combine(partial, LegacyLookup.PreservedLookupFileName), Lookup.OriginalBytes);
        Assert.False(File.Exists(Path.Combine(partial, "manifest.json")));

        var recovered = await SaveMigration.LoadAsync(V1, V2, Lookup, Package.Value);
        Assert.Single(recovered!.Individuals);
        Assert.Equal(original, File.ReadAllBytes(V1));
        Assert.Equal(original, File.ReadAllBytes(Path.Combine(partial, "state.json")));
        Assert.False(File.Exists(Path.Combine(partial, "manifest.json")));
        Assert.Contains(new JsonSaveStore(V2).History.List(), entry => entry.IsLegacy && entry.CollectionCount == 1 && !string.Equals(Path.GetDirectoryName(entry.Path), partial, StringComparison.OrdinalIgnoreCase));
        File.Delete(V2);
        await Assert.ThrowsAsync<SaveLoadException>(() => SaveMigration.LoadAsync(V1, V2, Lookup, Package.Value));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingActiveV1WithIncompleteStagingNeverStartsNewGame(bool hasPartialState)
    {
        var partial = Path.Combine(root, "legacy-v1", "interrupted");
        Directory.CreateDirectory(partial);
        var state = Path.Combine(partial, "state.json");
        var original = System.Text.Encoding.UTF8.GetBytes(JsonSaveStore.Serialize(Old(Pet())));
        if (hasPartialState) File.WriteAllBytes(state, original);

        await Assert.ThrowsAsync<SaveLoadException>(() => SaveMigration.LoadAsync(V1, V2, Lookup, Package.Value));
        Assert.False(File.Exists(V1)); Assert.False(File.Exists(V2));
        Assert.True(Directory.Exists(partial));
        if (hasPartialState) Assert.Equal(original, File.ReadAllBytes(state));
    }

    [Fact]
    public async Task CorruptManifestNeverAllowsAutomaticStaleV1Remigration()
    {
        File.WriteAllText(V1, JsonSaveStore.Serialize(Old(Pet())));
        var original = File.ReadAllBytes(V1);
        var hash = Convert.ToHexString(SHA256.HashData(original)).ToLowerInvariant();
        var partial = Path.Combine(root, "legacy-v1", hash);
        Directory.CreateDirectory(partial);
        File.WriteAllBytes(Path.Combine(partial, "state.json"), original);
        File.WriteAllBytes(Path.Combine(partial, LegacyLookup.PreservedLookupFileName), Lookup.OriginalBytes);
        const string brokenManifest = "{interrupted manifest";
        File.WriteAllText(Path.Combine(partial, "manifest.json"), brokenManifest);

        await Assert.ThrowsAsync<SaveLoadException>(() => SaveMigration.LoadAsync(V1, V2, Lookup, Package.Value));
        Assert.Equal(original, File.ReadAllBytes(V1));
        Assert.Equal(brokenManifest, File.ReadAllText(Path.Combine(partial, "manifest.json")));
        Assert.False(File.Exists(V2));
    }

    [Fact]
    public async Task MissingV2AfterMigrationRefusesStaleV1()
    {
        var original = Old(Enumerable.Range(0, 100).Select(_ => Pet()).ToArray());
        File.WriteAllText(V1, JsonSaveStore.Serialize(original));
        await SaveMigration.LoadAsync(V1, V2, Lookup, Package.Value);
        File.Delete(V2);
        await Assert.ThrowsAsync<SaveLoadException>(() => SaveMigration.LoadAsync(V1, V2, Lookup, Package.Value));
        Assert.False(File.Exists(V2));
        var recovery = new JsonSaveStore(V2);
        var checkpoint = recovery.History.List().First(e => e.SchemaVersion == 2 && e.CollectionCount == 100);
        SaveMigration.RestoreHistory(recovery, checkpoint, Package.Value, DataRoot);
        var restored = await SaveMigration.LoadAsync(V1, V2, Lookup, Package.Value);
        Assert.Equal(original.Individuals.Select(i => i.Id), restored!.Individuals.Select(i => i.Id));
        Assert.All(restored.Individuals, pet => Assert.True(pet.IsShiny));
    }

    [Fact]
    public async Task MissingV2WithOnlyImmutableMigrationEvidenceRefusesRemigration()
    {
        File.WriteAllText(V1, JsonSaveStore.Serialize(Old(Pet())));
        await SaveMigration.LoadAsync(V1, V2, Lookup, Package.Value);
        File.Delete(V2); Directory.Delete(V2 + ".history", true);
        await Assert.ThrowsAsync<SaveLoadException>(() => SaveMigration.LoadAsync(V1, V2, Lookup, Package.Value));
        Assert.False(File.Exists(V2));
    }

    [Fact]
    public async Task LegacyHistoryRestoreValidatesLookupAndPreservesV2()
    {
        File.WriteAllText(V1, JsonSaveStore.Serialize(Old(Pet())));
        var legacyBytes = File.ReadAllBytes(V1);
        var store = new JsonSaveStore(V2);
        await SaveMigration.LoadAsync(V1, V2, Lookup, Package.Value, store);
        var original = store.Load()!; original.ShopCoins += 10; store.Save(original);
        var legacy = store.History.List().Single(e => e.IsLegacy);
        SaveMigration.RestoreHistory(store, legacy, Package.Value, DataRoot);
        var restored = store.Load()!;
        Assert.Equal(2, restored.SchemaVersion);
        Assert.Equal(CareMode.Chill, restored.Settings.CareMode);
        Assert.False(restored.Settings.SuperHardcoreEnabled);
        Assert.Equal(legacyBytes, File.ReadAllBytes(V1));
        Assert.Equal(legacyBytes, File.ReadAllBytes(legacy.Path));
        Assert.Contains(store.History.List(), e => e.SchemaVersion == 2 && e.Coins == original.ShopCoins);
        var lookupPath = Path.Combine(Path.GetDirectoryName(legacy.Path)!, LegacyLookup.PreservedLookupFileName);
        File.WriteAllText(lookupPath, "[]"); var before = File.ReadAllBytes(V2);
        Assert.ThrowsAny<IOException>(() => SaveMigration.RestoreHistory(store, legacy, Package.Value, DataRoot));
        Assert.Equal(before, File.ReadAllBytes(V2));
    }

    [Fact]
    public void RestoreQuarantinesCatalogInvalidDisplacedPrimary()
    {
        var store = new JsonSaveStore(V2);
        store.Save(SaveMigration.Migrate(Old(Pet()), Lookup, Package.Value));
        var selected = store.History.List().First(entry => entry.SchemaVersion == 2);
        var node = JsonNode.Parse(File.ReadAllBytes(V2))!;
        node["Individuals"]![0]!["CurrentSpeciesId"] = 99999;
        node["Individuals"]![0]!["ReachedSpeciesIds"] = JsonNode.Parse("[99999]");
        var invalid = System.Text.Encoding.UTF8.GetBytes(node.ToJsonString());
        File.WriteAllBytes(V2, invalid);
        var structurallyValid = new JsonSaveStore(V2).Load()!;
        Assert.Throws<SaveLoadException>(() => SaveMigration.ValidateCatalog(structurallyValid, Package.Value, V2));

        SaveMigration.RestoreHistory(store, selected, Package.Value, DataRoot);
        Assert.Equal(invalid, File.ReadAllBytes(Assert.Single(Directory.GetFiles(root, "state-v2.json.rejected-*"))));
        Assert.DoesNotContain(store.History.List(), entry => entry.Sha256 == Convert.ToHexString(SHA256.HashData(invalid)).ToLowerInvariant());
        Assert.Single(new JsonSaveStore(V2).Load()!.Individuals);
    }

    [Fact]
    public async Task ConfirmedResetCommitsPendingStarterAndKeepsPreviousHistory()
    {
        var store = new JsonSaveStore(V2); var writer = new BackgroundSaveWriter(store);
        var state = new AppState(); state.Hydrate(SaveMigration.Migrate(Old(Pet()), Lookup, Package.Value));
        await state.FlushAsync(writer, () => 0);
        var reset = SaveMigration.Migrate(Old(), Lookup, Package.Value);
        await state.CommitAsync(writer, next => next.StarterSelectionPending = true, () => 0, replacement: reset);
        var restarted = await SaveMigration.LoadAsync(V1, V2, Lookup, Package.Value);
        Assert.True(restarted!.StarterSelectionPending); Assert.Empty(restarted.Individuals);
        Assert.Contains(store.History.List(), e => e.CollectionCount == 1);
        await state.CommitAsync(writer, next => next.StarterSelectionPending = false, () => 0);
        Assert.False(store.Load()!.StarterSelectionPending);
    }

    [Fact]
    public async Task FailedAtomicMigrationLeavesV1AndBackupExact()
    {
        File.WriteAllText(V1, JsonSaveStore.Serialize(Old(Pet())));
        var original = File.ReadAllBytes(V1);
        // Clock is reached after migration backup and before the durable temp commit.
        var store = new JsonSaveStore(V2, () => { Directory.CreateDirectory(V2 + ".tmp"); return DateTimeOffset.UtcNow; });
        await Assert.ThrowsAnyAsync<UnauthorizedAccessException>(() => SaveMigration.LoadAsync(V1, V2, Lookup, Package.Value, store));
        Assert.False(File.Exists(V2)); Assert.Equal(original, File.ReadAllBytes(V1));
        Assert.Contains(Directory.GetFiles(root, "state.json", SearchOption.AllDirectories), p => p != V1 && File.ReadAllBytes(p).SequenceEqual(original));
        Directory.Delete(V2 + ".tmp");
        await Assert.ThrowsAsync<SaveLoadException>(() => SaveMigration.LoadAsync(V1, V2, Lookup, Package.Value));
        var recovery = new JsonSaveStore(V2);
        var checkpoint = recovery.History.List().First(e => e.SchemaVersion == 2);
        SaveMigration.RestoreHistory(recovery, checkpoint, Package.Value, DataRoot);
        Assert.NotNull(await SaveMigration.LoadAsync(V1, V2, Lookup, Package.Value));
    }

    [Theory]
    [InlineData("Individuals", "null")]
    [InlineData("Settings", "null")]
    [InlineData("CareStates", "null")]
    [InlineData("PendingEggs", "null")]
    [InlineData("Individuals", "[null]")]
    [InlineData("PendingEggs", "[null]")]
    [InlineData("SchemaVersion", "3")]
    [InlineData("ShopCoins", "-1")]
    [InlineData("ShopCoins", "2147483648")]
    [InlineData("CatchAccumulatorUnits", "9223372036854775808")]
    [InlineData("ActiveIndividualId", "\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\"")]
    public async Task InvalidInputNeverWrites(string field, string value)
    {
        var json = JsonNode.Parse(JsonSaveStore.Serialize(Old(Pet())))!;
        json[field] = JsonNode.Parse(value); File.WriteAllText(V1, json.ToJsonString());
        var bytes = File.ReadAllBytes(V1);
        await Assert.ThrowsAsync<SaveLoadException>(() => SaveMigration.LoadAsync(V1, V2, Lookup, Package.Value));
        Assert.Equal(bytes, File.ReadAllBytes(V1)); Assert.False(File.Exists(V2));
    }

    [Theory]
    [InlineData("Id", "null")]
    [InlineData("Id", "\"00000000-0000-0000-0000-000000000000\"")]
    [InlineData("CurrentStage", "3")]
    [InlineData("DisplayStageOverride", "2")]
    [InlineData("EvolutionChainId", "99999")]
    public async Task InvalidIndividualNeverWrites(string field, string value)
    {
        var node = JsonNode.Parse(JsonSaveStore.Serialize(Old(Pet())))!;
        node["Individuals"]![0]![field] = JsonNode.Parse(value); File.WriteAllText(V1, node.ToJsonString());
        var original = File.ReadAllBytes(V1);
        await Assert.ThrowsAnyAsync<IOException>(() => SaveMigration.LoadAsync(V1, V2, Lookup, Package.Value));
        Assert.Equal(original, File.ReadAllBytes(V1)); Assert.False(File.Exists(V2));
    }

    [Fact]
    public void UnsupportedExistingShinyMigratesWithoutDowngrade()
    {
        var species = Package.Value.Species.First(s => s.NormalAvailable && !s.ShinyAvailable);
        var chain = new SpeciesEvolutionChain(99999, species.Id, 45, 1, new[] { species.Id }, new[] { species.DisplayName });
        var next = SaveMigration.Migrate(Old(Pet(species.Id, 99999, 1)), new(new[] { chain }, new byte[] { 1 }), Package.Value);
        Assert.True(next.Individuals[0].IsShiny); Assert.Equal(species.Id, next.Individuals[0].CurrentSpeciesId);
        Assert.False(Package.Value.CanObtain(species.Id, true));
    }

    [Fact]
    public void RetroCoinOverflowFailsWithoutClamping() { var old = Old(Pet()); old.ShopCoins = int.MaxValue; Assert.Throws<InvalidDataException>(() => SaveMigration.Migrate(old, Lookup, Package.Value)); }

    [Fact]
    public void SeparateV2Path() => Assert.Equal(Path.Combine(root, "state-v2.json"), AppPaths.GetV2SaveFilePath(root));
    [Fact]
    public async Task MissingDuplicateGuidAndCorruptJsonLeaveBytesUnchanged()
    {
        foreach (var kind in new[] { "missing", "duplicate", "corrupt", "null-care" })
        {
            var node = JsonNode.Parse(JsonSaveStore.Serialize(Old(Pet(), Pet())))!;
            if (kind == "missing") node["Individuals"]![0]!.AsObject().Remove("Id");
            if (kind == "duplicate") node["Individuals"]![1]!["Id"] = node["Individuals"]![0]!["Id"]!.DeepClone();
            if (kind == "null-care") node["CareStates"]![Guid.NewGuid().ToString()] = null;
            File.WriteAllText(V1, kind == "corrupt" ? "{bad json" : node.ToJsonString());
            var original = File.ReadAllBytes(V1);
            await Assert.ThrowsAsync<SaveLoadException>(() => SaveMigration.LoadAsync(V1, V2, Lookup, Package.Value));
            Assert.Equal(original, File.ReadAllBytes(V1)); Assert.False(File.Exists(V2));
        }
    }

    [Theory]
    [InlineData("FirstTraitId", "null")]
    [InlineData("TotalXp", "6401")]
    [InlineData("CurrentSpeciesId", "0")]
    [InlineData("FamilyRootId", "0")]
    public async Task InvalidV2IndividualDoesNotImportV1(string field, string value)
    {
        File.WriteAllText(V1, JsonSaveStore.Serialize(Old(Pet())));
        var node = JsonNode.Parse(JsonSaveStore.Serialize(SaveMigration.Migrate(Old(Pet()), Lookup, Package.Value)))!;
        node["Individuals"]![0]![field] = JsonNode.Parse(value);
        File.WriteAllText(V2, node.ToJsonString()); var before = File.ReadAllBytes(V2);
        await Assert.ThrowsAsync<SaveLoadException>(() => SaveMigration.LoadAsync(V1, V2, Lookup, Package.Value));
        Assert.Equal(before, File.ReadAllBytes(V2));
    }

    [Fact]
    public void PreservedLookupUsesExactSavedBranchAndImmutableBackupCannotBeReplaced()
    {
        var customPath = Path.Combine(root, LegacyLookup.PreservedLookupFileName);
        var chains = new[] { new SpeciesEvolutionChain(67, 133, 45, 2, new[] { 133, 135 }, new[] { "Eevee", "Jolteon" }) };
        File.WriteAllText(customPath, JsonSerializer.Serialize(chains));
        var lookup = LegacyLookup.Load(DataRoot, preservedLookupPath: customPath);
        Assert.Equal(File.ReadAllBytes(customPath), lookup.OriginalBytes);
        Assert.Equal(135, SaveMigration.Migrate(Old(Pet(133, 67, 2, EvolutionStage.Stage2)), lookup, Package.Value).Individuals[0].CurrentSpeciesId);
        File.WriteAllText(customPath, "{broken");
        Assert.Throws<JsonException>(() => LegacyLookup.Load(DataRoot, preservedLookupPath: customPath));
    }

    [Fact]
    public async Task NoFilesIsNewGameButExistingEmptySaveIsNot()
    {
        Assert.Null(await SaveMigration.LoadAsync(V1, V2, Lookup, Package.Value));
        new JsonSaveStore(V1).Save(Old());
        var loaded = await SaveMigration.LoadAsync(V1, V2, Lookup, Package.Value);
        Assert.NotNull(loaded); Assert.Equal(17, loaded.ShopCoins);
        Assert.Equal(30, loaded.Settings.EffectsSoundVolume);
    }

    [Fact]
    public async Task V2RoundTripHydrationPreservesReservedPropertiesAndAwards()
    {
        var migrated = SaveMigration.Migrate(Old(Pet()), Lookup, Package.Value);
        new JsonSaveStore(V2).Save(migrated);
        var loaded = await SaveMigration.LoadAsync(V1, V2, Lookup, Package.Value);
        var state = new AppState(); state.Hydrate(loaded!);
        Assert.Equal(JsonSaveStore.Serialize(migrated), JsonSaveStore.Serialize(state.ToSaveData(migrated.PendingCatchCount)));
        Assert.Equal(Package.Value.CatalogRevision, loaded!.CatalogRevision);
    }

    [Fact]
    public async Task UiContinuationRemainsFreeWhileCommitAndExitWait()
    {
        Task commit = Task.CompletedTask, exit = Task.CompletedTask;
        AppState state = null!;
        var write = new TaskCompletionSource<Action>(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new JsonSaveStore(V2);
        var first = true;
        var writer = new BackgroundSaveWriter(store, action => { if (first) { first = false; write.SetResult(action); } else Task.Run(action); });
        PokeCorner.App.Tests.Theme.WpfTestHost.Run(() =>
        {
            state = new AppState(); state.Hydrate(Old(Pet()));
            commit = state.CommitAsync(writer, next => next.ShopCoins += 2, () => 2);
            exit = state.FlushAsync(writer, () => 2);
            Assert.False(commit.IsCompleted); Assert.False(exit.IsCompleted);
        });
        var free = false;
        PokeCorner.App.Tests.Theme.WpfTestHost.Run(() => { free = true; state.RunWhenReady(() => state.Shop.Coins += 3); });
        Assert.True(free);
        await Task.Run(await write.Task);
        await commit.WaitAsync(TimeSpan.FromSeconds(10));
        await exit.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(22, store.Load()!.ShopCoins);
    }

    [Fact]
    public async Task ConsecutiveCommitsUsePublishedSnapshotAndExitWaitsForBoth()
    {
        AppState state = null!;
        Task one = Task.CompletedTask, two = Task.CompletedTask, exit = Task.CompletedTask;
        var firstWrite = new TaskCompletionSource<Action>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondWrite = new TaskCompletionSource<Action>(TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0; var store = new JsonSaveStore(V2);
        var writer = new BackgroundSaveWriter(store, action =>
        {
            switch (++count)
            {
                case 1: firstWrite.SetResult(action); break;
                case 2: secondWrite.SetResult(action); break;
                default: Task.Run(action); break;
            }
        });
        PokeCorner.App.Tests.Theme.WpfTestHost.Run(() =>
        {
            state = new AppState(); state.Hydrate(Old(Pet()));
            one = state.CommitAsync(writer, next => next.ShopCoins += 2, () => 2);
            two = state.CommitAsync(writer, next => next.ShopCoins += 3, () => 2);
            exit = state.FlushAsync(writer, () => 2);
            Assert.False(exit.IsCompleted);
        });
        await Task.Run(await firstWrite.Task);
        await secondWrite.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(exit.IsCompleted);
        await Task.Run(await secondWrite.Task);
        await Task.WhenAll(one, two, exit).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(22, state.Shop.Coins); Assert.Equal(22, store.Load()!.ShopCoins);
    }

    // This test pumps a deliberately reordered single-thread SynchronizationContext.
#pragma warning disable xUnit1031
    [Fact]
    public void ExitWaitsForEarlierQueuedCommitEvenWhenItsUiContinuationRunsFirst()
    {
        var previous = SynchronizationContext.Current;
        var context = new ReverseContinuationContext();
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            var state = new AppState(); state.Hydrate(Old(Pet()));
            var firstWrite = new TaskCompletionSource<Action>(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondWrite = new TaskCompletionSource<Action>(TaskCreationOptions.RunContinuationsAsynchronously);
            var writes = 0; var store = new JsonSaveStore(V2);
            var writer = new BackgroundSaveWriter(store, action =>
            {
                switch (++writes)
                {
                    case 1: firstWrite.SetResult(action); break;
                    case 2: secondWrite.SetResult(action); break;
                    default: Task.Run(action); break;
                }
            });
            var one = state.CommitAsync(writer, next => next.ShopCoins += 2, () => 2);
            var two = state.CommitAsync(writer, next => next.ShopCoins += 3, () => 2);
            var exit = state.FlushAsync(writer, () => 2);
            Task.Run(firstWrite.Task.GetAwaiter().GetResult()).GetAwaiter().GetResult();
            context.RunLast(); // first publication queues both waiters
            // The fixed barrier keeps exit on the drain task. Before the fix both
            // waiters are queued; run exit first to expose its stale snapshot.
            if (context.TryWaitForCount(2, TimeSpan.FromSeconds(1))) context.RunLast();
            Assert.False(exit.IsCompleted);
            Assert.Equal(1, writes); // no stale 19-coin flush submitted
            context.RunLast(); // second commit now enters its slot
            Task.Run(secondWrite.Task.GetAwaiter().GetResult()).GetAwaiter().GetResult();
            context.PumpUntil(() => Task.WhenAll(one, two, exit).IsCompleted);
            Task.WhenAll(one, two, exit).GetAwaiter().GetResult();
            Assert.Equal(22, store.Load()!.ShopCoins);
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
    }
#pragma warning restore xUnit1031

    private sealed class ReverseContinuationContext : SynchronizationContext
    {
        private readonly List<(SendOrPostCallback Callback, object? State)> queue = [];
        public override void Post(SendOrPostCallback callback, object? state)
        {
            lock (queue) { queue.Add((callback, state)); Monitor.PulseAll(queue); }
        }
        public void WaitForCount(int count)
        {
            if (!TryWaitForCount(count, TimeSpan.FromSeconds(10))) throw new TimeoutException("UI continuations did not arrive.");
        }
        public bool TryWaitForCount(int count, TimeSpan timeout)
        {
            lock (queue)
            {
                var deadline = DateTime.UtcNow.Add(timeout);
                while (queue.Count < count)
                {
                    var remaining = deadline - DateTime.UtcNow;
                    if (remaining <= TimeSpan.Zero || !Monitor.Wait(queue, remaining)) return false;
                }
                return true;
            }
        }
        public void RunLast()
        {
            WaitForCount(1);
            (SendOrPostCallback Callback, object? State) next;
            lock (queue) { next = queue[^1]; queue.RemoveAt(queue.Count - 1); }
            next.Callback(next.State);
        }
        public void PumpUntil(Func<bool> done)
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (!done())
            {
                if (DateTime.UtcNow >= deadline) throw new TimeoutException("UI barrier did not drain.");
                RunLast();
            }
        }
    }

    [Fact]
    public void RestoreValidBackupPreservesRejectedFileAndRejectsWrongSchema()
    {
        var valid = SaveMigration.Migrate(Old(Pet()), Lookup, Package.Value);
        valid.Settings.CareMode = CareMode.Hardcore;
        valid.Settings.SuperHardcoreEnabled = true;
        valid.CareStates[valid.Individuals[0].Id] = new() { RanAwayUntilUtc = DateTimeOffset.UtcNow.AddHours(1) };
        var backup = Path.Combine(root, "backup.json");
        var backupStore = new JsonSaveStore(backup); backupStore.Save(valid);
        File.WriteAllText(V2, "{broken v2"); var rejected = File.ReadAllBytes(V2);
        var targetStore = new JsonSaveStore(V2);
        SaveMigration.RestoreHistory(targetStore, backupStore.History.List().First(), Package.Value, DataRoot);
        Assert.Equal(File.ReadAllBytes(backup), File.ReadAllBytes(V2));
        var restored = targetStore.Load()!;
        Assert.Equal(CareMode.Chill, restored.Settings.CareMode);
        Assert.False(restored.Settings.SuperHardcoreEnabled);
        Assert.Null(restored.CareStates[valid.Individuals[0].Id].RanAwayUntilUtc);
        Assert.Equal(valid.Individuals[0].Id, restored.Individuals[0].Id);
        Assert.Contains(Directory.GetFiles(root, "state-v2.json.rejected-*"), p => File.ReadAllBytes(p).SequenceEqual(rejected));
        var wrong = Path.Combine(root, "v1backup.json"); var wrongStore = new JsonSaveStore(wrong); wrongStore.Save(Old(Pet()));
        var current = File.ReadAllBytes(V2);
        Assert.Throws<SaveLoadException>(() => SaveMigration.RestoreHistory(targetStore, wrongStore.History.List().First(), Package.Value, DataRoot));
        Assert.Equal(current, File.ReadAllBytes(V2));
    }

    [Theory]
    [InlineData("Tutorial")]
    [InlineData("Gift")]
    [InlineData("AwardLedger")]
    [InlineData("BerryRemainders")]
    public void MissingV2JournalIsRejectedWithoutReset(string field)
    {
        var node = JsonNode.Parse(JsonSaveStore.Serialize(SaveMigration.Migrate(Old(Pet()), Lookup, Package.Value)))!;
        node.AsObject().Remove(field); File.WriteAllText(V2, node.ToJsonString()); var original = File.ReadAllBytes(V2);
        Assert.Throws<SaveLoadException>(() => new JsonSaveStore(V2).Load());
        Assert.Equal(original, File.ReadAllBytes(V2));
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(1, 1)]
    [InlineData(0, 0)]
    public void InvalidRationalRemainderRejected(long numerator, long denominator)
    {
        var data = SaveMigration.Migrate(Old(Pet()), Lookup, Package.Value);
        data.BerryRemainders["oran"] = new() { Numerator = numerator, Denominator = denominator };
        File.WriteAllText(V2, JsonSaveStore.Serialize(data));
        Assert.Throws<SaveLoadException>(() => new JsonSaveStore(V2).Load());
    }

    [Fact]
    public void KnownCheckpointAndOtherSettingsRetainExactValues()
    {
        var old = Old(Pet());
        old.Settings.AutoHideInFullscreen = false; old.Settings.StartWithWindows = true; old.Settings.ScreenCorner = "TopLeft";
        old.Settings.PetSizePixels = 132; old.Settings.ShowProgressPanel = true; old.Settings.PetPositionX = 23; old.Settings.PetPositionY = 24;
        var checkpoint = DateTimeOffset.UtcNow.AddHours(-2);
        old.CareStates[old.Individuals[0].Id] = new() { LastInteractionUtc = checkpoint.AddHours(-1), LastNeglectCheckUtc = checkpoint, LastPetUtc = checkpoint.AddMinutes(-1) };
        var next = SaveMigration.Migrate(old, Lookup, Package.Value);
        old.Settings.CareMode = CareMode.Chill; old.Settings.SuperHardcoreEnabled = false;
        Assert.Equal(JsonSerializer.Serialize(old.Settings), JsonSerializer.Serialize(next.Settings));
        Assert.Equal(JsonSerializer.Serialize(old.CareStates), JsonSerializer.Serialize(next.CareStates));
        Assert.Equal(next.FeedingTreats, next.Items["treats"]);
    }

    [Fact]
    public void MissingMonitorAndOffscreenPositionUseExistingWpfNormalization()
    {
        var next = SaveMigration.Migrate(Old(Pet()), Lookup, Package.Value);
        PokeCorner.App.Tests.Theme.WpfTestHost.Run(() =>
        {
            var window = new global::PokeCorner.App.Overlay.OverlayWindow();
            window.PositionAt(global::PokeCorner.App.Overlay.ScreenCorner.BottomRight, next.Settings.MonitorIndex, next.Settings.PetSizePixels, int.MaxValue, int.MaxValue);
            Assert.True(double.IsFinite(window.Left)); Assert.True(double.IsFinite(window.Top));
            Assert.NotEqual((double)int.MaxValue, window.Left); Assert.NotEqual((double)int.MaxValue, window.Top);
            Assert.Equal(0, next.Settings.CrySoundVolume);
            window.AllowClose = true; window.Close();
        });
        Assert.Equal(999, next.Settings.MonitorIndex);
    }

    [Fact]
    public async Task TreatMutationUsesSingleAuthoritativeBalanceAndAlias()
    {
        var state = new AppState(); state.Hydrate(SaveMigration.Migrate(Old(Pet()), Lookup, Package.Value));
        var writer = new BackgroundSaveWriter(new JsonSaveStore(V2));
        await state.CommitAsync(writer, next => next.FeedingTreats -= 1, () => 2);
        var saved = new JsonSaveStore(V2).Load()!;
        Assert.Equal(7, state.Feeding.Treats); Assert.Equal(7, saved.FeedingTreats); Assert.Equal(7, saved.Items["treats"]);
        var node = JsonNode.Parse(JsonSaveStore.Serialize(saved))!; node["Items"]!["treats"] = 50;
        File.WriteAllText(V2, node.ToJsonString());
        Assert.Throws<SaveLoadException>(() => new JsonSaveStore(V2).Load());
    }

    [Fact]
    public void V2DisplayUsesSavedReachedBranchEvenIfInstalledLookupDiffers()
    {
        var chosen = new SpeciesEvolutionChain(67, 133, 45, 2, new[] { 133, 135 }, new[] { "Eevee", "Jolteon" });
        var migrated = SaveMigration.Migrate(Old(Pet(133, 67, 2, EvolutionStage.Stage2)), new(new[] { chosen }, new byte[] { 1 }), Package.Value);
        var individual = migrated.Individuals[0];
        Assert.Equal(135, individual.ResolveDisplaySpeciesId(Lookup.Chains));
        individual.DisplayStageOverride = 0;
        Assert.Equal(133, individual.ResolveDisplaySpeciesId(Array.Empty<SpeciesEvolutionChain>()));
        individual.DisplayStageOverride = null;
        Assert.Equal(135, individual.ResolveDisplaySpeciesId(Array.Empty<SpeciesEvolutionChain>()));
    }

    [Theory]
    [InlineData("FirstTraitId", "\"invented-trait\"")]
    [InlineData("CurrentSpeciesId", "99999")]
    public async Task UnknownV2TraitsAndSpeciesRequireRecovery(string field, string value)
    {
        var node = JsonNode.Parse(JsonSaveStore.Serialize(SaveMigration.Migrate(Old(Pet()), Lookup, Package.Value)))!;
        node["Individuals"]![0]![field] = JsonNode.Parse(value);
        if (field == "CurrentSpeciesId") node["Individuals"]![0]!["ReachedSpeciesIds"] = JsonNode.Parse("[99999]");
        File.WriteAllText(V2, node.ToJsonString()); var original = File.ReadAllBytes(V2);
        await Assert.ThrowsAsync<SaveLoadException>(() => SaveMigration.LoadAsync(V1, V2, Lookup, Package.Value));
        Assert.Equal(original, File.ReadAllBytes(V2));
    }

    [Fact]
    public async Task NonemptyV2GiftsWithoutSavedIdentityCannotMintReplacementGuid()
    {
        var node = JsonNode.Parse(JsonSaveStore.Serialize(SaveMigration.Migrate(Old(Pet()), Lookup, Package.Value)))!;
        node["Gift"]!["Gifts"]![0]!["Individual"]!.AsObject().Remove("Id");
        File.WriteAllText(V2, node.ToJsonString()); var original = File.ReadAllBytes(V2);
        await Assert.ThrowsAsync<SaveLoadException>(() => SaveMigration.LoadAsync(V1, V2, Lookup, Package.Value));
        Assert.Equal(original, File.ReadAllBytes(V2));
    }

    [Fact]
    public void CosmeticSelectorNeverOffersInstalledDifferentOrUnreachedBranch()
    {
        var chosen = new SpeciesEvolutionChain(67, 133, 45, 2, new[] { 133, 135 }, new[] { "Eevee", "Jolteon" });
        var state = new AppState();
        state.Hydrate(SaveMigration.Migrate(Old(Pet(133, 67, 2, EvolutionStage.Stage2)), new(new[] { chosen }, new byte[] { 1 }), Package.Value));
        var pet = state.Collection.GetActive()!;
        var vm = new global::PokeCorner.App.Popup.CollectionViewModel(state.Collection, state.CareStateByIndividualId, new(), Lookup.Chains, state.PendingEggs);
        var entry = vm.GetEntries(DateTimeOffset.UtcNow).Single();
        Assert.Equal(new int?[] { 133, 135 }, entry.EvolutionStages.Select(s => s.SpeciesId));
        Assert.DoesNotContain(entry.EvolutionStages, s => s.SpeciesName == "Vaporeon");
        pet.CurrentStage = EvolutionStage.Stage3;
        state.SetDisplayStage(pet, 2);
        Assert.Null(pet.DisplayStageOverride); Assert.Equal(135, pet.CurrentSpeciesId);
        pet.DisplayStageOverride = 2;
        Assert.Equal(135, pet.ResolveDisplaySpeciesId(Lookup.Chains));
        var stages = vm.GetEntries(DateTimeOffset.UtcNow).Single().EvolutionStages;
        Assert.Equal(new int?[] { 133, 135 }, stages.Select(s => s.SpeciesId));
        Assert.True(stages[1].IsCurrent);
    }

    [Fact]
    public async Task Fix1QueuedCoinsCommitDoesNotRestoreConsumedEncounter()
    {
        var state = new AppState(); state.Hydrate(SaveMigration.Migrate(Old(Pet()), Lookup, Package.Value));
        var store = new JsonSaveStore(V2);
        var pendingCount = 2;
        var write = new TaskCompletionSource<Action>(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = true;
        var writer = new BackgroundSaveWriter(store, action => { if (first) { first = false; write.SetResult(action); } else Task.Run(action); });
        var one = state.CommitAsync(writer, next => next.PendingCatchCount -= 1, () => pendingCount, next => pendingCount = next.PendingCatchCount);
        var two = state.CommitAsync(writer, next => next.ShopCoins += 3, () => pendingCount, next => pendingCount = next.PendingCatchCount);
        await Task.Run(await write.Task);
        await Task.WhenAll(one, two).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(1, pendingCount);
        Assert.Equal(1, store.Load()!.PendingCatchCount);
    }

    [Theory]
    [InlineData("ReachedSpeciesIds", "null")]
    [InlineData("ParentIds", "null")]
    [InlineData("TotalXp", "6401")]
    [InlineData("CurrentStage", "3")]
    [InlineData("FamilyRootId", "0")]
    [InlineData("DisplayStageOverride", "1")]
    public async Task Fix1ReservedGiftUsesCompleteIndividualValidation(string field, string value)
    {
        File.WriteAllText(V1, JsonSaveStore.Serialize(Old(Pet())));
        var node = JsonNode.Parse(JsonSaveStore.Serialize(SaveMigration.Migrate(Old(Pet()), Lookup, Package.Value)))!;
        node["Gift"]!["Gifts"]![0]!["Individual"]![field] = JsonNode.Parse(value);
        File.WriteAllText(V2, node.ToJsonString());
        var originalV2 = File.ReadAllBytes(V2); var originalV1 = File.ReadAllBytes(V1);
        Assert.Throws<SaveLoadException>(() => new JsonSaveStore(V2).Load());
        await Assert.ThrowsAsync<SaveLoadException>(() => SaveMigration.LoadAsync(V1, V2, Lookup, Package.Value));
        Assert.Equal(originalV2, File.ReadAllBytes(V2)); Assert.Equal(originalV1, File.ReadAllBytes(V1));
    }

    [Fact]
    public void Fix1DiscoveryAndShinyHistoriesSurviveAutosaveThenRelease()
    {
        var state = new AppState(); state.Hydrate(SaveMigration.Migrate(Old(Pet()), Lookup, Package.Value));
        var newPet = Pet(4, 4, 3);
        SaveMigration.InitializeIndividual(newPet, Lookup, Package.Value);
        state.Collection.Add(newPet);
        var writer = new BackgroundSaveWriter(new JsonSaveStore(V2), action => action());
        var beforeRelease = state.ToSaveData(2);
        writer.Post(beforeRelease);
        Assert.Contains(4, beforeRelease.DiscoveredSpeciesIds); Assert.Contains(4, beforeRelease.ShinySpeciesIds);
        state.Collection.Release(newPet.Id);
        var afterRelease = state.ToSaveData(2);
        writer.Post(afterRelease);
        var loaded = new JsonSaveStore(V2).Load()!;
        Assert.Contains(4, afterRelease.DiscoveredSpeciesIds); Assert.Contains(4, afterRelease.ShinySpeciesIds);
        Assert.Contains(4, loaded.DiscoveredSpeciesIds); Assert.Contains(4, loaded.ShinySpeciesIds);
        Assert.DoesNotContain(loaded.Individuals, pet => pet.Id == newPet.Id);
        Assert.Equal(beforeRelease.AwardLedger.Keys.Order(), loaded.AwardLedger.Keys.Order());
        Assert.Null(loaded.Metrics["successful-catches"]); Assert.Empty(loaded.EvolutionTransitions);
        var restarted = new AppState(); restarted.Hydrate(loaded);
        Assert.Contains(4, restarted.ToSaveData(2).DiscoveredSpeciesIds);
        Assert.Contains(4, restarted.ToSaveData(2).ShinySpeciesIds);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Fix1NullLegacyPathIsControlledWithoutFallbackOrFileChanges(bool preserved)
    {
        var path = Path.Combine(root, "lookup.json");
        var node = JsonSerializer.SerializeToNode(new[] { new SpeciesEvolutionChain(67, 133, 45, 2, new[] { 133, 135 }, new[] { "Eevee", "Jolteon" }) })!;
        node[0]!["StageSpeciesIds"] = null;
        File.WriteAllText(path, node.ToJsonString()); var original = File.ReadAllBytes(path);
        var error = Assert.Throws<InvalidDataException>(() => preserved
            ? LegacyLookup.Load(DataRoot, preservedLookupPath: path)
            : LegacyLookup.Load(DataRoot, existingCachePath: path));
        Assert.Contains("lookup", error.Message); Assert.Equal(original, File.ReadAllBytes(path));
        Assert.False(File.Exists(V2));
    }


    [Fact]
    public async Task FreshProfileDecisionRefusesLegacySaveThatAppearsBeforeFirstWrite()
    {
        var store = new JsonSaveStore(V2);
        Assert.Null(await SaveMigration.LoadAsync(V1, V2, Lookup, Package.Value, store));
        File.WriteAllText(V1, JsonSaveStore.Serialize(Old(Pet())));
        var fresh = SaveMigration.Migrate(Old(), Lookup, Package.Value);
        Assert.ThrowsAny<IOException>(() => store.Save(fresh));
        Assert.False(File.Exists(V2));
        Assert.Single(new JsonSaveStore(V1).Load()!.Individuals);
    }
    [Fact]
    public async Task ResetPublishesClearedCatchCountBeforeDeferredActions()
    {
        var writes = new List<Action>(); var writer = new BackgroundSaveWriter(new JsonSaveStore(V2), writes.Add);
        var state = new AppState(); state.Hydrate(SaveMigration.Migrate(Old(Pet()), Lookup, Package.Value));
        var pendingCount = 2; var observed = -1;
        var reset = SaveMigration.Migrate(Old(), Lookup, Package.Value);
        var commit = state.CommitAsync(writer, next => next.StarterSelectionPending = true, () => 0,
            afterPublish: _ => pendingCount = 0, replacement: reset);
        state.RunWhenReady(() => observed = pendingCount);
        writes[0](); await commit;
        Assert.Equal(0, observed); Assert.Empty(state.Collection.Individuals);
    }
    [Fact]
    public void ExitAfterPrimaryWriteFailureKeepsMemoryInHistoryWithoutReplacingForeignPrimary()
    {
        var state = new AppState(); state.Hydrate(SaveMigration.Migrate(Old(Pet()), Lookup, Package.Value));
        var store = new JsonSaveStore(V2); store.Save(state.ToSaveData(0));
        var writer = new BackgroundSaveWriter(store, write => write());
        const string foreign = "{foreign corrupt primary"; File.WriteAllText(V2, foreign);
        writer.Post(state.ToSaveData(0)); Assert.True(writer.HasFailed);
        state.Shop.Coins += 10;
        var app = (global::PokeCorner.App.App)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(global::PokeCorner.App.App));
        foreach (var (name, value) in new (string, object)[] { ("_appState", state), ("_saveStore", store), ("_saveWriter", writer), ("_hasLoadedState", true) })
            typeof(global::PokeCorner.App.App).GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(app, value);
        typeof(global::PokeCorner.App.App).GetMethod("SaveState", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(app, null);
        Assert.Equal(foreign, File.ReadAllText(V2));
        Assert.Contains(store.History.List(), entry => entry.SchemaVersion == 2 && entry.Coins == state.Shop.Coins && entry.FromMemory);
        Assert.Contains("из памяти", store.History.List().First(entry => entry.Coins == state.Shop.Coins).ToString());
        Assert.True(writer.HasFailed);
    }
}
