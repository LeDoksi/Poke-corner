using PokeCorner.Core.Domain;
using PokeCorner.Core.Persistence;

namespace PokeCorner.Core.Tests.Persistence;

public sealed class SaveProtectionTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "SaveProtection_" + Guid.NewGuid());
    private string Primary => Path.Combine(root, "state-v2.json");
    public SaveProtectionTests() => Directory.CreateDirectory(root);
    public void Dispose() => Directory.Delete(root, true);
    private static SaveData State(int count = 100, int coins = 0) => new()
    {
        Individuals = Enumerable.Range(0, count).Select(_ => new PokemonIndividual
        {
            SpeciesId = 1, CurrentSpeciesId = 1, FamilyRootId = 1, EvolutionChainId = 1, ChainLength = 3,
            FirstTraitId = SaveMigration.TraitIds[0], ReachedSpeciesIds = [1],
            Nature = Nature.Hardy, IsShiny = true, CaughtAtUtc = DateTimeOffset.Parse("2026-01-01T00:00:00Z")
        }).ToList(), Settings = new(), ShopCoins = coins, SchemaVersion = 2,
        CatalogRevision = "synthetic-v2", BalanceRevision = "synthetic-v2", MigrationId = SaveMigration.MigrationId,
        Berries = SaveMigration.BerryIds.ToDictionary(id => id, _ => 0),
        Items = SaveMigration.ItemIds.ToDictionary(id => id, _ => 0),
        BerryRemainders = SaveMigration.BerryIds.ToDictionary(id => id, _ => new RationalRemainder())
    };

    [Fact]
    public void MissingPrimaryWithHistoryRefusesNewGame()
    {
        var store = new JsonSaveStore(Primary); store.Save(State());
        File.Delete(Primary);
        Assert.Throws<SaveLoadException>(() => new JsonSaveStore(Primary).Load());
        File.WriteAllText(Primary, JsonSaveStore.Serialize(State(1)));
        Assert.ThrowsAny<IOException>(() => store.Save(State(1)));
        Assert.Single(new JsonSaveStore(Primary).Load()!.Individuals);
    }

    [Fact]
    public void FirstWriteRefusesSaveThatAppearedAfterFreshDecision()
    {
        var store = new JsonSaveStore(Primary); Assert.Null(store.Load());
        var original = JsonSaveStore.Serialize(State()); File.WriteAllText(Primary, original);
        Assert.ThrowsAny<IOException>(() => store.Save(State(1)));
        Assert.Equal(original, File.ReadAllText(Primary));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OrdinaryWriteRefusesExternalChange(bool delete)
    {
        var store = new JsonSaveStore(Primary); store.Save(State());
        if (delete) File.Delete(Primary); else File.WriteAllText(Primary, "{corrupt external replacement");
        Assert.ThrowsAny<IOException>(() => store.Save(State(1)));
        if (delete) Assert.False(File.Exists(Primary));
        else Assert.Equal("{corrupt external replacement", File.ReadAllText(Primary));
    }

    [Fact]
    public void InterruptedFirstWriteIsNotNewGame()
    {
        File.WriteAllText(Primary + ".tmp", JsonSaveStore.Serialize(State()));
        Assert.Throws<SaveLoadException>(() => new JsonSaveStore(Primary).Load());
        Assert.True(File.Exists(Primary + ".tmp"));
    }

    [Fact]
    public void ExistingEmptyCollectionRemainsValid()
    {
        new JsonSaveStore(Primary).Save(State(0));
        Assert.Empty(new JsonSaveStore(Primary).Load()!.Individuals);
    }

    [Fact]
    public void LeaseConflictsUntilDisposed()
    {
        using (var first = SaveProfileLease.Acquire(root))
            Assert.ThrowsAny<IOException>(() => SaveProfileLease.Acquire(root));
        using var next = SaveProfileLease.Acquire(root);
    }

    [Fact]
    public void BackupFailureLeavesPrimaryBytesIntact()
    {
        var original = JsonSaveStore.Serialize(State()); File.WriteAllText(Primary, original);
        var store = new JsonSaveStore(Primary); store.Load();
        File.WriteAllText(store.History.DirectoryPath, "blocked directory");
        Assert.ThrowsAny<IOException>(() => store.Save(State(1)));
        Assert.Equal(original, File.ReadAllText(Primary));
        File.Delete(store.History.DirectoryPath);
        Assert.ThrowsAny<IOException>(() => store.Save(State(1)));
    }

    [Fact]
    public void HistorySurvivesRapidAutosavesAndRestoresOriginalShinyProgress()
    {
        var now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var store = new JsonSaveStore(Primary, () => now);
        var original = State(); original.Individuals[0].CurrentStage = EvolutionStage.Stage2;
        original.Individuals[0].CurrentSpeciesId = 2; original.Individuals[0].ReachedSpeciesIds = [1, 2];
        store.Save(original);
        for (var i = 0; i < 40; i++) { now = now.AddSeconds(1); store.Save(State(1, i)); }
        var hundred = store.History.List().First(e => e.CollectionCount == 100);
        Assert.Contains(store.History.List(), e => e.CollectionCount == 1);
        Assert.Equal(20, Directory.GetFiles(store.History.DirectoryPath, "recent-*.checkpoint").Length);
        store.RestoreBytes(store.History.ReadValidated(hundred));
        var restored = store.Load()!;
        Assert.Equal(original.Individuals.Select(i => i.Id), restored.Individuals.Select(i => i.Id));
        Assert.All(restored.Individuals, i => Assert.True(i.IsShiny));
        Assert.Equal(EvolutionStage.Stage2, restored.Individuals[0].CurrentStage);
        Assert.Contains(store.History.List(), e => e.CollectionCount == 1);
    }

    [Fact]
    public void DailyAnchorSurvivesWhenOriginalIsNoLongerInitialOrRecent()
    {
        var now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var store = new JsonSaveStore(Primary, () => now); store.Save(State(1));
        now = now.AddDays(1); store.Save(State());
        now = now.AddDays(1); store.Save(State(1));
        for (var i = 0; i < 40; i++) { now = now.AddSeconds(1); store.Save(State(1, i)); }
        Assert.Contains(store.History.List(), e => e.CollectionCount == 100);
        for (var day = 0; day < 31; day++) { now = now.AddDays(1); store.Save(State(1, 100 + day)); }
        Assert.Equal(30, Directory.GetFiles(store.History.DirectoryPath, "day-*.checkpoint").Length);
        Assert.Equal(20, Directory.GetFiles(store.History.DirectoryPath, "recent-*.checkpoint").Length);
        Assert.True(File.Exists(Path.Combine(store.History.DirectoryPath, "initial.checkpoint")));
    }

    [Fact]
    public void SameDayPeakSurvivesPruningAndLatestPeakWinsTie()
    {
        var now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var store = new JsonSaveStore(Primary, () => now);
        store.Save(State(1));
        now = now.AddSeconds(1); store.Save(State(100));
        now = now.AddSeconds(1); store.Save(State(100, 5));
        now = now.AddSeconds(1); var lastPeakCapture = now; store.Save(State(1));
        for (var i = 0; i < 40; i++) { now = now.AddSeconds(1); store.Save(State(1, i + 10)); }

        var day = Path.Combine(store.History.DirectoryPath, "day-2026-01-01.checkpoint");
        var node = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllBytes(day))!;
        var bytes = Convert.FromBase64String(node["Bytes"]!.GetValue<string>());
        Assert.Equal(lastPeakCapture, node["SavedAtUtc"]!.GetValue<DateTimeOffset>());
        Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant(), node["Sha256"]!.GetValue<string>());
        var peak = Assert.Single(store.History.List().Where(e => e.CollectionCount == 100 && e.Coins == 5));
        Assert.Equal(day, peak.Path);
        Assert.Equal(20, Directory.GetFiles(store.History.DirectoryPath, "recent-*.checkpoint").Length);
        store.RestoreBytes(store.History.ReadValidated(peak));
        Assert.Equal(100, store.Load()!.Individuals.Count);
    }

    [Fact]
    public void IdenticalAutosavesDeduplicateCheckpoints()
    {
        var store = new JsonSaveStore(Primary); var snapshot = State();
        for (var i = 0; i < 25; i++) store.Save(snapshot);
        Assert.Single(store.History.List());
        Assert.Single(Directory.GetFiles(store.History.DirectoryPath, "recent-*.checkpoint"));
    }

    [Fact]
    public void RestoreRevalidatesChecksumAndPreservesCorruptPrimary()
    {
        var store = new JsonSaveStore(Primary); store.Save(State());
        var checkpoint = store.History.List().First();
        var backup = File.ReadAllBytes(checkpoint.Path);
        var node = System.Text.Json.Nodes.JsonNode.Parse(backup)!;
        node["Sha256"] = "bad"; File.WriteAllText(checkpoint.Path, node.ToJsonString());
        Assert.ThrowsAny<IOException>(() => store.History.ReadValidated(checkpoint));
        Assert.Equal(100, store.Load()!.Individuals.Count);
        File.WriteAllBytes(checkpoint.Path, backup);
        const string rejected = "{corrupt/future primary"; File.WriteAllText(Primary, rejected);
        Assert.Throws<SaveLoadException>(() => store.Load());
        store.RestoreBytes(store.History.ReadValidated(checkpoint));
        Assert.Equal(100, store.Load()!.Individuals.Count);
        Assert.Equal(rejected, File.ReadAllText(Assert.Single(Directory.GetFiles(root, "state-v2.json.rejected-*"))));
    }

    [Fact]
    public void RestorePreservesInterruptedTempBeforeReplacingIt()
    {
        var store = new JsonSaveStore(Primary); store.Save(State());
        File.Delete(Primary); File.WriteAllText(Primary + ".tmp", "half-written");
        store.RestoreBytes(store.History.ReadValidated(store.History.List().First()));
        Assert.Equal("half-written", File.ReadAllText(Assert.Single(Directory.GetFiles(root, "state-v2.json.interrupted-*"))));
        Assert.Equal(100, store.Load()!.Individuals.Count);
        store.Save(State(1));
    }

    [Theory]
    [InlineData("{corrupt")]
    [InlineData("{\"SchemaVersion\":99,\"Individuals\":[],\"Settings\":{}}")]
    public void InvalidPrimaryNeverBecomesFresh(string bytes)
    {
        File.WriteAllText(Primary, bytes); var store = new JsonSaveStore(Primary);
        Assert.Throws<SaveLoadException>(() => store.Load());
        Assert.ThrowsAny<IOException>(() => store.Save(State(1)));
        Assert.Equal(bytes, File.ReadAllText(Primary));
    }

    [Fact]
    public void ExclusivePrimaryReadErrorNeverBecomesFresh()
    {
        File.WriteAllText(Primary, JsonSaveStore.Serialize(State()));
        using var locked = new FileStream(Primary, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Throws<SaveLoadException>(() => new JsonSaveStore(Primary).Load());
    }

    [Fact]
    public async Task WriterLatchesFailureAndDoesNotRetryStaleCandidate()
    {
        var store = new JsonSaveStore(Primary); store.Save(State());
        var pending = new List<Action>(); var writer = new BackgroundSaveWriter(store, pending.Add);
        var failureCount = 0; writer.SaveFailed += _ => failureCount++;
        writer.Post(State(1)); writer.Post(State(2));
        const string replacement = "{external unknown"; File.WriteAllText(Primary, replacement);
        pending[0](); pending[1]();
        Assert.True(writer.HasFailed); Assert.Equal(1, failureCount);
        writer.Post(State(3)); Assert.Equal(2, pending.Count);
        var commit = writer.CommitAsync(State(4)); pending[2]();
        await Assert.ThrowsAnyAsync<IOException>(() => commit);
        Assert.Equal(replacement, File.ReadAllText(Primary));
    }

    [Fact]
    public void RestoreRefusesCheckpointChangedAfterSelectionEvenWithValidNewChecksum()
    {
        var store = new JsonSaveStore(Primary); store.Save(State());
        var selected = store.History.List().First();
        var node = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllBytes(selected.Path))!;
        var changed = System.Text.Encoding.UTF8.GetBytes(JsonSaveStore.Serialize(State(1)));
        node["Bytes"] = Convert.ToBase64String(changed);
        node["Sha256"] = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(changed)).ToLowerInvariant();
        File.WriteAllText(selected.Path, node.ToJsonString());
        Assert.ThrowsAny<IOException>(() => store.History.ReadValidated(selected));
        Assert.Equal(100, store.Load()!.Individuals.Count);
    }

    [Fact]
    public void TwoLoadedWritersCannotReplaceEachOthersProgress()
    {
        new JsonSaveStore(Primary).Save(State());
        var first = new JsonSaveStore(Primary); var second = new JsonSaveStore(Primary);
        first.Load(); second.Load(); first.Save(State(2));
        Assert.ThrowsAny<IOException>(() => second.Save(State(1)));
        Assert.Equal(2, new JsonSaveStore(Primary).Load()!.Individuals.Count);
    }
}
