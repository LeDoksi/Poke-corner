using PokeCorner.Core.Domain;
using PokeCorner.Core.Persistence;
using PokeCorner.Core.PokeApi;
using PokeCorner.Core;

namespace PokeCorner.Core.Tests.GlobalUpdate;

public sealed class AchievementsTests
{
    private static AchievementService Service(CatalogPackage catalog)
    {
        File.Copy(Path.Combine(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../..")),
            "docs/design-v2/final/achievement-rewards.json"),
            Path.Combine(catalog.DataRoot, "final/achievement-rewards.json"));
        return new AchievementService(catalog);
    }

    private static SaveData State(CatalogPackage catalog) => new()
    {
        SchemaVersion = 2, CatalogRevision = catalog.CatalogRevision, BalanceRevision = "test",
        MigrationId = SaveMigration.MigrationId, Settings = new(), Individuals = [],
        Berries = SaveMigration.BerryIds.ToDictionary(id => id, _ => 0),
        Items = SaveMigration.ItemIds.ToDictionary(id => id, _ => 0),
        BerryRemainders = SaveMigration.BerryIds.ToDictionary(id => id, _ => new RationalRemainder())
    };

    [Fact]
    public void ClosedEncounterDoesNotCountAsCatchUntilOpened()
    {
        using var fixture = new NurseryCoreTests();
        var catalog = fixture.Catalog([1]);
        var state = State(catalog);
        var encounter = new EncounterState { Id = Guid.NewGuid(), Source = "expedition", Phase = "resolved",
            Individual = new PokemonIndividual { SpeciesId = 1, CurrentSpeciesId = 1, FamilyRootId = 1,
                EvolutionChainId = 1, ChainLength = 1, Nature = Nature.Hardy, IsShiny = false,
                ReachedSpeciesIds = [1], CaughtAtUtc = DateTimeOffset.UtcNow } };
        state.Encounters.Add(encounter);
        var service = Service(catalog);

        service.Evaluate(state);
        Assert.DoesNotContain("trophy-catches-1", state.AwardLedger.Keys);

        encounter.Phase = "opened";
        service.Evaluate(state);
        Assert.Equal(1, state.Metrics["successful-catches"]);
        Assert.Contains("trophy-catches-1", state.AwardLedger.Keys);
        var coins = state.ShopCoins;
        service.Evaluate(state);
        Assert.Equal(coins, state.ShopCoins);
    }

    [Fact]
    public void FinalGoalUsesReachableRootsAndEdgesAndKeepsItsRevision()
    {
        using var fixture = new NurseryCoreTests();
        var catalog = fixture.Catalog([1, 2, 25, 26]);
        var service = Service(catalog);
        Assert.Equal(new[] { 1, 2 }, service.ReachableSpeciesIds);
        var state = State(catalog);
        state.DiscoveredSpeciesIds.UnionWith([1, 2]);
        service.Evaluate(state);
        var award = state.AwardLedger["trophy-species-4"];
        Assert.Equal(new[] { 1, 2 }, award.AvailableSpeciesIds);
        Assert.Equal(catalog.CatalogRevision, award.CatalogRevision);
        state.DiscoveredSpeciesIds.Clear();
        service.Evaluate(state);
        Assert.Same(award, state.AwardLedger["trophy-species-4"]);
    }

    [Fact]
    public void RetroCreditPreservesUnknownActionsAndEarnedHistory()
    {
        using var fixture = new NurseryCoreTests();
        var catalog = fixture.Catalog([1]);
        var service = Service(catalog);
        var state = State(catalog);
        state.DiscoveredSpeciesIds.Add(1);
        state.ShinySpeciesIds.Add(1);
        state.Metrics["successful-catches"] = null;
        service.Evaluate(state);
        Assert.Contains("trophy-species-1", state.AwardLedger.Keys);
        Assert.Contains("trophy-shiny-1", state.AwardLedger.Keys);
        Assert.DoesNotContain("trophy-catches-1", state.AwardLedger.Keys);
        Assert.Null(state.Metrics["successful-catches"]);
        var coins = state.ShopCoins;
        service.Evaluate(state);
        Assert.Equal(coins, state.ShopCoins);
        Assert.Contains(1, state.DiscoveredSpeciesIds);
        Assert.Contains(1, state.ShinySpeciesIds);
    }

    [Fact]
    public void CanonicalCatalogContainsFiftySixOneTimeAwardsWorthEightHundredSixtyFiveCoins()
    {
        using var fixture = new NurseryCoreTests();
        var catalog = fixture.Catalog([1, 144]);
        var service = Service(catalog);
        var state = State(catalog);
        state.DiscoveredSpeciesIds.UnionWith(Enumerable.Range(1, 100));
        state.DiscoveredSpeciesIds.Add(144);
        state.ShinySpeciesIds.UnionWith(Enumerable.Range(1, 30));
        state.EvolutionTransitions.UnionWith(Enumerable.Range(1, 50).Select(i => $"{i}->{i + 1}"));
        var maxXp = new LevelProgression(catalog.DataRoot).MaxXp;
        state.Individuals.AddRange(Enumerable.Range(1, 100).Select(_ => new PokemonIndividual
        {
            SpeciesId = 1, CurrentSpeciesId = 1, FamilyRootId = 1, EvolutionChainId = 1,
            ChainLength = 1, Nature = Nature.Hardy, IsShiny = false,
            ReachedSpeciesIds = [1], CaughtAtUtc = DateTimeOffset.UtcNow,
            TotalXp = maxXp, Nickname = "Ace", IsFavorite = true,
            FirstTraitId = "collector", SecondTraitId = "finder"
        }));
        foreach (var key in new[] { "successful-catches", "claimed-expeditions", "visited-regions",
            "hard-expedition-in-each-region", "first-expedition-xp", "first-feeding",
            "first-care-evolution-ready", "distinct-cared-individuals", "distinct-hatched-root-species" })
            state.Metrics[key] = key switch
            {
                "successful-catches" => 500, "claimed-expeditions" => 200,
                "distinct-cared-individuals" => 50, "distinct-hatched-root-species" => 30, _ => 8
            };
        foreach (var berry in SaveMigration.BerryIds) state.Berries[berry] = 21;
        foreach (var id in new[] { "type-team", "bonus-find", "nursery-expedition", "family-expedition",
            "evolved-expedition", "berry-evolution", "branch-choice", "offspring-choice" })
            AchievementMetrics.Record(state, "special:trophy-special-" + id);
        var retrained = new RetrainingService(catalog, state).Retrain(state.Individuals[0].Id, 1, Guid.NewGuid());
        Assert.True(retrained.Success, retrained.Message);
        state.RehomingResults.Add(Guid.NewGuid(), new RehomeResult(Guid.NewGuid(), null, 6));
        state.CompletedOperations["potion:" + Guid.NewGuid()] = Guid.NewGuid();
        service.Evaluate(state);
        Assert.Equal(56, state.AwardLedger.Count);
        Assert.Equal(865, state.AwardLedger.Values.Sum(x => x.CoinsGranted));
        Assert.Equal(865, state.ShopCoins);
        service.Evaluate(state);
        Assert.Equal(865, state.ShopCoins);
    }

    [Fact]
    public void FrozenHatchAndDistinctCareMarkersSurviveReevaluation()
    {
        using var fixture = new NurseryCoreTests();
        var catalog = fixture.Catalog([172]);
        var service = Service(catalog);
        var state = State(catalog);
        var child = new PokemonIndividual
        {
            SpeciesId = 172, CurrentSpeciesId = 25, FamilyRootId = 172,
            EvolutionChainId = 10, ChainLength = 3, Nature = Nature.Hardy, IsShiny = false,
            ReachedSpeciesIds = [172, 25], CaughtAtUtc = DateTimeOffset.UtcNow
        };
        state.HatchedNurseryResults[Guid.NewGuid()] = child;
        AchievementMetrics.Record(state, "care:" + child.Id);
        service.Evaluate(state);
        Assert.Equal(1, state.Metrics["distinct-hatched-root-species"]);
        Assert.Equal(1, state.Metrics["distinct-cared-individuals"]);
        Assert.Contains("trophy-nursery-1", state.AwardLedger.Keys);
        var coins = state.ShopCoins;
        service.Evaluate(state);
        Assert.Equal(coins, state.ShopCoins);
    }

    [Fact]
    public async Task ConfirmedRetrainingAwardsOnceAndReplaysWithoutSpendingAgain()
    {
        using var fixture = new NurseryCoreTests();
        var catalog = fixture.Catalog([1]);
        var state = State(catalog);
        var individual = new PokemonIndividual
        {
            SpeciesId = 1, CurrentSpeciesId = 1, FamilyRootId = 1, EvolutionChainId = 1,
            ChainLength = 1, Nature = Nature.Hardy, IsShiny = false,
            ReachedSpeciesIds = [1], CaughtAtUtc = DateTimeOffset.UtcNow,
            FirstTraitId = "collector", TotalXp = 5
        };
        state.Individuals.Add(individual);
        foreach (var berry in new[] { "rawst", "pecha", "cheri" }) state.Berries[berry] = 20;
        var app = new AppState { Achievements = Service(catalog) };
        app.Hydrate(state);
        var operation = Guid.NewGuid();
        var path = Path.Combine(Path.GetTempPath(), "retrain-" + Guid.NewGuid() + ".json");
        try
        {
            var store = new JsonSaveStore(path);
            store.Save(state);
            var writer = new BackgroundSaveWriter(store);
            RetrainingAction? result = null;
            Directory.CreateDirectory(path + ".tmp");
            await Assert.ThrowsAnyAsync<IOException>(() => app.CommitAsync(writer, next =>
            {
                result = new RetrainingService(catalog, next).Retrain(individual.Id, 1, operation);
                Assert.True(result.Success);
            }, () => 0));
            var failedChoice = result!.TraitId;
            Assert.Empty(app.ToSaveData(0).AwardLedger);
            Assert.Equal(20, app.ToSaveData(0).Berries["rawst"]);
            Assert.Equal("collector", app.ToSaveData(0).Individuals.Single().FirstTraitId);
            Directory.Delete(path + ".tmp");
            writer = new BackgroundSaveWriter(new JsonSaveStore(path));
            await app.CommitAsync(writer, next =>
            {
                result = new RetrainingService(catalog, next).Retrain(individual.Id, 1, operation);
                Assert.True(result.Success);
            }, () => 0);
            Assert.Equal(failedChoice, result!.TraitId);
            Assert.Contains("trophy-special-retrain", store.Load()!.AwardLedger.Keys);
            Assert.Single(store.Load()!.RetrainingResults);
            Assert.Equal(individual.Id, store.Load()!.Individuals.Single().Id);
            Assert.Equal(5, store.Load()!.Individuals.Single().TotalXp);
            Assert.False(store.Load()!.Individuals.Single().IsShiny);
            Assert.Null(store.Load()!.Individuals.Single().SecondTraitId);
            var coins = store.Load()!.ShopCoins;
            await app.CommitAsync(writer, next =>
            {
                result = new RetrainingService(catalog, next).Retrain(individual.Id, 1, operation);
                Assert.True(result.Success);
                Assert.True(result.Repeated);
            }, () => 0);
            Assert.Equal(coins, store.Load()!.ShopCoins);
            Assert.Equal(0, store.Load()!.Berries["rawst"]);
            Assert.Equal(0, store.Load()!.Berries["pecha"]);
            Assert.Equal(0, store.Load()!.Berries["cheri"]);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (Directory.Exists(path + ".tmp")) Directory.Delete(path + ".tmp");
        }
    }

    [Fact]
    public void RetrainingBlocksBusyOrMissingResourcesAndExcludesBothOldTraits()
    {
        using var fixture = new NurseryCoreTests();
        var catalog = fixture.Catalog([1]);
        var state = State(catalog);
        var individual = new PokemonIndividual
        {
            SpeciesId = 1, CurrentSpeciesId = 1, FamilyRootId = 1, EvolutionChainId = 1,
            ChainLength = 1, Nature = Nature.Hardy, IsShiny = false,
            ReachedSpeciesIds = [1], CaughtAtUtc = DateTimeOffset.UtcNow,
            FirstTraitId = "collector", SecondTraitId = "finder"
        };
        state.Individuals.Add(individual);
        var service = new RetrainingService(catalog, state);
        var operation = Guid.NewGuid();
        Assert.False(service.Retrain(individual.Id, 1, operation).Success);
        foreach (var berry in new[] { "rawst", "pecha", "cheri" }) state.Berries[berry] = 20;
        state.Nest = new NurseryState { Status = "ready", ParticipantIds = [individual.Id], Child = individual };
        Assert.False(service.Retrain(individual.Id, 1, operation).Success);
        state.Nest = null;
        var preview = service.Preview(individual.Id, 1);
        Assert.Equal(3, preview.Costs.Count);
        Assert.All(preview.Costs, cost => Assert.Equal(20, cost.Required));
        Assert.DoesNotContain("collector", preview.Outcomes);
        Assert.DoesNotContain("finder", preview.Outcomes);
        Assert.Null(preview.Reason);
        Assert.True(service.Retrain(individual.Id, 1, operation).Success);
        Assert.Equal("finder", individual.SecondTraitId);
        Assert.False(service.Retrain(individual.Id, 2, operation).Success);
    }

    [Fact]
    public async Task FailedCommitDoesNotPublishAwardAndRetryGrantsItOnce()
    {
        using var fixture = new NurseryCoreTests();
        var catalog = fixture.Catalog([1]);
        var service = Service(catalog);
        var initial = State(catalog);
        var app = new AppState { Achievements = service };
        app.Hydrate(initial);
        var caredId = Guid.NewGuid();
        var path = Path.Combine(Path.GetTempPath(), "achievement-" + Guid.NewGuid() + ".json");
        try
        {
            var store = new JsonSaveStore(path);
            store.Save(initial);
            Directory.CreateDirectory(path + ".tmp");
            var writer = new BackgroundSaveWriter(store);
            void Feed(SaveData next)
            {
                AchievementMetrics.Record(next, "first-feeding");
                AchievementMetrics.Record(next, "care:" + caredId);
            }
            await Assert.ThrowsAnyAsync<IOException>(() => app.CommitAsync(writer, Feed, () => 0));
            Assert.Empty(app.ToSaveData(0).AwardLedger);
            Assert.Empty(store.Load()!.AwardLedger);
            Assert.False(app.ToSaveData(0).Metrics.ContainsKey("care:" + caredId));
            Directory.Delete(path + ".tmp");
            var recoveredStore = new JsonSaveStore(path);
            var recoveredWriter = new BackgroundSaveWriter(recoveredStore);
            await app.CommitAsync(recoveredWriter, Feed, () => 0);
            Assert.Equal(5, recoveredStore.Load()!.ShopCoins);
            Assert.Contains("trophy-care-1", recoveredStore.Load()!.AwardLedger.Keys);
            Assert.Equal(1, recoveredStore.Load()!.Metrics["distinct-cared-individuals"]);
            await app.CommitAsync(recoveredWriter, Feed, () => 0);
            Assert.Equal(5, recoveredStore.Load()!.ShopCoins);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (Directory.Exists(path + ".tmp")) Directory.Delete(path + ".tmp");
        }
    }
}
